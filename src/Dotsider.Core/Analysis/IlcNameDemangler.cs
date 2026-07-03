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
    /// <param name="ManagedName">The managed name joined from recovered metadata, or null when no join exists — heuristics never populate it. Overloads share a name, so precision lives in <paramref name="IsExactMatch"/>.</param>
    /// <param name="Kind">The classified symbol kind.</param>
    /// <param name="IsExactMatch">Whether <paramref name="ManagedName"/> identifies exactly one recovered member.</param>
    internal readonly record struct Result(string? ManagedName, NativeSymbolKind Kind, bool IsExactMatch);

    /// <summary>A method-key join: the shared display name and whether the key is ambiguous.</summary>
    /// <param name="Name">The managed display name, or null when a sanitization collision leaves no single name.</param>
    /// <param name="Ambiguous">Whether more than one recovered method produced this key (overloads share their name).</param>
    private readonly record struct MethodJoin(string? Name, bool Ambiguous);

    // Sanitized type key -> managed FullName; sanitized "{typeKey}__{method}" -> the method join.
    // A type key colliding with a different FullName maps to null; a method key hit twice is
    // ambiguous even when the display name is identical — overloads share a name, and without a
    // signature the reader cannot tell which one a native symbol is.
    private readonly Dictionary<string, string?> _typeByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MethodJoin> _methodByKey = new(StringComparer.Ordinal);

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
                AddMethod($"{typeKey}__{Sanitize(method)}", $"{type.FullName}.{method}");
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

        if (_methodByKey.TryGetValue(flat, out var join) && join.Name is not null)
            return new Result(join.Name, NativeSymbolKind.Function, !join.Ambiguous);

        // Overload disambiguation suffix (_0, _1, …) that metadata cannot distinguish.
        var deSuffixed = StripTrailingOverloadSuffix(flat);
        if (deSuffixed is not null && _methodByKey.TryGetValue(deSuffixed, out var overload) && overload.Name is not null)
            return new Result(overload.Name, NativeSymbolKind.Function, false);

        // No join: heuristics never claim a managed name — the raw name stays the display.
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

    private void AddMethod(string key, string value)
    {
        // Any second writer marks the key ambiguous — even with an identical display name,
        // duplicates mean overloads, and no signature can say which one a symbol is. The shared
        // name survives when it is the same, so the symbol can still be named, just not exactly.
        if (_methodByKey.TryGetValue(key, out var existing))
        {
            _methodByKey[key] = new MethodJoin(
                string.Equals(existing.Name, value, StringComparison.Ordinal) ? existing.Name : null,
                Ambiguous: true);
        }
        else
        {
            _methodByKey[key] = new MethodJoin(value, Ambiguous: false);
        }
    }
}
