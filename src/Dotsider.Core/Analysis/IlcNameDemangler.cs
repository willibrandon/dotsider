using System.Text;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Maps ILC-mangled native symbol names back to managed names and classifies compiler-generated
/// symbols by kind. ILC's name mangler is deterministic (NativeAotNameMangler in dotnet/runtime):
/// a type mangles to <c>{assembly}_{namespace}_{Outer}_{Inner}</c> with <c>System.Private.*</c>
/// abbreviated to <c>S.P.*</c>, generic instantiations and constructed forms wrapped in
/// <c>&lt;…&gt;</c>, and a method appended as <c>{mangledType}__{method}</c>. Because every
/// non-identifier character (including the <c>.</c>, <c>+</c>, <c>&lt;</c>, <c>&gt;</c>, <c>$</c>
/// that would mark boundaries) collapses to a single <c>_</c>, the split points are ambiguous —
/// so rather than guess, the demangler joins a sanitized symbol against the set of names it
/// recovered from the binary's own metadata (<see cref="RecoveredType"/>) and reports an exact
/// match only when the join is unambiguous. Node-level prefixes (vtables, statics, frozen
/// strings, dictionaries, data) are classified from their spelling before any join.
/// </summary>
internal sealed class IlcNameDemangler
{
    /// <summary>The outcome of demangling a single symbol.</summary>
    /// <param name="ManagedName">The managed name, or null when no confident name could be produced.</param>
    /// <param name="Kind">The classified symbol kind.</param>
    /// <param name="IsExactMatch">Whether <paramref name="ManagedName"/> is an unambiguous join rather than a heuristic display.</param>
    internal readonly record struct Result(string? ManagedName, NativeSymbolKind Kind, bool IsExactMatch);

    // Sanitized type key -> managed FullName; sanitized "{typeKey}__{method}" -> managed "FullName.method".
    // Ambiguous keys (sanitization collisions or repeated names) map to null so they can't claim an exact match.
    private readonly Dictionary<string, string?> _typeByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string?> _methodByKey = new(StringComparer.Ordinal);

    /// <summary>
    /// Builds a demangler from the types recovered from a binary's embedded metadata.
    /// </summary>
    /// <param name="recoveredTypes">The recovered types, each carrying its assembly scope and method names.</param>
    internal IlcNameDemangler(IReadOnlyList<RecoveredType> recoveredTypes)
    {
        foreach (var type in recoveredTypes)
        {
            var typeKey = TypeKey(type);
            if (typeKey.Length == 0) continue;
            Add(_typeByKey, typeKey, type.FullName);

            foreach (var method in type.MethodNames)
                Add(_methodByKey, $"{typeKey}__{Sanitize(method)}", $"{type.FullName}.{method}");
        }
    }

    /// <summary>
    /// Demangles and classifies a raw native symbol name.
    /// </summary>
    /// <param name="symbol">The raw symbol name from the PDB/DWARF/nlist reader (Mach-O's leading <c>_</c> already stripped).</param>
    internal Result Demangle(string symbol)
    {
        if (string.IsNullOrEmpty(symbol)) return new Result(null, NativeSymbolKind.Data, false);

        // Node-level classification first — these are data, not functions, and never join as methods.
        if (TryClassifyNode(symbol, out var nodeResult)) return nodeResult;

        // A function: strip generic/constructed <…> scopes, then join against known methods.
        var flat = StripScopes(symbol);

        if (_methodByKey.TryGetValue(flat, out var exact) && exact is not null)
            return new Result(exact, NativeSymbolKind.Function, true);

        // Overload disambiguation suffix (_0, _1, …) that metadata cannot distinguish.
        var deSuffixed = StripTrailingOverloadSuffix(flat);
        if (deSuffixed is not null && _methodByKey.TryGetValue(deSuffixed, out var overload) && overload is not null)
            return new Result(overload, NativeSymbolKind.Function, false);

        // Known type, method absent from the (sparse) metadata: name it by its longest known type prefix.
        if (TryMatchByTypePrefix(flat, out var byType))
            return new Result(byType, NativeSymbolKind.Function, false);

        // No join: a heuristic display with the assembly moniker expanded, but no managed name.
        return new Result(null, NativeSymbolKind.Function, false);
    }

    /// <summary>Expands ILC assembly monikers for display (e.g. <c>S_P_CoreLib</c> → <c>System.Private.CoreLib</c>).</summary>
    internal static string PrettifyForDisplay(string symbol)
    {
        if (symbol.StartsWith("S_P_", StringComparison.Ordinal))
            return "System.Private." + symbol[4..];
        return symbol;
    }

