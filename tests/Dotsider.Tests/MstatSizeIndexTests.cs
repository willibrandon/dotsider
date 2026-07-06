using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="MstatSizeIndex"/> — the shared normalization layer — against the real
/// size reports published next to the NativeAOT samples. The anti-drift property matters
/// most: the index total must equal the Size Map total for the same build.
/// </summary>
[Collection("SampleAssemblies")]
public class MstatSizeIndexTests(SampleAssemblyFixture samples)
{
    private MstatData ReadV1()
    {
        Assert.SkipWhen(samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");
        var data = MstatReader.Read(samples.NativeAotConsoleMstat!);
        Assert.NotNull(data);
        return data;
    }

    /// <summary>
    /// Verifies the index total equals the size tree total built from the same binary — the
    /// guarantee that a figure shown by <c>analyze --size</c> equals the same figure in a
    /// size diff.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Create_FixtureMstat_TotalMatchesSizeAnalyzer()
    {
        Assert.SkipWhen(samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "AOT binary was not produced");

        var index = MstatSizeIndex.Create(ReadV1());
        using var analyzer = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);
        var tree = SizeAnalyzer.BuildSizeTree(analyzer);

        Assert.Equal(tree.Size, index.Total);
    }

    /// <summary>
    /// Verifies overloads stay distinct: the fixture's Greeter has Greet(string) and
    /// Greet(int), which share a display name but never a key.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Create_OverloadsKeyedSeparately()
    {
        var index = MstatSizeIndex.Create(ReadV1());

        var greets = index.Entries
            .Where(e => e.Section == MstatSectionKind.Method
                && e.AssemblyName == "NativeAotConsole"
                && e.DisplayName == "Greet")
            .ToList();

        Assert.Equal(2, greets.Count);
        Assert.Equal(2, greets.Select(e => e.Key).Distinct().Count());
        Assert.Contains(greets, e => e.LeafName.Contains("string", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(greets, e => e.LeafName.Contains("int", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Verifies frozen string literals aggregate into one entry whose row count and node-name
    /// list expose the aggregation instead of hiding it.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Create_FrozenObjectsAggregatedByTypeAndOwner()
    {
        var data = ReadV1();
        var index = MstatSizeIndex.Create(data);

        var literalRows = data.FrozenObjects
            .Where(f => f.TypeName == "System.String" && f.OwningType is null)
            .ToList();
        Assert.SkipWhen(literalRows.Count < 2, "fixture froze fewer than two string literals");

        var entry = Assert.Single(index.Entries, e =>
            e.Section == MstatSectionKind.FrozenObject
            && e.LeafName == "System.String"
            && e.AssemblyName == MstatSizeIndex.UnattributedName);
        Assert.Equal(literalRows.Count, entry.EntryCount);
        Assert.Equal(literalRows.Sum(f => (long)f.Size), entry.Size);
        Assert.Equal(literalRows.Count(f => f.NodeName is not null), entry.NodeNames.Count);
    }

    /// <summary>
    /// Verifies ownerless frozen objects are never charged to the assembly defining their
    /// type: a string literal's bytes belong to whoever wrote the literal, which the report
    /// cannot say — so they land in the explicit unattributed bucket, and the bytes are
    /// conserved in the aggregates rather than dropped.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Create_FrozenStringLiterals_GoToUnattributedBucket()
    {
        var data = ReadV1();
        var index = MstatSizeIndex.Create(data);

        var literalBytes = data.FrozenObjects
            .Where(f => f.OwningType is null)
            .Sum(f => (long)f.Size);
        Assert.SkipWhen(literalBytes == 0, "fixture froze no ownerless objects");

        Assert.True(index.AssemblyTotals.ContainsKey(MstatSizeIndex.UnattributedName));
        Assert.True(index.AssemblyTotals[MstatSizeIndex.UnattributedName] >= literalBytes);
        Assert.True(index.NamespaceTotals.ContainsKey(MstatSizeIndex.UnattributedName));

        // CoreLib's own total must not include the literal bytes: methods + MethodTables +
        // owned frozen + RVA + resources attributed to it, recomputed independently.
        var expectedCoreLib =
            index.Entries
                .Where(e => e.AssemblyName == "System.Private.CoreLib"
                    && e.Section != MstatSectionKind.Blob)
                .Sum(e => e.Size);
        Assert.Equal(expectedCoreLib, index.AssemblyTotals["System.Private.CoreLib"]);
    }

    /// <summary>
    /// Verifies owned frozen objects (serialized statics) carry their owner's attribution.
    /// The console fixture typically has none, in which case this skips honestly.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Create_OwnedFrozenObjects_AttributedToOwnerAssembly()
    {
        var data = ReadV1();
        var owned = data.FrozenObjects.Where(f => f.OwningType is not null).ToList();
        Assert.SkipWhen(owned.Count == 0, "fixture has no owned frozen objects");

        var index = MstatSizeIndex.Create(data);
        Assert.All(
            index.Entries.Where(e => e.Section == MstatSectionKind.FrozenObject
                && e.TypeName != e.LeafName),
            e => Assert.NotEqual(MstatSizeIndex.UnattributedName, e.AssemblyName));
    }

    /// <summary>
    /// Verifies entries carry the structured hierarchy fields — a consumer places every entry
    /// in an assembly → namespace → type → leaf tree without parsing display strings.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Create_EntriesCarryStructuredHierarchy()
    {
        var index = MstatSizeIndex.Create(ReadV1());

        Assert.All(
            index.Entries.Where(e => e.Section == MstatSectionKind.Method),
            e =>
            {
                Assert.False(string.IsNullOrEmpty(e.TypeName));
                Assert.False(string.IsNullOrEmpty(e.LeafName));
            });
        Assert.All(
            index.Entries.Where(e => e.Section == MstatSectionKind.MethodTable),
            e => Assert.Equal("MethodTable", e.LeafName));
        Assert.All(
            index.Entries.Where(e => e.Section == MstatSectionKind.RvaField),
            e =>
            {
                Assert.False(string.IsNullOrEmpty(e.TypeName));
                Assert.False(string.IsNullOrEmpty(e.LeafName));
                Assert.DoesNotContain("::", e.LeafName);
            });
        Assert.All(
            index.Entries.Where(e => e.Section is MstatSectionKind.Blob or MstatSectionKind.Resource),
            e => Assert.Equal("", e.TypeName));
    }

    /// <summary>
    /// Verifies the shared double-count rule: with 2.1+ detail sections populated, the
    /// back-compat blob buckets are excluded from the entries so no byte counts twice.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Create_DetailSectionsExcludeBlobBuckets()
    {
        var data = ReadV1();
        Assert.SkipWhen(data.FrozenObjects.Count == 0, "fixture has no frozen objects");

        var index = MstatSizeIndex.Create(data);

        Assert.DoesNotContain(index.Entries, e => e.Key == "B|ArrayOfFrozenObjects");
        Assert.DoesNotContain(index.Entries, e => e.Key == "B|FieldRvaData");
        Assert.Contains(index.Entries, e => e.Section == MstatSectionKind.FrozenObject);
        Assert.Contains(index.Entries, e => e.Section == MstatSectionKind.RvaField);
    }

    /// <summary>
    /// Verifies namespace totals cover RVA fields and owned frozen objects alongside methods
    /// and MethodTables — a namespace budget measures all bytes the namespace put in the
    /// image, recomputed here independently of the index's own arithmetic.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Create_NamespaceTotalsIncludeRvaAndOwnedFrozen()
    {
        var index = MstatSizeIndex.Create(ReadV1());

        var expected = index.Entries
            .Where(e => e.Section is MstatSectionKind.Method or MstatSectionKind.MethodTable
                or MstatSectionKind.RvaField or MstatSectionKind.FrozenObject)
            .GroupBy(e => e.Namespace, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Size), StringComparer.Ordinal);

        Assert.Equal(expected.Count, index.NamespaceTotals.Count);
        foreach (var (ns, total) in expected)
            Assert.Equal(total, index.NamespaceTotals[ns]);
    }

    /// <summary>
    /// Verifies resources never contribute to namespace totals — they carry no namespace, and
    /// inventing one would corrupt namespace budgets.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Create_ResourcesExcludedFromNamespaceTotals()
    {
        Assert.SkipWhen(samples.NativeAotConsoleV2Mstat is null, "V2 mstat sidecar was not produced");
        var data = MstatReader.Read(samples.NativeAotConsoleV2Mstat!);
        Assert.NotNull(data);
        Assert.SkipWhen(data.ManifestResources.Count == 0, "V2 fixture embeds no resources");

        var index = MstatSizeIndex.Create(data);

        var resource = Assert.Single(
            index.Entries, e => e.Section == MstatSectionKind.Resource);
        Assert.Equal("", resource.Namespace);
        var namespaceSum = index.NamespaceTotals.Values.Sum();
        var attributableSum = index.Entries
            .Where(e => e.Section is MstatSectionKind.Method or MstatSectionKind.MethodTable
                or MstatSectionKind.RvaField or MstatSectionKind.FrozenObject)
            .Sum(e => e.Size);
        Assert.Equal(attributableSum, namespaceSum);
    }
}
