using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Joins pre-ILC managed methods to the native evidence of the AOT image they were
/// compiled into: native symbols (via <see cref="IlcNameDemangler"/>, keyed from real
/// companion metadata instead of the binary's reduced recovered types) and mstat size
/// rows. Built once, queried per-frame — every lookup is a dictionary hit.
/// </summary>
/// <remarks>
/// The join is grouped by <c>(assembly, declaring type, method name)</c>: ILC's mangling
/// collapses signatures, so overloads form one evidence pool that no single candidate
/// owns. A single-method group owns its evidence (<see cref="MethodCorrelationStatus.CorrelatedExact"/>,
/// several symbols meaning generic instantiations); a multi-method group is shared
/// (<see cref="MethodCorrelationStatus.CorrelatedAmbiguous"/>), reported on every sibling
/// but counted once in <see cref="TotalCorrelatedSize"/>. Overload-suffix assignment
/// (<c>_0</c>/<c>_1</c>) is never guessed — the same policy the demangler applies.
/// </remarks>
public sealed class ManagedNativeIndex
{
    private readonly Dictionary<(string AssemblyName, int Token), MethodCorrelation> _byToken;

    // Correlated symbol ranges sorted by start address. An address that lands inside a
    // symbol (a stack-trace or disassembly target, not just the entry point) resolves via
    // range containment, not an exact-VA hash lookup.
    private readonly (ulong Start, ulong End, MethodCorrelation Correlation)[] _addressRanges;

    private ManagedNativeIndex(
        List<MethodCorrelation> methods,
        Dictionary<(string, int), MethodCorrelation> byToken,
        (ulong Start, ulong End, MethodCorrelation Correlation)[] addressRanges,
        long totalCorrelatedSize)
    {
        Methods = methods;
        _byToken = byToken;
        _addressRanges = addressRanges;
        TotalCorrelatedSize = totalCorrelatedSize;
        foreach (var correlation in methods)
        {
            switch (correlation.Status)
            {
                case MethodCorrelationStatus.CorrelatedExact: ExactCount++; break;
                case MethodCorrelationStatus.CorrelatedAmbiguous: AmbiguousCount++; break;
                case MethodCorrelationStatus.CorrelatedByMstatOnly: MstatOnlyCount++; break;
                default: NotInImageCount++; break;
            }
        }
    }

    /// <summary>Every managed method's correlation, in source order.</summary>
    public IReadOnlyList<MethodCorrelation> Methods { get; }

    /// <summary>Methods that own native evidence outright.</summary>
    public int ExactCount { get; }

    /// <summary>Methods whose native evidence is shared with sibling overloads.</summary>
    public int AmbiguousCount { get; }

    /// <summary>Methods with mstat size evidence but no native symbol to disassemble.</summary>
    public int MstatOnlyCount { get; }

    /// <summary>Methods with no native evidence — trimmed, fully inlined, or bodiless.</summary>
    public int NotInImageCount { get; }

    /// <summary>
    /// The correlated native bytes, deduplicated: every evidence pool — owned or shared —
    /// contributes exactly once, with mstat sizes preferred over symbol sizes.
    /// </summary>
    public long TotalCorrelatedSize { get; }

    /// <summary>
    /// Finds a method's correlation by its assembly simple name and metadata token.
    /// Tokens collide across assemblies, so the composite key is required.
    /// </summary>
    /// <param name="assemblyName">The assembly simple name the method is defined in.</param>
    /// <param name="methodToken">The method's metadata token.</param>
    public MethodCorrelation? Find(string assemblyName, int methodToken) =>
        _byToken.TryGetValue((assemblyName, methodToken), out var correlation) ? correlation : null;

    /// <summary>
    /// Finds the correlation a native symbol belongs to, keyed by its virtual address.
    /// For a shared (overload) pool the first candidate is returned; its
    /// <see cref="MethodCorrelation.Status"/> reveals the ambiguity.
    /// </summary>
    /// <param name="symbol">The native symbol to look up.</param>
    public MethodCorrelation? FindByNativeSymbol(NativeSymbol symbol) =>
        FindByAddress(symbol.VirtualAddress);

