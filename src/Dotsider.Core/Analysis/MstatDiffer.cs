using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Compares two ILC size reports and explains where the bytes went: a hierarchical delta tree
/// (assembly → namespace → type → method, beside the binary's data categories), flat top
/// contributors, and per-assembly / per-namespace aggregate deltas. Entries are matched by the
/// build-stable identity keys of <see cref="MstatSizeIndex"/>, so overloads, folded
/// MethodTables, and owner-grouped frozen objects compare correctly across builds.
/// </summary>
public static class MstatDiffer
{
    /// <summary>
    /// Compares two decoded reports under a shared detail-section policy
    /// (<see cref="MstatSectionPolicy.ForPair"/>), so mixed format versions degrade to blob
    /// fidelity together and no byte is counted differently on the two sides.
    /// </summary>
    /// <param name="left">The baseline report. Use <see cref="MstatData.Empty"/> when there is none.</param>
    /// <param name="right">The report under comparison.</param>
    /// <returns>The size difference.</returns>
    public static MstatDiffResult Compare(MstatData left, MstatData right)
    {
        var policy = MstatSectionPolicy.ForPair(left, right);
        return Compare(MstatSizeIndex.Create(left, policy), MstatSizeIndex.Create(right, policy));
    }

    /// <summary>
    /// Compares two normalized indexes. Both must have been created under the same
    /// <see cref="MstatSectionPolicy"/> — otherwise the same bytes sit in different sections
    /// on the two sides and the comparison is meaningless.
    /// </summary>
    /// <param name="left">The baseline index.</param>
    /// <param name="right">The index under comparison.</param>
    /// <returns>The size difference.</returns>
    public static MstatDiffResult Compare(MstatSizeIndex left, MstatSizeIndex right)
    {
        if (left.Policy != right.Policy)
        {
            throw new ArgumentException(
                "Indexes were built under different section policies; create both with " +
                "MstatSectionPolicy.ForPair so the same bytes land in the same sections.",
                nameof(right));
        }

        var matches = MatchEntries(left, right);

        var counts = CountByKind(matches);
        var unchangedTotal = matches.Where(m => m.Diff == DiffKind.Unchanged).Sum(m => m.RightSize);
        var changed = matches.Where(m => m.Diff != DiffKind.Unchanged).ToList();

        var contributors = changed
            .OrderByDescending(m => Math.Abs(m.Delta))
            .ThenBy(m => m.Entry.FullPath, StringComparer.Ordinal)
            .Select(ToContributor)
            .ToList();

        var summary = new SizeDiffSummary(
            left.Total, right.Total, right.Total - left.Total, unchangedTotal, counts,
            left.Data.DeduplicatedMethods.Count, right.Data.DeduplicatedMethods.Count);

        return new MstatDiffResult(
            $"{left.Data.FormatMajorVersion}.{left.Data.FormatMinorVersion}",
            $"{right.Data.FormatMajorVersion}.{right.Data.FormatMinorVersion}",
            BuildTree(changed),
            summary,
            contributors,
            BuildAggregates(left.AssemblyTotals, right.AssemblyTotals),
            BuildAggregates(left.NamespaceTotals, right.NamespaceTotals));
    }

    /// <summary>An entry matched across the two sides; either side may be absent.</summary>
    private sealed record Match(
        MstatSizeEntry Entry,       // the right side when present, else the left — carries identity and display
        MstatSizeEntry? Left,
        MstatSizeEntry? Right)
    {
        public long LeftSize => Left?.Size ?? 0;
        public long RightSize => Right?.Size ?? 0;
        public long Delta => RightSize - LeftSize;
        public DiffKind Diff =>
            Left is null ? DiffKind.Added
            : Right is null ? DiffKind.Removed
            : LeftSize != RightSize ? DiffKind.Changed
            : DiffKind.Unchanged;
    }

