using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="MstatDiffer"/> against the real V1/V2 mstat pair: the V2 sample is
/// the same application rebuilt with deliberate deltas — an added namespace, a grown
/// overload, a removed property accessor, and an embedded resource. Native sizes vary by
/// platform and toolchain, so every size assertion is sign/kind/order-based, never exact
/// bytes.
/// </summary>
[Collection("SampleAssemblies")]
public class MstatDifferTests(SampleAssemblyFixture samples)
{
    private (MstatData V1, MstatData V2) ReadPair()
    {
        Assert.SkipWhen(samples.NativeAotConsoleMstat is null, "V1 mstat sidecar was not produced");
        Assert.SkipWhen(samples.NativeAotConsoleV2Mstat is null, "V2 mstat sidecar was not produced");
        var v1 = MstatReader.Read(samples.NativeAotConsoleMstat!);
        var v2 = MstatReader.Read(samples.NativeAotConsoleV2Mstat!);
        Assert.NotNull(v1);
        Assert.NotNull(v2);
        return (v1, v2);
    }

    /// <summary>
    /// Verifies the namespace that exists only in V2 diffs as a fully added subtree under the
    /// app's assembly, with zero baseline bytes.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Compare_V1V2_AddedNamespaceDetected()
    {
        var (v1, v2) = ReadPair();

        var diff = MstatDiffer.Compare(v1, v2);

        var assembly = Assert.Single(diff.Root.Children, n => n.Name == "NativeAotConsole");
        var telemetry = Assert.Single(assembly.Children, n => n.Name == "NativeAotConsole.Telemetry");
        Assert.Equal(DiffKind.Added, telemetry.Diff);
        Assert.Equal(0, telemetry.LeftSize);
        Assert.True(telemetry.Delta > 0);
        Assert.Equal(telemetry.RightSize, telemetry.Delta);
    }

    /// <summary>
    /// Verifies the property accessor removed in V2 diffs as a removed method with a negative
    /// delta.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Compare_V1V2_RemovedMethodDetected()
    {
        var (v1, v2) = ReadPair();

        var diff = MstatDiffer.Compare(v1, v2);

        var removed = Assert.Single(diff.Contributors, c =>
            c.Name == "get_Name()" && c.AssemblyName == "NativeAotConsole");
        Assert.Equal(DiffKind.Removed, removed.Diff);
        Assert.True(removed.Delta < 0);
        Assert.Equal(0, removed.RightSize);
    }

    /// <summary>
    /// Verifies the overload grown in V2 diffs as changed with a positive delta — sign only,
    /// never exact bytes.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Compare_V1V2_GrownMethodDetected()
    {
        var (v1, v2) = ReadPair();

        var diff = MstatDiffer.Compare(v1, v2);

        var grown = Assert.Single(diff.Contributors, c =>
            c.Name == "Greet(string)" && c.AssemblyName == "NativeAotConsole");
        Assert.Equal(DiffKind.Changed, grown.Diff);
        Assert.True(grown.Delta > 0);
        Assert.True(grown.LeftSize > 0);
    }

    /// <summary>
    /// Verifies signature-keyed identity: V2 grows Greet(string) and leaves Greet(int)
    /// untouched, so the string overload appears among the changed entries and the int
    /// overload does not — the two are never merged into one row.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Compare_V1V2_OverloadsTrackedSeparately()
    {
        var (v1, v2) = ReadPair();

        var diff = MstatDiffer.Compare(v1, v2);

        Assert.Contains(diff.Contributors, c =>
            c.Name == "Greet(string)" && c.AssemblyName == "NativeAotConsole");
        Assert.DoesNotContain(diff.Contributors, c =>
            c.Name == "Greet(int)" && c.AssemblyName == "NativeAotConsole");
    }

    /// <summary>
    /// Verifies the manifest resource embedded only in V2 produces an added Resources
    /// category.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Compare_V1V2_ResourceCategoryAdded()
    {
        var (v1, v2) = ReadPair();
        Assert.SkipWhen(v2.ManifestResources.Count == 0, "V2 fixture embeds no resources");

        var diff = MstatDiffer.Compare(v1, v2);

        var resources = Assert.Single(diff.Root.Children, n => n.Name == "Resources");
        Assert.Equal(DiffKind.Added, resources.Diff);
        Assert.True(resources.Delta > 0);
        Assert.Contains(diff.Contributors, c =>
            c.Kind == SizeNodeKind.Resource && c.Name == "NativeAotConsole.Payload.txt");
    }

