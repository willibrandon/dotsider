using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="DependencyGraphBuilder"/> covering transitive traversal, identity-based
/// dedupe, cycle and diamond preservation, unresolved and identity-mismatch leaves, per-edge
/// TypeRef counts by full identity, cancellation, determinism, and behavior across bundle,
/// apphost, and native deployment shapes.
/// </summary>
[TestClass]
public class DependencyGraphBuilderTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>HelloWorld produces a root node marked IsRoot.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void HelloWorld_HasRootNode()
    {
        using var a = new AssemblyAnalyzer(Samples.HelloWorldDll);
        var graph = DependencyGraphBuilder.Build(a);
        Assert.IsNotEmpty(graph.Nodes);
        Assert.ContainsSingle(n => n.IsRoot, graph.Nodes);
    }

    /// <summary>The root node's name matches the analyzed assembly name.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void HelloWorld_RootNodeNameMatchesAssembly()
    {
        using var a = new AssemblyAnalyzer(Samples.HelloWorldDll);
        var graph = DependencyGraphBuilder.Build(a);
        Assert.AreEqual("HelloWorld", graph.Nodes.First(n => n.IsRoot).Name);
    }

    /// <summary>RichLibrary still sees its direct third-party references as nodes.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibrary_HasDirectReferenceNode_NewtonSoft()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var graph = DependencyGraphBuilder.Build(a);
        Assert.Contains(n => n.Name == "Newtonsoft.Json", graph.Nodes);
    }

    /// <summary>
    /// RichLibrary's graph has nodes at depth greater than one, proving the traversal walks
    /// past the root's direct references into transitive references.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibrary_HasNodesAtDepthGreaterThanOne()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var graph = DependencyGraphBuilder.Build(a);
        Assert.Contains(n => n.Depth > 1, graph.Nodes);
    }

    /// <summary>
    /// RichLibrary's graph contains edges whose source is not the root — direct evidence that
    /// the builder is emitting transitive child-to-child edges, not only root-to-child.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibrary_HasEdgesWhereSourceIsNotRoot()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var graph = DependencyGraphBuilder.Build(a);
        var rootId = graph.Nodes.First(n => n.IsRoot).Id;
        Assert.Contains(e => e.SourceId != rootId, graph.Edges);
    }

    /// <summary>Every edge references a node id that exists in the node set.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void AllEdgesReferenceExistingNodesById()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var graph = DependencyGraphBuilder.Build(a);
        var ids = graph.Nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var e in graph.Edges)
        {
            Assert.Contains(e.SourceId, ids);
            Assert.Contains(e.TargetId, ids);
        }
    }

    /// <summary>Node ids are unique across the graph.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NoDuplicateNodeIds()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var graph = DependencyGraphBuilder.Build(a);
        var ids = graph.Nodes.Select(n => n.Id).ToList();
        Assert.HasCount(ids.Count, ids.Distinct(StringComparer.Ordinal));
    }

    /// <summary>Exactly one root node.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void OnlyOneRootNode()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var graph = DependencyGraphBuilder.Build(a);
        Assert.ContainsSingle(n => n.IsRoot, graph.Nodes);
    }

    /// <summary>At least one transitive edge has a positive TypeRef count.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TransitiveEdgesCarryTypeRefCounts()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var graph = DependencyGraphBuilder.Build(a);
        Assert.Contains(e => e.TypeRefCount > 0, graph.Edges);
    }

    /// <summary>
    /// Every non-root node carries navigation context keyed by its id, and the root entry
    /// has <see cref="AssemblyProvenance.Root"/>.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NavigationById_CoversEveryNode_AndRootIsRoot()
    {
        using var a = new AssemblyAnalyzer(Samples.HelloWorldDll);
        var graph = DependencyGraphBuilder.Build(a);
        foreach (var n in graph.Nodes)
            Assert.IsTrue(graph.NavigationById.ContainsKey(n.Id), $"missing nav for {n.Id}");

        var root = graph.Nodes.First(n => n.IsRoot);
        Assert.AreEqual(AssemblyProvenance.Root, graph.NavigationById[root.Id].Provenance);
    }

    /// <summary>Building the same graph twice produces identical node id order and edge lists.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Determinism_NodesAndEdgesOrderStableAcrossRuns()
    {
        using var a1 = new AssemblyAnalyzer(Samples.RichLibraryDll);
        using var a2 = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var g1 = DependencyGraphBuilder.Build(a1);
        var g2 = DependencyGraphBuilder.Build(a2);

        Assert.AreSequenceEqual(
            [.. g1.Nodes.Select(n => n.Id)],
            [.. g2.Nodes.Select(n => n.Id)]);
        Assert.AreSequenceEqual(
            [.. g1.Edges.Select(e => (e.SourceId, e.TargetId))],
            [.. g2.Edges.Select(e => (e.SourceId, e.TargetId))]);
    }

    /// <summary>
    /// A reference to an assembly that cannot be located anywhere produces an unresolved leaf
    /// with <see cref="GraphNode.Unresolved"/> set and provenance <see cref="AssemblyProvenance.Unresolved"/>.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void UnresolvedRef_AddedAsLeafWithUnresolvedFlag()
    {
        using var scope = SyntheticAssemblyScope.Create();
        var rootPath = scope.WriteAssembly("RootUnres", refs: [("NonExistentTarget_ZZZ", new Version(1, 0, 0, 0))]);

        using var a = new AssemblyAnalyzer(rootPath);
        var graph = DependencyGraphBuilder.Build(a);

        var leaf = graph.Nodes.FirstOrDefault(n => n.Name == "NonExistentTarget_ZZZ");
        Assert.IsNotNull(leaf);
        Assert.IsTrue(leaf!.Unresolved);
        Assert.AreEqual(AssemblyProvenance.Unresolved,
            graph.NavigationById[leaf.Id].Provenance);
    }

    /// <summary>
    /// When a probe finds a file whose simple name matches but whose manifest identity does not,
    /// the builder must mark the node as an unresolved leaf with
    /// <see cref="AssemblyProvenance.IdentityMismatch"/> and must not expand from the mismatched
    /// file. The candidate probe path is recorded on the navigation context for diagnostics.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void IdentityMismatch_DoesNotGraftSubtree()
    {
        using var scope = SyntheticAssemblyScope.Create();
        // Place a "MisIdent" assembly on disk with version 9.9.9.9 and a child ref to "ChildOfMis".
        scope.WriteAssembly(
            "MisIdent", new Version(9, 9, 9, 9),
            refs: [("ChildOfMis", new Version(1, 0, 0, 0))]);
        // Also place the child so, if the mismatch were silently expanded, ChildOfMis would appear.
        scope.WriteAssembly("ChildOfMis", new Version(1, 0, 0, 0));
        // Root requests "MisIdent" version 1.0.0.0 — identity mismatch.
        var rootPath = scope.WriteAssembly(
            "RootMis",
            refs: [("MisIdent", new Version(1, 0, 0, 0))]);

        using var a = new AssemblyAnalyzer(rootPath);
        var graph = DependencyGraphBuilder.Build(a);

        var mis = graph.Nodes.FirstOrDefault(n => n.Name == "MisIdent");
        Assert.IsNotNull(mis);
        Assert.IsTrue(mis!.Unresolved);
        Assert.AreEqual(AssemblyProvenance.IdentityMismatch,
            graph.NavigationById[mis.Id].Provenance);
        Assert.DoesNotContain(n => n.Name == "ChildOfMis", graph.Nodes);
        Assert.IsNotNull(graph.NavigationById[mis.Id].CandidateProbePath);
    }

    /// <summary>
    /// Two references with the same simple name but different versions produce two distinct nodes.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void DuplicateSimpleName_DifferentVersion_ProducesTwoNodes()
    {
        using var scope = SyntheticAssemblyScope.Create();
        var rootPath = scope.WriteAssembly(
            "RootDupVersion",
            refs:
            [
                ("TargetLib", new Version(1, 0, 0, 0)),
                ("TargetLib", new Version(2, 0, 0, 0)),
            ]);

        using var a = new AssemblyAnalyzer(rootPath);
        var graph = DependencyGraphBuilder.Build(a);

        var targets = graph.Nodes.Where(n => n.Name == "TargetLib").ToList();
        Assert.HasCount(2, targets);
        Assert.AreNotEqual(targets[0].Id, targets[1].Id);
    }

    /// <summary>
    /// Two references with the same simple name but different public key tokens produce two distinct nodes.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void DuplicateSimpleName_DifferentPublicKeyToken_ProducesTwoNodes()
    {
        using var scope = SyntheticAssemblyScope.Create();
        var rootPath = scope.WriteAssembly(
            "RootDupPkt",
            refs:
            [
                ("TargetPktLib", new Version(1, 0, 0, 0), (byte[]?)[1, 2, 3, 4, 5, 6, 7, 8]),
                ("TargetPktLib", new Version(1, 0, 0, 0), (byte[]?)[9, 9, 9, 9, 9, 9, 9, 9]),
            ]);

        using var a = new AssemblyAnalyzer(rootPath);
        var graph = DependencyGraphBuilder.Build(a);

        var targets = graph.Nodes.Where(n => n.Name == "TargetPktLib").ToList();
        Assert.HasCount(2, targets);
        Assert.AreNotEqual(targets[0].Id, targets[1].Id);
    }

    /// <summary>
    /// TypeRefs whose resolution scope ultimately derives from an AssemblyRef are counted against
    /// the full identity of that AssemblyRef, so an edge's TypeRefCount is meaningful even when
    /// two refs share a simple name.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TypeRefCount_GroupsByFullIdentity_NotSimpleName()
    {
        using var scope = SyntheticAssemblyScope.Create();
        var rootPath = scope.WriteAssembly(
            "RootCountCheck",
            refs:
            [
                ("TargetCountLib", new Version(1, 0, 0, 0)),
                ("TargetCountLib", new Version(2, 0, 0, 0)),
            ],
            typeRefs:
            [
                ("Sample.T1", 0),
                ("Sample.T2", 0),
                ("Sample.T3", 1),
            ]);

        using var a = new AssemblyAnalyzer(rootPath);
        var graph = DependencyGraphBuilder.Build(a);

        var rootId = graph.Nodes.First(n => n.IsRoot).Id;
        var v1Id = graph.Nodes.Single(n => n.Name == "TargetCountLib" && n.Version == "1.0.0.0").Id;
        var v2Id = graph.Nodes.Single(n => n.Name == "TargetCountLib" && n.Version == "2.0.0.0").Id;

        var edgeToV1 = graph.Edges.Single(e => e.SourceId == rootId && e.TargetId == v1Id);
        var edgeToV2 = graph.Edges.Single(e => e.SourceId == rootId && e.TargetId == v2Id);

        Assert.AreEqual(2, edgeToV1.TypeRefCount);
        Assert.AreEqual(1, edgeToV2.TypeRefCount);
    }

    /// <summary>
    /// AppLocal probe finds a strong-named Microsoft framework assembly whose deployed version
    /// is a strict roll-forward of a stale AssemblyRef in a transitive dependency. The graph
    /// keys the node on the loaded (deployed) identity and records the original requested
    /// identity on the edge — no IdentityMismatch leaf, no duplicate node. Reproduced with the
    /// AppLocalRollForward sample where Microsoft.Diagnostics.Tracing.TraceEvent 3.2.2's
    /// AssemblyRef points at Microsoft.Diagnostics.NETCore.Client v0.2.10.10501 while NuGet
    /// restored v0.2.13.11903 next to it.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void AppLocalRollForward_FrameworkPkt_HigherVersion_ResolvesAndRecordsRequestedIdentity()
    {
        using var a = new AssemblyAnalyzer(Samples.AppLocalRollForwardDll);
        var graph = DependencyGraphBuilder.Build(a);

        var nodes = graph.Nodes
            .Where(n => n.Name == "Microsoft.Diagnostics.NETCore.Client")
            .ToList();
        var node = Assert.ContainsSingle(nodes);
        Assert.IsFalse(node.Unresolved);

        var nav = graph.NavigationById[node.Id];
        Assert.AreEqual(AssemblyProvenance.AppLocal, nav.Provenance);
        Assert.IsNotNull(nav.LoadedIdentity);
        Assert.AreEqual(node.Version, nav.LoadedIdentity!.Version);

        var traceEventNode = graph.Nodes.Single(
            n => n.Name == "Microsoft.Diagnostics.Tracing.TraceEvent");
        var edge = graph.Edges.Single(
            e => e.SourceId == traceEventNode.Id && e.TargetId == node.Id);
        Assert.IsNotNull(edge.RequestedIdentity);
        Assert.AreNotEqual(node.Version, edge.RequestedIdentity!.Version);
        Assert.AreEqual("31bf3856ad364e35", edge.RequestedIdentity.PublicKeyToken);
    }

    /// <summary>
    /// When a reference forms a cycle back to an ancestor, the cycle-closing edge is emitted but
    /// the ancestor is not re-expanded.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Cycle_EdgeEmittedButNotRecursed()
    {
        using var scope = SyntheticAssemblyScope.Create();
        scope.WriteAssembly("CycA", refs: [("CycB", new Version(1, 0, 0, 0))]);
        scope.WriteAssembly("CycB", refs: [("CycA", new Version(1, 0, 0, 0))]);
        var rootPath = Path.Combine(scope.Directory, "CycA.dll");

        using var a = new AssemblyAnalyzer(rootPath);
        var graph = DependencyGraphBuilder.Build(a);

        var cycA = graph.Nodes.Single(n => n.Name == "CycA");
        var cycB = graph.Nodes.Single(n => n.Name == "CycB");

        Assert.Contains(e => e.SourceId == cycA.Id && e.TargetId == cycB.Id, graph.Edges);
        Assert.Contains(e => e.SourceId == cycB.Id && e.TargetId == cycA.Id, graph.Edges);
        Assert.ContainsSingle(n => n.Name == "CycA", graph.Nodes);
        Assert.ContainsSingle(n => n.Name == "CycB", graph.Nodes);
    }

    /// <summary>
    /// When two different parents reference the same child, the child appears once (merged on
    /// identity) and both parent-to-child edges are emitted.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Diamond_MergedOnIdentity()
    {
        using var scope = SyntheticAssemblyScope.Create();
        scope.WriteAssembly("DiaRoot", refs:
        [
            ("DiaLeftBranch", new Version(1, 0, 0, 0)),
            ("DiaRightBranch", new Version(1, 0, 0, 0)),
        ]);
        scope.WriteAssembly("DiaLeftBranch", refs: [("DiaCommon", new Version(1, 0, 0, 0))]);
        scope.WriteAssembly("DiaRightBranch", refs: [("DiaCommon", new Version(1, 0, 0, 0))]);
        scope.WriteAssembly("DiaCommon");

        var rootPath = Path.Combine(scope.Directory, "DiaRoot.dll");
        using var a = new AssemblyAnalyzer(rootPath);
        var graph = DependencyGraphBuilder.Build(a);

        Assert.ContainsSingle(n => n.Name == "DiaCommon", graph.Nodes);
        var commonId = graph.Nodes.Single(n => n.Name == "DiaCommon").Id;
        Assert.AreEqual(2, graph.Edges.Count(e => e.TargetId == commonId));
    }

    /// <summary>
    /// A token canceled before the build begins is observed before any traversal work is reported.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BuildWithCancellation_AlreadyCanceled_StopsBeforeTraversal()
    {
        using var analyzer = new AssemblyAnalyzer(Samples.HelloWorldDll);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var observed = new List<DependencyGraphBuildCheckpoint>();

        var exception = Assert.ThrowsExactly<OperationCanceledException>(() =>
            DependencyGraphBuilder.BuildWithCancellation(
                analyzer,
                cancellation.Token,
                observed.Add));

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        Assert.IsEmpty(observed);
    }

    /// <summary>
    /// Cancellation requested after the first managed reference has been resolved stops the walk
    /// and disposes a child analyzer that was already queued for transitive traversal.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BuildWithCancellation_AfterManagedReferenceProcessed_StopsAndCleansUp()
    {
        using var scope = SyntheticAssemblyScope.Create();
        scope.WriteAssembly("CancelChild");
        var rootPath = scope.WriteAssembly(
            "CancelRoot",
            refs: [("CancelChild", new Version(1, 0, 0, 0))]);
        var directory = scope.Directory;
        var observed = new List<DependencyGraphBuildCheckpoint>();
        using var cancellation = new CancellationTokenSource();

        OperationCanceledException exception;
        using (var analyzer = new AssemblyAnalyzer(rootPath))
        {
            exception = Assert.ThrowsExactly<OperationCanceledException>(() =>
                DependencyGraphBuilder.BuildWithCancellation(
                    analyzer,
                    cancellation.Token,
                    checkpoint =>
                    {
                        observed.Add(checkpoint);
                        cancellation.Cancel();
                    }));
        }

        scope.Dispose();

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        Assert.AreSequenceEqual(
            [DependencyGraphBuildCheckpoint.ManagedAssemblyReferenceProcessed],
            observed);
        Assert.IsFalse(System.IO.Directory.Exists(directory));
    }

    /// <summary>
    /// A Native AOT binary with mstat and DGML sidecars produces a real graph: the compiled
    /// assemblies as nodes, DGML links aggregated to assembly-pair edges, and the native
    /// import modules at depth 1.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeAot_BuildsAssemblyAndImportGraph()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        using var a = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);
        var graph = DependencyGraphBuilder.Build(a);

        var root = Assert.ContainsSingle(n => n.IsRoot, graph.Nodes);
        Assert.AreEqual("NativeAotConsole", root.Name);
        Assert.Contains(n =>
            n.Kind == GraphNodeKind.Assembly && n.Name == "System.Private.CoreLib", graph.Nodes);
        Assert.Contains(n => n.Kind == GraphNodeKind.NativeImport, graph.Nodes);

        if (Samples.NativeAotConsoleDgml is not null)
            Assert.Contains(e => e.SourceId == root.Id && e.TypeRefCount > 0, graph.Edges);
    }

    /// <summary>
    /// Cancellation requested after a real DGML link has been inspected stops Native AOT
    /// assembly-edge aggregation before the remaining links are visited.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BuildWithCancellation_AfterDgmlLinkProcessed_StopsAggregation()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");
        TestSkip.When(Samples.NativeAotConsoleDgml is null, "DGML sidecar was not produced");

        using var analyzer = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);
        Assert.IsNotEmpty(analyzer.Dgml!.Links);
        var observed = new List<DependencyGraphBuildCheckpoint>();
        using var cancellation = new CancellationTokenSource();

        var exception = Assert.ThrowsExactly<OperationCanceledException>(() =>
            DependencyGraphBuilder.BuildWithCancellation(
                analyzer,
                cancellation.Token,
                checkpoint =>
                {
                    if (checkpoint != DependencyGraphBuildCheckpoint.DgmlLinkProcessed)
                        return;

                    observed.Add(checkpoint);
                    cancellation.Cancel();
                }));

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        Assert.AreSequenceEqual(
            [DependencyGraphBuildCheckpoint.DgmlLinkProcessed],
            observed);
    }

    /// <summary>
    /// The compiled-in assembly nodes carry full identity — version and public key token —
    /// and their navigation contexts classify framework assemblies so the hide-framework
    /// filter works on the AOT graph.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeAot_AssemblyNodesCarryIdentityAndFrameworkClassification()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        using var a = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);
        var graph = DependencyGraphBuilder.Build(a);

        var corelib = graph.Nodes.First(n => n.Name == "System.Private.CoreLib");
        Assert.IsNotNull(corelib.Version);
        Assert.IsTrue(graph.NavigationById[corelib.Id].IsFrameworkAssembly);
    }

    /// <summary>
    /// Every AOT node carries a navigation context with a null resolution and the
    /// compiled-into-native-image provenance, so Enter degrades to a message instead of
    /// reporting a missing context.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeAot_AllNodesHaveDegradedNavigationContexts()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        using var a = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);
        var graph = DependencyGraphBuilder.Build(a);

        TestAssert.All(graph.Nodes, n =>
        {
            Assert.IsTrue(graph.NavigationById.ContainsKey(n.Id), $"{n.Name}: navigation context missing");
            Assert.IsNull(graph.NavigationById[n.Id].Resolved);
        });
        TestAssert.All(graph.Nodes.Where(n => !n.IsRoot), n =>
            Assert.AreEqual(AssemblyProvenance.CompiledIntoNativeImage, graph.NavigationById[n.Id].Provenance));
    }

    /// <summary>
    /// A Native AOT binary with no sidecars falls back to the import star: the root plus its
    /// native import modules, every edge sourced from the root.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeAot_WithoutSidecars_ReturnsRootPlusImportStar()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null || !File.Exists(Samples.NativeAotConsoleExe),
            "Native AOT sample is not available on this platform.");

        var dir = Directory.CreateTempSubdirectory("dotsider-depgraph-");
        try
        {
            var exeCopy = Path.Combine(dir.FullName, Path.GetFileName(Samples.NativeAotConsoleExe!));
            File.Copy(Samples.NativeAotConsoleExe!, exeCopy);
            using var a = new AssemblyAnalyzer(exeCopy);

            var graph = DependencyGraphBuilder.Build(a);

            var root = Assert.ContainsSingle(n => n.IsRoot, graph.Nodes);
            TestAssert.All(graph.Nodes.Where(n => !n.IsRoot), n =>
                Assert.AreEqual(GraphNodeKind.NativeImport, n.Kind));
            TestAssert.All(graph.Edges, e => Assert.AreEqual(root.Id, e.SourceId));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>
    /// A native binary that is not Native AOT (a plain apphost opened directly) still
    /// produces a root-only graph and does not throw — the guarantee the AOT branch must not
    /// disturb.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeNonAot_ReturnsRootOnlyGraph()
    {
        using var a = new AssemblyAnalyzer(Samples.HelloWorldExe);
        Assert.AreEqual(BinaryKind.Native, a.BinaryKind);

        var graph = DependencyGraphBuilder.Build(a);

        Assert.ContainsSingle(graph.Nodes);
        Assert.IsTrue(graph.Nodes[0].IsRoot);
        Assert.IsEmpty(graph.Edges);
    }

    /// <summary>
    /// A bundle-backed analyzer opened via <see cref="AssemblyLoader"/> still yields a transitive
    /// graph without throwing when the builder walks through bundle-extracted refs.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BundleBacked_Traversal_Works()
    {
        TestSkip.When(Samples.SelfContainedConsoleExe is null || !File.Exists(Samples.SelfContainedConsoleExe),
            "Self-contained sample is not available on this platform.");
        var result = AssemblyLoader.Open(Samples.SelfContainedConsoleExe!);
        using var analyzer = result switch
        {
            AssemblyOpenResult.Direct d => d.Analyzer,
            AssemblyOpenResult.ApphostWithCompanion ac => ac.HostAnalyzer,
            AssemblyOpenResult.BundleEntry be => be.EntryAnalyzer,
            _ => throw new InvalidOperationException("unexpected open result"),
        };

        var graph = DependencyGraphBuilder.Build(analyzer);
        Assert.Contains(n => n.IsRoot, graph.Nodes);
        Assert.Contains(n => n.Depth > 0, graph.Nodes);
    }

    /// <summary>
    /// Framework classification identifies well-known Microsoft framework assemblies even when
    /// they are resolved outside of the shared runtime directory (e.g., from app-local copies
    /// in a self-contained publish).
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void FrameworkAssemblies_AreClassifiedAcrossDeploymentModels()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var graph = DependencyGraphBuilder.Build(a);

        var systemRuntime = graph.Nodes.FirstOrDefault(n => n.Name == "System.Runtime");
        Assert.IsNotNull(systemRuntime);
        Assert.IsTrue(graph.NavigationById[systemRuntime!.Id].IsFrameworkAssembly);
    }

}