    private static List<Match> MatchEntries(MstatSizeIndex left, MstatSizeIndex right)
    {
        var leftByKey = left.Entries.ToDictionary(e => e.Key, StringComparer.Ordinal);
        var rightByKey = right.Entries.ToDictionary(e => e.Key, StringComparer.Ordinal);

        var matches = new List<Match>(Math.Max(left.Entries.Count, right.Entries.Count));
        foreach (var l in left.Entries)
            matches.Add(new Match(rightByKey.GetValueOrDefault(l.Key) ?? l, l, rightByKey.GetValueOrDefault(l.Key)));
        foreach (var r in right.Entries)
        {
            if (!leftByKey.ContainsKey(r.Key))
                matches.Add(new Match(r, null, r));
        }

        return matches;
    }

    private static List<SizeDiffKindCounts> CountByKind(List<Match> matches)
    {
        var counts = new Dictionary<SizeNodeKind, (int Added, int Removed, int Grown, int Shrunk, int Unchanged)>();
        foreach (var m in matches)
        {
            var kind = KindOf(m.Entry.Section);
            var c = counts.GetValueOrDefault(kind);
            switch (m.Diff)
            {
                case DiffKind.Added: c.Added++; break;
                case DiffKind.Removed: c.Removed++; break;
                case DiffKind.Changed when m.Delta > 0: c.Grown++; break;
                case DiffKind.Changed: c.Shrunk++; break;
                default: c.Unchanged++; break;
            }

            counts[kind] = c;
        }

        return [.. counts
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => new SizeDiffKindCounts(
                kvp.Key, kvp.Value.Added, kvp.Value.Removed, kvp.Value.Grown, kvp.Value.Shrunk, kvp.Value.Unchanged))];
    }

    private static SizeDiffContributor ToContributor(Match m) => new(
        ContributorName(m.Entry),
        m.Entry.FullPath,
        KindOf(m.Entry.Section),
        m.Diff,
        m.LeftSize,
        m.RightSize,
        m.Delta,
        m.Entry.AssemblyName,
        m.Entry.Namespace,
        m.Left?.EntryCount ?? 0,
        m.Right?.EntryCount ?? 0,
        m.Left?.NodeNames ?? [],
        m.Right?.NodeNames ?? []);

    /// <summary>
    /// A contributor prints without its tree context, so the name carries what the tree would:
    /// MethodTables name their type, owned frozen objects name their owner.
    /// </summary>
    private static string ContributorName(MstatSizeEntry entry) => entry.Section switch
    {
        MstatSectionKind.MethodTable => $"{entry.TypeName} (MethodTable)",
        MstatSectionKind.FrozenObject when entry.TypeName != entry.LeafName =>
            $"{entry.LeafName} (owned by {entry.TypeName})",
        _ => entry.LeafName,
    };

    private static SizeNodeKind KindOf(MstatSectionKind section) => section switch
    {
        MstatSectionKind.Method => SizeNodeKind.Method,
        MstatSectionKind.MethodTable => SizeNodeKind.MethodTable,
        MstatSectionKind.Blob => SizeNodeKind.Blob,
        MstatSectionKind.FrozenObject => SizeNodeKind.FrozenObject,
        MstatSectionKind.RvaField => SizeNodeKind.RvaField,
        _ => SizeNodeKind.Resource,
    };

    private static SizeDiffNode BuildTree(List<Match> changed)
    {
        var roots = new List<SizeDiffNode>();
        roots.AddRange(BuildAssemblySubtrees(changed));

        AddCategory(roots, changed, MstatSectionKind.Blob, "Blobs");
        AddCategory(roots, changed, MstatSectionKind.FrozenObject, "Frozen Objects");
        AddCategory(roots, changed, MstatSectionKind.RvaField, "RVA Fields");
        AddCategory(roots, changed, MstatSectionKind.Resource, "Resources");

        return MakeInterior("Total", "", SizeNodeKind.Assembly, Order(roots));
    }

    private static List<SizeDiffNode> BuildAssemblySubtrees(List<Match> changed)
    {
        var result = new List<SizeDiffNode>();
        var byAssembly = changed
            .Where(m => m.Entry.Section is MstatSectionKind.Method or MstatSectionKind.MethodTable)
            .GroupBy(m => m.Entry.AssemblyName, StringComparer.Ordinal);

        foreach (var asmGroup in byAssembly)
        {
            var namespaceNodes = new List<SizeDiffNode>();
            foreach (var nsGroup in asmGroup.GroupBy(m => m.Entry.Namespace, StringComparer.Ordinal))
            {
                var nsDisplay = nsGroup.Key.Length > 0 ? nsGroup.Key : "(global)";
                var typeNodes = new List<SizeDiffNode>();
                foreach (var typeGroup in nsGroup.GroupBy(m => m.Entry.TypeName, StringComparer.Ordinal))
                {
                    var leaves = typeGroup.Select(ToLeaf).ToList();
                    typeNodes.Add(MakeInterior(
                        SizeAnalyzer.StripNamespace(typeGroup.Key, nsGroup.Key),
                        $"{asmGroup.Key}/{typeGroup.Key}",
                        SizeNodeKind.Type, Order(leaves)));
                }

                namespaceNodes.Add(MakeInterior(
                    nsDisplay, $"{asmGroup.Key}/{nsDisplay}", SizeNodeKind.Namespace, Order(typeNodes)));
            }

            result.Add(MakeInterior(
                asmGroup.Key, asmGroup.Key, SizeNodeKind.Assembly, Order(namespaceNodes)));
        }

        return result;
    }

    private static void AddCategory(
        List<SizeDiffNode> roots, List<Match> changed, MstatSectionKind section, string name)
    {
        var leaves = changed
            .Where(m => m.Entry.Section == section)
            .Select(ToLeaf)
            .ToList();
        if (leaves.Count > 0)
            roots.Add(MakeInterior(name, name, SizeNodeKind.Category, Order(leaves)));
    }

    private static SizeDiffNode ToLeaf(Match m)
    {
        var name = m.Entry.Section switch
        {
            MstatSectionKind.MethodTable => "MethodTable",
            MstatSectionKind.FrozenObject when m.Entry.TypeName != m.Entry.LeafName =>
                $"{m.Entry.LeafName} (owned by {m.Entry.TypeName})",
            _ => m.Entry.LeafName,
        };
        return new SizeDiffNode(
            name, m.Entry.FullPath, KindOf(m.Entry.Section), m.Diff,
            m.LeftSize, m.RightSize, m.Delta, [],
            m.Left?.EntryCount ?? 0, m.Right?.EntryCount ?? 0,
            m.Left?.NodeNames ?? [], m.Right?.NodeNames ?? []);
    }

    /// <summary>
    /// An interior node summarizes its children: one-sided subtrees stay added or removed,
    /// anything else is changed, and sizes are the sums over the changed entries beneath.
    /// </summary>
    private static SizeDiffNode MakeInterior(
        string name, string fullPath, SizeNodeKind kind, List<SizeDiffNode> children)
    {
        var diff =
            children.Count == 0 ? DiffKind.Unchanged
            : children.All(c => c.Diff == DiffKind.Added) ? DiffKind.Added
            : children.All(c => c.Diff == DiffKind.Removed) ? DiffKind.Removed
            : DiffKind.Changed;
        return new SizeDiffNode(
            name, fullPath, kind, diff,
            children.Sum(c => c.LeftSize), children.Sum(c => c.RightSize), children.Sum(c => c.Delta),
            children,
            children.Sum(c => c.LeftEntryCount), children.Sum(c => c.RightEntryCount),
            [], []);
    }

    private static List<SizeDiffNode> Order(List<SizeDiffNode> nodes) => [.. nodes
        .OrderByDescending(n => Math.Abs(n.Delta))
        .ThenBy(n => n.FullPath, StringComparer.Ordinal)];

    private static List<SizeDiffAggregate> BuildAggregates(
        IReadOnlyDictionary<string, long> left, IReadOnlyDictionary<string, long> right)
    {
        var names = new HashSet<string>(left.Keys, StringComparer.Ordinal);
        names.UnionWith(right.Keys);

        return [.. names
            .Select(name => new SizeDiffAggregate(
                name,
                left.GetValueOrDefault(name),
                right.GetValueOrDefault(name),
                right.GetValueOrDefault(name) - left.GetValueOrDefault(name)))
            .OrderByDescending(a => Math.Abs(a.Delta))
            .ThenByDescending(a => a.RightSize)
            .ThenBy(a => a.Name, StringComparer.Ordinal)];
    }
}