    /// <summary>
    /// Verifies the recursive tree invariant: every interior node's sizes are exactly the
    /// sums of its children.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Compare_V1V2_NodeSumsEqualChildSums()
    {
        var (v1, v2) = ReadPair();

        var diff = MstatDiffer.Compare(v1, v2);

        static void AssertSums(SizeDiffNode node)
        {
            if (node.Children.Count == 0) return;
            Assert.Equal(node.Children.Sum(c => c.LeftSize), node.LeftSize);
            Assert.Equal(node.Children.Sum(c => c.RightSize), node.RightSize);
            Assert.Equal(node.Children.Sum(c => c.Delta), node.Delta);
            foreach (var child in node.Children)
                AssertSums(child);
        }

        AssertSums(diff.Root);
    }

    /// <summary>
    /// Verifies contributors are ordered by absolute delta, largest first, and the added
    /// Telemetry members are among them.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Compare_V1V2_ContributorsOrderedByAbsoluteDelta()
    {
        var (v1, v2) = ReadPair();

        var diff = MstatDiffer.Compare(v1, v2);

        for (var i = 1; i < diff.Contributors.Count; i++)
        {
            Assert.True(
                Math.Abs(diff.Contributors[i - 1].Delta) >= Math.Abs(diff.Contributors[i].Delta),
                $"contributors out of order at {i}");
        }

        Assert.Contains(diff.Contributors, c => c.Namespace == "NativeAotConsole.Telemetry");
    }

    /// <summary>
    /// Verifies blobs match by name across builds: the Metadata region exists in both, so it
    /// must appear as one changed-or-unchanged entry, never as an added/removed pair.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Compare_V1V2_BlobsMatchedByName()
    {
        var (v1, v2) = ReadPair();

        var diff = MstatDiffer.Compare(v1, v2);

        var metadata = diff.Contributors.Where(c => c.FullPath == "Blobs/Metadata").ToList();
        Assert.SkipWhen(metadata.Count == 0, "Metadata blob byte-identical across the builds");
        var entry = Assert.Single(metadata);
        Assert.Equal(DiffKind.Changed, entry.Diff);
        Assert.True(entry.LeftSize > 0);
        Assert.True(entry.RightSize > 0);
    }

    /// <summary>
    /// Verifies a self-diff is empty: no changed entries, zero delta, and the unchanged mass
    /// equals the build total.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Compare_SelfDiff_AllUnchangedZeroDelta()
    {
        Assert.SkipWhen(samples.NativeAotConsoleMstat is null, "V1 mstat sidecar was not produced");
        var v1 = MstatReader.Read(samples.NativeAotConsoleMstat!);
        Assert.NotNull(v1);

        var diff = MstatDiffer.Compare(v1, v1);

        Assert.Empty(diff.Root.Children);
        Assert.Empty(diff.Contributors);
        Assert.Equal(0, diff.Summary.Delta);
        Assert.Equal(diff.Summary.LeftTotal, diff.Summary.RightTotal);
        Assert.Equal(diff.Summary.LeftTotal, diff.Summary.UnchangedTotal);
        Assert.All(diff.Summary.Counts, c =>
        {
            Assert.Equal(0, c.Added);
            Assert.Equal(0, c.Removed);
            Assert.Equal(0, c.Grown);
            Assert.Equal(0, c.Shrunk);
        });
    }

    /// <summary>
    /// Verifies namespace aggregates carry the added namespace and fold across assemblies:
    /// the System namespace has rows in more than one assembly, and its aggregate must be the
    /// sum over all of them, recomputed here independently.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Compare_V1V2_NamespaceDeltasFoldAcrossAssemblies()
    {
        var (v1, v2) = ReadPair();

        var diff = MstatDiffer.Compare(v1, v2);

        var telemetry = Assert.Single(diff.NamespaceDeltas, a => a.Name == "NativeAotConsole.Telemetry");
        Assert.Equal(0, telemetry.LeftSize);
        Assert.True(telemetry.Delta > 0);

        var policy = MstatSectionPolicy.ForPair(v1, v2);
        var rightIndex = MstatSizeIndex.Create(v2, policy);
        var assembliesWithSystem = rightIndex.Entries
            .Where(e => e.Namespace == "System"
                && e.Section is MstatSectionKind.Method or MstatSectionKind.MethodTable)
            .Select(e => e.AssemblyName)
            .Distinct()
            .Count();
        Assert.SkipWhen(assembliesWithSystem < 2, "System namespace spans fewer than two assemblies");

        var system = Assert.Single(diff.NamespaceDeltas, a => a.Name == "System");
        Assert.Equal(rightIndex.NamespaceTotals["System"], system.RightSize);
    }