    private bool TryClassifyNode(string symbol, out Result result)
    {
        // Windows vtable: ??_7{type}@@6B@   Unix vtable: _ZTV{decimalLength}{type}
        if (symbol.StartsWith("??_7", StringComparison.Ordinal) && symbol.EndsWith("@@6B@", StringComparison.Ordinal))
        {
            var inner = symbol[4..^5];
            result = new Result(MethodTableName(inner), NativeSymbolKind.MethodTable, IsExactMatchForType(inner));
            return true;
        }

        if (symbol.StartsWith("_ZTV", StringComparison.Ordinal))
        {
            var inner = StripItaniumLengthPrefix(symbol[4..]);
            result = new Result(MethodTableName(inner), NativeSymbolKind.MethodTable, IsExactMatchForType(inner));
            return true;
        }

        // Frozen string literals: {compilationUnitPrefix}__Str_…
        if (symbol.Contains("__Str_", StringComparison.Ordinal))
        {
            result = new Result(null, NativeSymbolKind.FrozenObject, false);
            return true;
        }

        if (symbol.StartsWith("__GenericDict_", StringComparison.Ordinal))
        {
            result = new Result(null, NativeSymbolKind.GenericDictionary, false);
            return true;
        }

        // Statics — Windows MSVC form ?__GCSTATICS@… / Unix __GCSTATICS…
        if (IsStatics(symbol))
        {
            result = new Result(null, NativeSymbolKind.Statics, false);
            return true;
        }

        if (symbol.StartsWith("__readonlydata_", StringComparison.Ordinal)
            || symbol.StartsWith("__writableData", StringComparison.Ordinal))
        {
            result = new Result(null, NativeSymbolKind.Data, false);
            return true;
        }

        if (symbol.StartsWith("__unbox", StringComparison.Ordinal) || symbol.Contains("unwind", StringComparison.Ordinal))
        {
            result = new Result(null, NativeSymbolKind.Stub, false);
            return true;
        }

        result = default;
        return false;
    }

    private static bool IsStatics(string s) =>
        s.Contains("__GCSTATICS", StringComparison.Ordinal)
        || s.Contains("__NONGCSTATICS", StringComparison.Ordinal)
        || s.Contains("__THREADSTATICS", StringComparison.Ordinal)
        || s.Contains("__TypeThreadStaticIndex", StringComparison.Ordinal);

    private string MethodTableName(string mangledType)
    {
        var flat = StripScopes(mangledType);
        return _typeByKey.TryGetValue(flat, out var name) && name is not null
            ? $"{name} (MethodTable)"
            : $"{PrettifyForDisplay(mangledType)} (MethodTable)";
    }

    private bool IsExactMatchForType(string mangledType) =>
        _typeByKey.TryGetValue(StripScopes(mangledType), out var name) && name is not null;

    private bool TryMatchByTypePrefix(string flat, out string managedName)
    {
        // Walk the '__' method-separator candidates, longest type prefix first.
        for (var i = flat.Length - 2; i > 0; i--)
        {
            if (flat[i] != '_' || flat[i + 1] != '_') continue;
            var typeKey = flat[..i];
            if (_typeByKey.TryGetValue(typeKey, out var typeName) && typeName is not null)
            {
                managedName = $"{typeName}.{flat[(i + 2)..]}";
                return true;
            }
        }

        managedName = "";
        return false;
    }

    private static string? StripTrailingOverloadSuffix(string flat)
    {
        var i = flat.Length - 1;
        while (i >= 0 && char.IsAsciiDigit(flat[i])) i--;
        if (i == flat.Length - 1 || i < 1 || flat[i] != '_') return null;
        return flat[..i];
    }

    /// <summary>Removes balanced <c>&lt;…&gt;</c> scopes (generic instantiations, constructed forms) at any nesting depth.</summary>
    private static string StripScopes(string s)
    {
        var open = s.IndexOf('<', StringComparison.Ordinal);
        if (open < 0) return s;

        var sb = new StringBuilder(s.Length);
        var depth = 0;
        foreach (var c in s)
        {
            if (c == '<') depth++;
            else if (c == '>') { if (depth > 0) depth--; }
            else if (depth == 0) sb.Append(c);
        }

        return sb.ToString();
    }

    /// <summary>Strips the Itanium <c>_ZTV</c> decimal length prefix, leaving the mangled type name.</summary>
    private static string StripItaniumLengthPrefix(string s)
    {
        var i = 0;
        while (i < s.Length && char.IsAsciiDigit(s[i])) i++;
        return i < s.Length ? s[i..] : s;
    }

    private static string TypeKey(RecoveredType type)
    {
        var assembly = type.AssemblyName is { Length: > 0 } asm ? Abbreviate(asm) : "";
        var sanitizedType = Sanitize(type.FullName);
        return assembly.Length > 0 ? $"{Sanitize(assembly)}_{sanitizedType}" : sanitizedType;
    }

    private static string Abbreviate(string assemblyName) =>
        assemblyName.StartsWith("System.Private.", StringComparison.Ordinal)
            ? "S.P." + assemblyName["System.Private.".Length..]
            : assemblyName;

    /// <summary>
    /// Applies ILC's <c>SanitizeName</c>: ASCII letters and <c>_</c> pass through, digits pass
    /// (a leading digit is prefixed with <c>_</c>), and every other character — including each
    /// multibyte codepoint — becomes a single <c>_</c>.
    /// </summary>
    internal static string Sanitize(ReadOnlySpan<char> s)
    {
        var sb = new StringBuilder(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (char.IsAsciiLetter(c) || c == '_')
            {
                sb.Append(c);
            }
            else if (char.IsAsciiDigit(c))
            {
                if (i == 0) sb.Append('_');
                sb.Append(c);
            }
            else
            {
                sb.Append('_');
                // A surrogate pair is one codepoint → one underscore.
                if (char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])) i++;
            }
        }

        return sb.ToString();
    }

    private static void Add(Dictionary<string, string?> map, string key, string value)
    {
        // First writer wins the value; a second, different value marks the key ambiguous (null).
        if (map.TryGetValue(key, out var existing))
        {
            if (!string.Equals(existing, value, StringComparison.Ordinal))
                map[key] = null;
        }
        else
        {
            map[key] = value;
        }
    }
}
