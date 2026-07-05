using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Queryable view of a ReadyToRun image's precompiled methods: managed-method lookup by owning
/// assembly identity and token, and reverse lookup by native address over the methods' disjoint
/// code ranges. The token is qualified by assembly name because a composite spans several
/// assemblies whose tokens collide. Built once, every lookup a dictionary or binary-search hit.
/// </summary>
public sealed class ReadyToRunIndex
{
    private readonly Dictionary<(string Assembly, int Token), ReadyToRunMethodEntry> _byToken;
    private readonly (ulong Start, ulong End, ReadyToRunMethodEntry Entry)[] _ranges;

    private ReadyToRunIndex(
        IReadOnlyList<ReadyToRunMethodEntry> methods,
        Dictionary<(string, int), ReadyToRunMethodEntry> byToken,
        (ulong, ulong, ReadyToRunMethodEntry)[] ranges,
        long totalCodeSize)
    {
        Methods = methods;
        _byToken = byToken;
        _ranges = ranges;
        TotalCodeSize = totalCodeSize;
        foreach (var m in methods)
            if (m.IsGenericInstantiation)
                InstantiationCount++;
    }

    /// <summary>Every precompiled method entry (base methods and generic instantiations).</summary>
    public IReadOnlyList<ReadyToRunMethodEntry> Methods { get; }

    /// <summary>The number of generic-instantiation entries.</summary>
    public int InstantiationCount { get; }

    /// <summary>The total precompiled native code size across all methods.</summary>
    public long TotalCodeSize { get; }

    /// <summary>
    /// Finds a method's primary (non-generic) entry by owning assembly name and token, or the
    /// first entry when only instantiations exist.
    /// </summary>
    /// <param name="assemblyName">The owning assembly's simple name.</param>
    /// <param name="token">The method's metadata token.</param>
    public ReadyToRunMethodEntry? Find(string assemblyName, int token) =>
        _byToken.TryGetValue((assemblyName, token), out var entry) ? entry : null;

    /// <summary>Every entry for a token — the base method plus any generic instantiations.</summary>
    /// <param name="assemblyName">The owning assembly's simple name.</param>
    /// <param name="token">The method's metadata token.</param>
    public IReadOnlyList<ReadyToRunMethodEntry> FindAll(string assemblyName, int token)
    {
        var result = new List<ReadyToRunMethodEntry>();
        foreach (var m in Methods)
            if (m.Token == token && string.Equals(m.AssemblyName, assemblyName, StringComparison.Ordinal))
                result.Add(m);
        return result;
    }

    /// <summary>
    /// Finds the method whose native code covers <paramref name="virtualAddress"/> — an address
    /// anywhere inside any of its ranges — or null for uncorrelated (helper/stub) code.
    /// </summary>
    /// <param name="virtualAddress">A virtual address, e.g. a call target.</param>
    public ReadyToRunMethodEntry? FindByAddress(ulong virtualAddress)
    {
        var ranges = _ranges;
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

        return found >= 0 && virtualAddress < ranges[found].End ? ranges[found].Entry : null;
    }

    /// <summary>Builds the index from a ReadyToRun image's method entries.</summary>
    /// <param name="methods">The precompiled method entries.</param>
    public static ReadyToRunIndex Build(IReadOnlyList<ReadyToRunMethodEntry> methods)
    {
        var byToken = new Dictionary<(string, int), ReadyToRunMethodEntry>();
        var ranges = new List<(ulong, ulong, ReadyToRunMethodEntry)>();
        long total = 0;

        foreach (var method in methods)
        {
            // The base (non-generic) entry wins the token key; an instantiation only fills a gap.
            var key = (method.AssemblyName, method.Token);
            if (!byToken.TryGetValue(key, out var existing) || (existing.IsGenericInstantiation && !method.IsGenericInstantiation))
                byToken[key] = method;

            total += method.TotalSize;
            foreach (var range in method.CodeRanges)
                if (range.Size > 0)
                    ranges.Add((range.VirtualAddress, range.VirtualAddress + (ulong)range.Size, method));
        }

        ranges.Sort(static (a, b) => a.Item1.CompareTo(b.Item1));
        return new ReadyToRunIndex(methods, byToken, [.. ranges], total);
    }
}
