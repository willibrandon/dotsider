using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis;

/// <summary>
/// The normalized view of an ILC size report that every size consumer shares: raw rows
/// aggregated under build-stable identity keys, one double-count policy for the 2.1+ detail
/// sections, owner-based attribution for frozen objects, and per-assembly / per-namespace byte
/// totals. <see cref="SizeAnalyzer"/> builds the Size Map from it, <see cref="MstatDiffer"/>
/// compares two of them, and budget evaluation reads its aggregates — so a total shown in one
/// place always equals the same total shown in another.
/// </summary>
public sealed class MstatSizeIndex
{
    /// <summary>
    /// The attribution bucket for bytes no assembly or namespace can honestly be charged for —
    /// frozen objects with no owning type, such as string literals. Scoped size budgets never
    /// draw from this bucket, but it stays visible in the aggregates so the bytes are never
    /// silently dropped.
    /// </summary>
    public const string UnattributedName = "(unattributed)";

    private MstatSizeIndex(
        MstatData data, MstatSectionPolicy policy, long total,
        IReadOnlyList<MstatSizeEntry> entries,
        IReadOnlyDictionary<string, long> assemblyTotals,
        IReadOnlyDictionary<string, long> namespaceTotals)
    {
        Data = data;
        Policy = policy;
        Total = total;
        Entries = entries;
        AssemblyTotals = assemblyTotals;
        NamespaceTotals = namespaceTotals;
    }

    /// <summary>The decoded report the index was built from.</summary>
    public MstatData Data { get; }

    /// <summary>The detail-section policy the index applied.</summary>
    public MstatSectionPolicy Policy { get; }

    /// <summary>The total attributable bytes — the same figure the Size Map reports for the build.</summary>
    public long Total { get; }

    /// <summary>Every normalized entry, in first-occurrence order per section.</summary>
    public IReadOnlyList<MstatSizeEntry> Entries { get; }

    /// <summary>
    /// Attributable bytes per assembly: methods, MethodTables, RVA fields, and resources by
    /// their defining assembly, frozen objects by their owning type's assembly (ownerless
    /// bytes land under <see cref="UnattributedName"/>). Blobs are global and excluded.
    /// </summary>
    public IReadOnlyDictionary<string, long> AssemblyTotals { get; }

    /// <summary>
    /// Attributable bytes per namespace, folded across assemblies: methods and MethodTables by
    /// their namespace, RVA fields by their declaring type's namespace, frozen objects by
    /// their owning type's namespace (ownerless bytes land under
    /// <see cref="UnattributedName"/>). Blobs and resources carry no namespace and are
    /// excluded. The global namespace keys as an empty string.
    /// </summary>
    public IReadOnlyDictionary<string, long> NamespaceTotals { get; }

    /// <summary>
    /// Builds the index for one report on its own, using <see cref="MstatSectionPolicy.ForData"/>.
    /// </summary>
    /// <param name="data">The decoded report.</param>
    /// <returns>The normalized index.</returns>
    public static MstatSizeIndex Create(MstatData data) => Create(data, MstatSectionPolicy.ForData(data));

