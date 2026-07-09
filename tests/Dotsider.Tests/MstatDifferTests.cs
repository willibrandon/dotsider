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
[TestClass]
public class MstatDifferTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private static (MstatData V1, MstatData V2) ReadPair()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "V1 mstat sidecar was not produced");
        TestSkip.When(Samples.NativeAotConsoleV2Mstat is null, "V2 mstat sidecar was not produced");
        var v1 = MstatReader.Read(Samples.NativeAotConsoleMstat!);
        var v2 = MstatReader.Read(Samples.NativeAotConsoleV2Mstat!);
        Assert.IsNotNull(v1);
        Assert.IsNotNull(v2);
        return (v1, v2);
    }

    /// <summary>
    /// Verifies the namespace that exists only in V2 diffs as a fully added subtree under the
    /// app's assembly, with zero baseline bytes.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Compare_V1V2_AddedNamespaceDetected()
    {
        var (v1, v2) = ReadPair();

        var diff = MstatDiffer.Compare(v1, v2);

        var assembly = Assert.ContainsSingle(n => n.Name == "NativeAotConsole", diff.Root.Children);
        var telemetry = Assert.ContainsSingle(n => n.Name == "NativeAotConsole.Telemetry", assembly.Children);
        Assert.AreEqual(DiffKind.Added, telemetry.Diff);
        Assert.AreEqual(0, telemetry.LeftSize);
        Assert.IsGreaterThan(0, telemetry.Delta);
        Assert.AreEqual(telemetry.RightSize, telemetry.Delta);
    }

    /// <summary>
    /// Verifies the property accessor removed in V2 diffs as a removed method with a negative
    /// delta.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Compare_V1V2_RemovedMethodDetected()
    {
        var (v1, v2) = ReadPair();

        var diff = MstatDiffer.Compare(v1, v2);

        var removed = Assert.ContainsSingle(c =>
            c.Name == "get_Name()" && c.AssemblyName == "NativeAotConsole", diff.Contributors);
        Assert.AreEqual(DiffKind.Removed, removed.Diff);
        Assert.IsLessThan(0, removed.Delta);
        Assert.AreEqual(0, removed.RightSize);
    }

    /// <summary>
    /// Verifies the overload grown in V2 diffs as changed with a positive delta — sign only,
    /// never exact bytes.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Compare_V1V2_GrownMethodDetected()
    {
        var (v1, v2) = ReadPair();

        var diff = MstatDiffer.Compare(v1, v2);

        var grown = Assert.ContainsSingle(c =>
            c.Name == "Greet(string)" && c.AssemblyName == "NativeAotConsole", diff.Contributors);
        Assert.AreEqual(DiffKind.Changed, grown.Diff);
        Assert.IsGreaterThan(0, grown.Delta);
        Assert.IsGreaterThan(0, grown.LeftSize);
    }

    /// <summary>
    /// Verifies signature-keyed identity: V2 grows Greet(string) and leaves Greet(int)
    /// untouched, so the string overload appears among the changed entries and the int
    /// overload does not — the two are never merged into one row.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Compare_V1V2_OverloadsTrackedSeparately()
    {
        var (v1, v2) = ReadPair();

        var diff = MstatDiffer.Compare(v1, v2);

        Assert.Contains(c =>
            c.Name == "Greet(string)" && c.AssemblyName == "NativeAotConsole", diff.Contributors);
        Assert.DoesNotContain(c =>
            c.Name == "Greet(int)" && c.AssemblyName == "NativeAotConsole", diff.Contributors);
    }

    /// <summary>
    /// Verifies the manifest resource embedded only in V2 produces an added Resources
    /// category.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Compare_V1V2_ResourceCategoryAdded()
    {
        var (v1, v2) = ReadPair();
        TestSkip.When(v2.ManifestResources.Count == 0, "V2 fixture embeds no resources");

        var diff = MstatDiffer.Compare(v1, v2);

        var resources = Assert.ContainsSingle(n => n.Name == "Resources", diff.Root.Children);
        Assert.AreEqual(DiffKind.Added, resources.Diff);
        Assert.IsGreaterThan(0, resources.Delta);
        Assert.Contains(c =>
            c.Kind == SizeNodeKind.Resource && c.Name == "NativeAotConsole.Payload.txt", diff.Contributors);
    }

    /// <summary>
    /// Verifies the recursive tree invariant: every interior node's sizes are exactly the
    /// sums of its children.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Compare_V1V2_NodeSumsEqualChildSums()
    {
        var (v1, v2) = ReadPair();

        var diff = MstatDiffer.Compare(v1, v2);

        static void AssertSums(SizeDiffNode node)
        {
            if (node.Children.Count == 0) return;
            Assert.AreEqual(node.Children.Sum(c => c.LeftSize), node.LeftSize);
            Assert.AreEqual(node.Children.Sum(c => c.RightSize), node.RightSize);
            Assert.AreEqual(node.Children.Sum(c => c.Delta), node.Delta);
            foreach (var child in node.Children)
                AssertSums(child);
        }

        AssertSums(diff.Root);
    }

    /// <summary>
    /// Verifies contributors are ordered by absolute delta, largest first, and the added
    /// Telemetry members are among them.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Compare_V1V2_ContributorsOrderedByAbsoluteDelta()
    {
        var (v1, v2) = ReadPair();

        var diff = MstatDiffer.Compare(v1, v2);

        for (var i = 1; i < diff.Contributors.Count; i++)
        {
            Assert.IsGreaterThanOrEqualTo(Math.Abs(diff.Contributors[i].Delta), Math.Abs(diff.Contributors[i - 1].Delta), $"contributors out of order at {i}");
        }

        Assert.Contains(c => c.Namespace == "NativeAotConsole.Telemetry", diff.Contributors);
    }

    /// <summary>
    /// Verifies blobs match by name across builds: the Metadata region exists in both, so it
    /// must appear as one changed-or-unchanged entry, never as an added/removed pair.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Compare_V1V2_BlobsMatchedByName()
    {
        var (v1, v2) = ReadPair();

        var diff = MstatDiffer.Compare(v1, v2);

        var metadata = diff.Contributors.Where(c => c.FullPath == "Blobs/Metadata").ToList();
        TestSkip.When(metadata.Count == 0, "Metadata blob byte-identical across the builds");
        var entry = Assert.ContainsSingle(metadata);
        Assert.AreEqual(DiffKind.Changed, entry.Diff);
        Assert.IsGreaterThan(0, entry.LeftSize);
        Assert.IsGreaterThan(0, entry.RightSize);
    }

    /// <summary>
    /// Verifies a self-diff is empty: no changed entries, zero delta, and the unchanged mass
    /// equals the build total.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Compare_SelfDiff_AllUnchangedZeroDelta()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "V1 mstat sidecar was not produced");
        var v1 = MstatReader.Read(Samples.NativeAotConsoleMstat!);
        Assert.IsNotNull(v1);

        var diff = MstatDiffer.Compare(v1, v1);

        Assert.IsEmpty(diff.Root.Children);
        Assert.IsEmpty(diff.Contributors);
        Assert.AreEqual(0, diff.Summary.Delta);
        Assert.AreEqual(diff.Summary.LeftTotal, diff.Summary.RightTotal);
        Assert.AreEqual(diff.Summary.LeftTotal, diff.Summary.UnchangedTotal);
        TestAssert.All(diff.Summary.Counts, c =>
        {
            Assert.AreEqual(0, c.Added);
            Assert.AreEqual(0, c.Removed);
            Assert.AreEqual(0, c.Grown);
            Assert.AreEqual(0, c.Shrunk);
        });
    }

    /// <summary>
    /// Verifies namespace aggregates carry the added namespace and fold across assemblies:
    /// the System namespace has rows in more than one assembly, and its aggregate must be the
    /// sum over all of them, recomputed here independently.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Compare_V1V2_NamespaceDeltasFoldAcrossAssemblies()
    {
        var (v1, v2) = ReadPair();

        var diff = MstatDiffer.Compare(v1, v2);

        var telemetry = Assert.ContainsSingle(a => a.Name == "NativeAotConsole.Telemetry", diff.NamespaceDeltas);
        Assert.AreEqual(0, telemetry.LeftSize);
        Assert.IsGreaterThan(0, telemetry.Delta);

        var policy = MstatSectionPolicy.ForPair(v1, v2);
        var rightIndex = MstatSizeIndex.Create(v2, policy);
        var assembliesWithSystem = rightIndex.Entries
            .Where(e => e.Namespace == "System"
                && e.Section is MstatSectionKind.Method or MstatSectionKind.MethodTable)
            .Select(e => e.AssemblyName)
            .Distinct()
            .Count();
        TestSkip.When(assembliesWithSystem < 2, "System namespace spans fewer than two assemblies");

        var system = Assert.ContainsSingle(a => a.Name == "System", diff.NamespaceDeltas);
        Assert.AreEqual(rightIndex.NamespaceTotals["System"], system.RightSize);
    }

    /// <summary>
    /// Verifies aggregated entries are marked: the frozen string-literal bucket folds many
    /// rows and must say so through its entry counts.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Compare_V1V2_AggregatedEntriesCarryCounts()
    {
        var (v1, v2) = ReadPair();
        TestSkip.When(
            v1.FrozenObjects.Count(f => f.OwningType is null) < 2,
            "fixture froze fewer than two string literals");

        var diff = MstatDiffer.Compare(v1, v2);

        var literals = diff.Contributors.FirstOrDefault(c =>
            c.Kind == SizeNodeKind.FrozenObject
            && c.AssemblyName == MstatSizeIndex.UnattributedName);
        TestSkip.When(literals is null, "frozen literals byte-identical across the builds");
        Assert.IsGreaterThan(1, literals!.LeftEntryCount);
        Assert.IsGreaterThan(1, literals.RightEntryCount);
        Assert.IsGreaterThan(1, literals.RightNodeNames.Count);
    }

    /// <summary>
    /// Verifies an added contributor's node names join the V2 dependency graph — the "why did
    /// this appear" answer resolves against real labels.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Compare_V1V2_AddedContributorNodeNamesJoinRightDgml()
    {
        var (v1, v2) = ReadPair();
        TestSkip.When(Samples.NativeAotConsoleV2Dgml is null, "V2 DGML sidecar was not produced");

        var diff = MstatDiffer.Compare(v1, v2);
        var dgml = DgmlReader.Read(Samples.NativeAotConsoleV2Dgml!);
        Assert.IsNotNull(dgml);

        var added = diff.Contributors.First(c =>
            c.Diff == DiffKind.Added
            && c.Namespace == "NativeAotConsole.Telemetry"
            && c.Kind == SizeNodeKind.Method
            && c.RightNodeNames.Count > 0);
        Assert.Contains(name => dgml.FindNodeByLabel(name) is not null, added.RightNodeNames);
    }

    /// <summary>
    /// Verifies the pre-node-name degradation path with a byte-patched copy of the real V1
    /// report: renaming the <c>.names</c> PE section makes every node-name lookup miss —
    /// the observable shape of a 1.x report, which the .NET 7 toolchain would be needed to
    /// produce for real. Entries still match by their name-based keys, so a self-comparison
    /// against the unpatched report stays empty.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Compare_MstatWithoutNamesSection_MatchesWithEmptyNodeNames()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "V1 mstat sidecar was not produced");

        var patched = Path.Combine(Path.GetTempPath(), $"dotsider-nonames-{Guid.NewGuid():N}.mstat");
        try
        {
            var bytes = File.ReadAllBytes(Samples.NativeAotConsoleMstat!);
            var sectionName = ".names\0\0"u8.ToArray();
            var offset = FindBytes(bytes, sectionName);
            TestSkip.When(offset < 0, "report carries no .names section");
            bytes[offset + 1] = (byte)'x';
            File.WriteAllBytes(patched, bytes);

            var noNames = MstatReader.Read(patched);
            var original = MstatReader.Read(Samples.NativeAotConsoleMstat!);
            Assert.IsNotNull(noNames);
            Assert.IsNotNull(original);
            TestAssert.All(noNames.Methods, m => Assert.IsNull(m.NodeName));

            var diff = MstatDiffer.Compare(noNames, original);
            Assert.IsEmpty(diff.Root.Children);
            Assert.AreEqual(0, diff.Summary.Delta);
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