    /// <summary>
    /// Verifies aggregated entries are marked: the frozen string-literal bucket folds many
    /// rows and must say so through its entry counts.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Compare_V1V2_AggregatedEntriesCarryCounts()
    {
        var (v1, v2) = ReadPair();
        Assert.SkipWhen(
            v1.FrozenObjects.Count(f => f.OwningType is null) < 2,
            "fixture froze fewer than two string literals");

        var diff = MstatDiffer.Compare(v1, v2);

        var literals = diff.Contributors.FirstOrDefault(c =>
            c.Kind == SizeNodeKind.FrozenObject
            && c.AssemblyName == MstatSizeIndex.UnattributedName);
        Assert.SkipWhen(literals is null, "frozen literals byte-identical across the builds");
        Assert.True(literals!.LeftEntryCount > 1);
        Assert.True(literals.RightEntryCount > 1);
        Assert.True(literals.RightNodeNames.Count > 1);
    }

    /// <summary>
    /// Verifies an added contributor's node names join the V2 dependency graph — the "why did
    /// this appear" answer resolves against real labels.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Compare_V1V2_AddedContributorNodeNamesJoinRightDgml()
    {
        var (v1, v2) = ReadPair();
        Assert.SkipWhen(samples.NativeAotConsoleV2Dgml is null, "V2 DGML sidecar was not produced");

        var diff = MstatDiffer.Compare(v1, v2);
        var dgml = DgmlReader.Read(samples.NativeAotConsoleV2Dgml!);
        Assert.NotNull(dgml);

        var added = diff.Contributors.First(c =>
            c.Diff == DiffKind.Added
            && c.Namespace == "NativeAotConsole.Telemetry"
            && c.Kind == SizeNodeKind.Method
            && c.RightNodeNames.Count > 0);
        Assert.Contains(added.RightNodeNames, name => dgml.FindNodeByLabel(name) is not null);
    }

    /// <summary>
    /// Verifies the pre-node-name degradation path with a byte-patched copy of the real V1
    /// report: renaming the <c>.names</c> PE section makes every node-name lookup miss —
    /// the observable shape of a 1.x report, which the .NET 7 toolchain would be needed to
    /// produce for real. Entries still match by their name-based keys, so a self-comparison
    /// against the unpatched report stays empty.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Compare_MstatWithoutNamesSection_MatchesWithEmptyNodeNames()
    {
        Assert.SkipWhen(samples.NativeAotConsoleMstat is null, "V1 mstat sidecar was not produced");

        var patched = Path.Combine(Path.GetTempPath(), $"dotsider-nonames-{Guid.NewGuid():N}.mstat");
        try
        {
            var bytes = File.ReadAllBytes(samples.NativeAotConsoleMstat!);
            var sectionName = ".names\0\0"u8.ToArray();
            var offset = FindBytes(bytes, sectionName);
            Assert.SkipWhen(offset < 0, "report carries no .names section");
            bytes[offset + 1] = (byte)'x';
            File.WriteAllBytes(patched, bytes);

            var noNames = MstatReader.Read(patched);
            var original = MstatReader.Read(samples.NativeAotConsoleMstat!);
            Assert.NotNull(noNames);
            Assert.NotNull(original);
            Assert.All(noNames.Methods, m => Assert.Null(m.NodeName));

            var diff = MstatDiffer.Compare(noNames, original);
            Assert.Empty(diff.Root.Children);
            Assert.Equal(0, diff.Summary.Delta);
        }
        finally
        {
            File.Delete(patched);
        }
    }

    private static int FindBytes(byte[] haystack, byte[] needle)
    {
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }

            if (match) return i;
        }

        return -1;
    }
}