    /// <summary>
    /// Finds the correlation whose native code covers <paramref name="virtualAddress"/> — an
    /// address anywhere inside a correlated symbol, not only its entry point — or null for
    /// uncorrelated (runtime/stub) code.
    /// </summary>
    /// <param name="virtualAddress">A virtual address, e.g. from a stack trace or a call target.</param>
    public MethodCorrelation? FindByAddress(ulong virtualAddress)
    {
        // Binary-search the disjoint, start-sorted ranges for the last one starting at or
        // before the address, then confirm the address falls within its end.
        var ranges = _addressRanges;
        int lo = 0, hi = ranges.Length - 1, found = -1;
        while (lo <= hi)
        {
            var mid = (int)(((uint)lo + (uint)hi) >> 1);
            if (ranges[mid].Start <= virtualAddress)
            {
                found = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        return found >= 0 && virtualAddress < ranges[found].End
            ? ranges[found].Correlation
            : null;
    }

    /// <summary>
    /// Builds the index from managed method sources, the image's native symbols, and its
    /// mstat report. Deliberately data-shaped — no analyzer required — so synthetic inputs
    /// exercise every join rule without real binaries.
    /// </summary>
    /// <param name="sources">The pre-ILC assemblies: the root managed input and any local references.</param>
    /// <param name="nativeSymbols">The AOT image's native symbols (empty when no symbol source exists).</param>
    /// <param name="mstat">The image's mstat report, or null when absent.</param>
    public static ManagedNativeIndex Build(
        IReadOnlyList<ManagedMethodSource> sources,
        IReadOnlyList<NativeSymbol> nativeSymbols,
        MstatData? mstat)
    {
        var groups = new List<Group>();
        var sourceLookups = new List<SourceLookup>(sources.Count);

        foreach (var source in sources)
        {
            var lookup = new SourceLookup(source.AssemblyName);
            foreach (var method in source.Methods)
            {
                var key = $"{method.DeclaringType}.{method.Name}";
                if (!lookup.GroupsByDisplayName.TryGetValue(key, out var group))
                {
                    group = new Group(source.AssemblyName);
                    lookup.GroupsByDisplayName[key] = group;
                    groups.Add(group);
                    lookup.GroupsByTypeAndName[$"{method.DeclaringType}::{method.Name}"] = group;
                }

                group.Methods.Add(method);
            }

            // The demangler joins sanitized symbol keys against these names — the same
            // engine the native symbol reader uses, fed complete companion metadata.
            var recovered = source.Methods
                .GroupBy(m => m.DeclaringType, StringComparer.Ordinal)
                .Select(g => new RecoveredType(
                    g.Key,
                    [.. g.Select(m => m.Name).Distinct(StringComparer.Ordinal)],
                    source.AssemblyName))
                .ToList();
            lookup.Demangler = new IlcNameDemangler(recovered);
            sourceLookups.Add(lookup);
        }

        foreach (var symbol in nativeSymbols)
        {
            if (symbol.Kind != NativeSymbolKind.Function) continue;

            foreach (var lookup in sourceLookups)
            {
                var result = lookup.Demangler!.Demangle(symbol.Name);
                if (result.ManagedName is null && symbol.Name.StartsWith('_'))
                    result = lookup.Demangler.Demangle(symbol.Name[1..]);
                if (result.ManagedName is null || result.Kind != NativeSymbolKind.Function)
                    continue;

                if (lookup.GroupsByDisplayName.TryGetValue(result.ManagedName, out var group))
                {
                    group.Symbols.Add(symbol);
                    break;
                }
            }
        }

        if (mstat is not null)
        {
            var lookupByAssembly = sourceLookups.ToDictionary(
                l => l.AssemblyName, l => l, StringComparer.Ordinal);
            foreach (var row in mstat.Methods)
            {
                if (!lookupByAssembly.TryGetValue(row.AssemblyName, out var lookup)) continue;

                var key = $"{StripTrailingInstantiation(row.DeclaringType)}::{StripTrailingInstantiation(row.Name)}";
                if (lookup.GroupsByTypeAndName.TryGetValue(key, out var group))
                    group.MstatRows.Add(row);
            }
        }

        var methods = new List<MethodCorrelation>();
        var byToken = new Dictionary<(string, int), MethodCorrelation>();
        var addressRanges = new List<(ulong Start, ulong End, MethodCorrelation Correlation)>();
        long totalSize = 0;

        foreach (var group in groups)
        {
            var hasSymbols = group.Symbols.Count > 0;
            var hasMstat = group.MstatRows.Count > 0;
            // mstat sizes are authoritative when present; symbol sizes may be
            // distance-derived. Never both — that would double-count the same bytes. Within
            // either evidence kind, dedupe (aliases at one VA, mstat rows sharing a node)
            // so the same bytes are never summed twice.
            var groupSize = hasMstat
                ? SumDistinctMstatSize(group.MstatRows)
                : SumDistinctSymbolSize(group.Symbols);
            var shared = group.Methods.Count > 1;
            if (hasSymbols || hasMstat) totalSize += groupSize;

            foreach (var method in group.Methods)
            {
                var status = (hasSymbols, hasMstat) switch
                {
                    (true, _) when !shared => MethodCorrelationStatus.CorrelatedExact,
                    (true, _) => MethodCorrelationStatus.CorrelatedAmbiguous,
                    (false, true) => MethodCorrelationStatus.CorrelatedByMstatOnly,
                    _ => MethodCorrelationStatus.NotInNativeImage,
                };

                var correlation = new MethodCorrelation(
                    group.AssemblyName, method, status, group.Symbols, group.MstatRows)
                {
                    NativeSize = shared ? 0 : groupSize,
                    SharedCandidateSize = shared && (hasSymbols || hasMstat) ? groupSize : 0,
                };
                methods.Add(correlation);
                byToken[(group.AssemblyName, method.Token)] = correlation;
            }

            if (hasSymbols)
            {
                var first = methods[^group.Methods.Count];
                var seenVa = new HashSet<ulong>();
                foreach (var symbol in group.Symbols)
                {
                    // Aliases at the same VA describe one range; keep the first.
                    if (!seenVa.Add(symbol.VirtualAddress)) continue;
                    var start = symbol.VirtualAddress;
                    var end = start + (ulong)Math.Max(symbol.Size, 1);
                    addressRanges.Add((start, end, first));
                }
            }
        }

        addressRanges.Sort(static (a, b) => a.Start.CompareTo(b.Start));
        return new ManagedNativeIndex(methods, byToken, [.. addressRanges], totalSize);
    }

    /// <summary>
    /// Strips one balanced trailing <c>&lt;…&gt;</c> instantiation group from an mstat display
    /// name. The opening bracket must not be the first character, which preserves
    /// compiler-generated metadata names like <c>&lt;Main&gt;$</c> (they never end with <c>&gt;</c>).
    /// </summary>
    internal static string StripTrailingInstantiation(string name)
    {
        if (name.Length == 0 || name[^1] != '>') return name;

        var depth = 0;
        for (var i = name.Length - 1; i > 0; i--)
        {
            if (name[i] == '>') depth++;
            else if (name[i] == '<' && --depth == 0)
                return name[..i];
        }

        return name;
    }

    /// <summary>
    /// Sums symbol sizes over distinct virtual addresses, so aliases the reader merged at one
    /// VA contribute their bytes exactly once.
    /// </summary>
    private static long SumDistinctSymbolSize(List<NativeSymbol> symbols)
    {
        if (symbols.Count == 1) return symbols[0].Size;

        var seen = new HashSet<ulong>(symbols.Count);
        long sum = 0;
        foreach (var symbol in symbols)
            if (seen.Add(symbol.VirtualAddress))
                sum += symbol.Size;
        return sum;
    }

    /// <summary>
    /// Sums mstat sizes over distinct dependency-graph node names, so rows that repeat a node
    /// count once. Rows without a node name (1.x reports) are each distinct and always counted.
    /// </summary>
    private static long SumDistinctMstatSize(List<MstatMethod> rows)
    {
        if (rows.Count == 1) return rows[0].Size;

        HashSet<string>? seen = null;
        long sum = 0;
        foreach (var row in rows)
        {
            if (row.NodeName is { } node)
            {
                seen ??= new HashSet<string>(StringComparer.Ordinal);
                if (!seen.Add(node)) continue;
            }

            sum += row.Size;
        }

        return sum;
    }

    private sealed class SourceLookup(string assemblyName)
    {
        public string AssemblyName { get; } = assemblyName;
        public Dictionary<string, Group> GroupsByDisplayName { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, Group> GroupsByTypeAndName { get; } = new(StringComparer.Ordinal);
        public IlcNameDemangler? Demangler { get; set; }
    }

    private sealed class Group(string assemblyName)
    {
        public string AssemblyName { get; } = assemblyName;
        public List<MethodDefInfo> Methods { get; } = [];
        public List<NativeSymbol> Symbols { get; } = [];
        public List<MstatMethod> MstatRows { get; } = [];
    }
}