    /// <summary>
    /// Builds the index under an explicit detail-section policy. Two indexes are comparable by
    /// <see cref="MstatDiffer"/> only when they share a policy — use
    /// <see cref="MstatSectionPolicy.ForPair"/> for a pair of reports.
    /// </summary>
    /// <param name="data">The decoded report.</param>
    /// <param name="policy">The detail-section policy to apply.</param>
    /// <returns>The normalized index.</returns>
    public static MstatSizeIndex Create(MstatData data, MstatSectionPolicy policy)
    {
        // Accumulate rows per identity key, preserving first-occurrence order so downstream
        // trees enumerate in report order like the pre-index SizeAnalyzer did.
        var entries = new Dictionary<string, Accumulator>(StringComparer.Ordinal);
        var order = new List<string>();

        void Add(
            string key, MstatSectionKind section, string assembly, string ns,
            string typeName, string leafName, string displayName, string fullPath,
            long size, string? nodeName)
        {
            if (!entries.TryGetValue(key, out var acc))
            {
                acc = new Accumulator(section, assembly, ns, typeName, leafName, displayName, fullPath);
                entries[key] = acc;
                order.Add(key);
            }

            acc.Size += size;
            acc.EntryCount++;
            if (nodeName is not null) acc.NodeNames.Add(nodeName);
        }

        foreach (var m in data.Methods)
        {
            var leaf = $"{m.Name}{m.Signature}";
            Add($"M|{m.AssemblyName}|{m.DeclaringType}|{leaf}",
                MstatSectionKind.Method, m.AssemblyName, m.Namespace, m.DeclaringType, leaf, m.Name,
                $"{m.AssemblyName}/{m.DeclaringType}::{leaf}",
                (long)m.Size + m.GcInfoSize + m.EhInfoSize, m.NodeName);
        }

        foreach (var t in data.Types)
        {
            Add($"T|{t.AssemblyName}|{t.Name}",
                MstatSectionKind.MethodTable, t.AssemblyName, t.Namespace, t.Name, "MethodTable", t.Name,
                $"{t.AssemblyName}/{t.Name}::MethodTable",
                t.Size, t.NodeName);
        }

        var excludedBlobs = policy.ExcludedBlobNames();
        foreach (var b in data.Blobs)
        {
            if (excludedBlobs.Contains(b.Name)) continue;
            Add($"B|{b.Name}",
                MstatSectionKind.Blob, "", "", "", b.Name, b.Name,
                $"Blobs/{b.Name}",
                b.Size, null);
        }

        if (policy.UseFrozenObjects)
        {
            foreach (var f in data.FrozenObjects)
            {
                // Attribution follows the owner — the code that caused the bytes — never the
                // assembly defining the object's type; string literals would all land on the
                // core library otherwise. Ownerless objects are honestly unattributed.
                var owned = !string.IsNullOrEmpty(f.OwningAssemblyName);
                var assembly = owned ? f.OwningAssemblyName! : UnattributedName;
                var ns = owned ? f.OwningNamespace ?? "" : UnattributedName;
                var owner = f.OwningType ?? "(literals)";
                Add($"F|{assembly}|{f.TypeName}|{f.OwningType ?? ""}",
                    MstatSectionKind.FrozenObject, assembly, ns,
                    f.OwningType ?? f.TypeName, f.TypeName, f.TypeName,
                    $"Frozen Objects/{assembly}/{owner}/{f.TypeName}",
                    f.Size, f.NodeName);
            }
        }

        if (policy.UseRvaFields)
        {
            foreach (var f in data.RvaFields)
            {
                // Name is the reader's own "{Type}::{Field}" composition; splitting at its
                // separator recovers the structure without guessing at display syntax.
                var separator = f.Name.IndexOf("::", StringComparison.Ordinal);
                var typeName = separator > 0 ? f.Name[..separator] : "";
                var fieldName = separator > 0 ? f.Name[(separator + 2)..] : f.Name;
                Add($"V|{f.AssemblyName}|{f.Name}",
                    MstatSectionKind.RvaField, f.AssemblyName, f.Namespace, typeName, fieldName, f.Name,
                    $"RVA Fields/{f.AssemblyName}/{f.Name}",
                    f.Size, f.NodeName);
            }
        }

        if (policy.UseManifestResources)
        {
            foreach (var r in data.ManifestResources)
            {
                Add($"R|{r.AssemblyName}|{r.Name}",
                    MstatSectionKind.Resource, r.AssemblyName, "", "", r.Name, r.Name,
                    $"Resources/{r.AssemblyName}/{r.Name}",
                    r.Size, null);
            }
        }

        var list = new List<MstatSizeEntry>(order.Count);
        long total = 0;
        var assemblyTotals = new Dictionary<string, long>(StringComparer.Ordinal);
        var namespaceTotals = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var key in order)
        {
            var acc = entries[key];
            list.Add(new MstatSizeEntry(
                acc.Section, key, acc.AssemblyName, acc.Namespace, acc.TypeName,
                acc.LeafName, acc.DisplayName, acc.FullPath,
                acc.Size, acc.EntryCount, acc.NodeNames));

            total += acc.Size;
            if (acc.Section != MstatSectionKind.Blob)
                assemblyTotals[acc.AssemblyName] = assemblyTotals.GetValueOrDefault(acc.AssemblyName) + acc.Size;
            if (acc.Section is MstatSectionKind.Method or MstatSectionKind.MethodTable
                or MstatSectionKind.RvaField or MstatSectionKind.FrozenObject)
            {
                namespaceTotals[acc.Namespace] = namespaceTotals.GetValueOrDefault(acc.Namespace) + acc.Size;
            }
        }

        return new MstatSizeIndex(data, policy, total, list, assemblyTotals, namespaceTotals);
    }

    private sealed class Accumulator(
        MstatSectionKind section, string assemblyName, string ns,
        string typeName, string leafName, string displayName, string fullPath)
    {
        public MstatSectionKind Section { get; } = section;
        public string AssemblyName { get; } = assemblyName;
        public string Namespace { get; } = ns;
        public string TypeName { get; } = typeName;
        public string LeafName { get; } = leafName;
        public string DisplayName { get; } = displayName;
        public string FullPath { get; } = fullPath;
        public long Size { get; set; }
        public int EntryCount { get; set; }
        public List<string> NodeNames { get; } = [];
    }
}
