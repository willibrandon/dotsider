using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="DependencyGraphBuilder"/> covering transitive traversal, identity-based
/// dedupe, cycle and diamond preservation, unresolved and identity-mismatch leaves, per-edge
/// TypeRef counts by full identity, determinism, and behavior across bundle, apphost, and
/// native deployment shapes.
/// </summary>
[Collection("SampleAssemblies")]
public class DependencyGraphBuilderTests(SampleAssemblyFixture samples)
{
    /// <summary>HelloWorld produces a root node marked IsRoot.</summary>
    [Fact(Timeout = 30_000)]
    public void HelloWorld_HasRootNode()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        var graph = DependencyGraphBuilder.Build(a);
        Assert.NotEmpty(graph.Nodes);
        Assert.Single(graph.Nodes, n => n.IsRoot);
    }

    /// <summary>The root node's name matches the analyzed assembly name.</summary>
    [Fact(Timeout = 30_000)]
    public void HelloWorld_RootNodeNameMatchesAssembly()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        var graph = DependencyGraphBuilder.Build(a);
        Assert.Equal("HelloWorld", graph.Nodes.First(n => n.IsRoot).Name);
    }

    /// <summary>RichLibrary still sees its direct third-party references as nodes.</summary>
    [Fact(Timeout = 30_000)]
    public void RichLibrary_HasDirectReferenceNode_NewtonSoft()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var graph = DependencyGraphBuilder.Build(a);
        Assert.Contains(graph.Nodes, n => n.Name == "Newtonsoft.Json");
    }

    /// <summary>
    /// RichLibrary's graph has nodes at depth greater than one, proving the traversal walks
    /// past the root's direct references into transitive references.
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void RichLibrary_HasNodesAtDepthGreaterThanOne()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var graph = DependencyGraphBuilder.Build(a);
        Assert.Contains(graph.Nodes, n => n.Depth > 1);
    }

    /// <summary>
    /// RichLibrary's graph contains edges whose source is not the root — direct evidence that
    /// the builder is emitting transitive child-to-child edges, not only root-to-child.
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void RichLibrary_HasEdgesWhereSourceIsNotRoot()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var graph = DependencyGraphBuilder.Build(a);
        var rootId = graph.Nodes.First(n => n.IsRoot).Id;
        Assert.Contains(graph.Edges, e => e.SourceId != rootId);
    }

    /// <summary>Every edge references a node id that exists in the node set.</summary>
    [Fact(Timeout = 60_000)]
    public void AllEdgesReferenceExistingNodesById()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var graph = DependencyGraphBuilder.Build(a);
        var ids = graph.Nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var e in graph.Edges)
        {
            Assert.Contains(e.SourceId, ids);
            Assert.Contains(e.TargetId, ids);
        }
    }

    /// <summary>Node ids are unique across the graph.</summary>
    [Fact(Timeout = 60_000)]
    public void NoDuplicateNodeIds()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var graph = DependencyGraphBuilder.Build(a);
        var ids = graph.Nodes.Select(n => n.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>Exactly one root node.</summary>
    [Fact(Timeout = 30_000)]
    public void OnlyOneRootNode()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var graph = DependencyGraphBuilder.Build(a);
        Assert.Single(graph.Nodes, n => n.IsRoot);
    }

    /// <summary>At least one transitive edge has a positive TypeRef count.</summary>
    [Fact(Timeout = 60_000)]
    public void TransitiveEdgesCarryTypeRefCounts()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var graph = DependencyGraphBuilder.Build(a);
        Assert.Contains(graph.Edges, e => e.TypeRefCount > 0);
    }

    /// <summary>
    /// Every non-root node carries navigation context keyed by its id, and the root entry
    /// has <see cref="AssemblyProvenance.Root"/>.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NavigationById_CoversEveryNode_AndRootIsRoot()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        var graph = DependencyGraphBuilder.Build(a);
        foreach (var n in graph.Nodes)
            Assert.True(graph.NavigationById.ContainsKey(n.Id), $"missing nav for {n.Id}");

        var root = graph.Nodes.First(n => n.IsRoot);
        Assert.Equal(AssemblyProvenance.Root, graph.NavigationById[root.Id].Provenance);
    }

    /// <summary>Building the same graph twice produces identical node id order and edge lists.</summary>
    [Fact(Timeout = 60_000)]
    public void Determinism_NodesAndEdgesOrderStableAcrossRuns()
    {
        using var a1 = new AssemblyAnalyzer(samples.RichLibraryDll);
        using var a2 = new AssemblyAnalyzer(samples.RichLibraryDll);
        var g1 = DependencyGraphBuilder.Build(a1);
        var g2 = DependencyGraphBuilder.Build(a2);

        Assert.Equal<IEnumerable<string>>(
            [.. g1.Nodes.Select(n => n.Id)],
            [.. g2.Nodes.Select(n => n.Id)]);
        Assert.Equal<IEnumerable<(string, string)>>(
            [.. g1.Edges.Select(e => (e.SourceId, e.TargetId))],
            [.. g2.Edges.Select(e => (e.SourceId, e.TargetId))]);
    }

    /// <summary>
    /// A reference to an assembly that cannot be located anywhere produces an unresolved leaf
    /// with <see cref="GraphNode.Unresolved"/> set and provenance <see cref="AssemblyProvenance.Unresolved"/>.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void UnresolvedRef_AddedAsLeafWithUnresolvedFlag()
    {
        using var scope = SyntheticAssemblyScope.Create();
        var rootPath = scope.WriteAssembly("RootUnres", refs: new[] { ("NonExistentTarget_ZZZ", new Version(1, 0, 0, 0)) });

        using var a = new AssemblyAnalyzer(rootPath);
        var graph = DependencyGraphBuilder.Build(a);

        var leaf = graph.Nodes.FirstOrDefault(n => n.Name == "NonExistentTarget_ZZZ");
        Assert.NotNull(leaf);
        Assert.True(leaf!.Unresolved);
        Assert.Equal(AssemblyProvenance.Unresolved,
            graph.NavigationById[leaf.Id].Provenance);
    }

    /// <summary>
    /// When a probe finds a file whose simple name matches but whose manifest identity does not,
    /// the builder must mark the node as an unresolved leaf with
    /// <see cref="AssemblyProvenance.IdentityMismatch"/> and must not expand from the mismatched
    /// file. The candidate probe path is recorded on the navigation context for diagnostics.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void IdentityMismatch_DoesNotGraftSubtree()
    {
        using var scope = SyntheticAssemblyScope.Create();
        // Place a "MisIdent" assembly on disk with version 9.9.9.9 and a child ref to "ChildOfMis".
        scope.WriteAssembly(
            "MisIdent", new Version(9, 9, 9, 9),
            refs: new[] { ("ChildOfMis", new Version(1, 0, 0, 0)) });
        // Also place the child so, if the mismatch were silently expanded, ChildOfMis would appear.
        scope.WriteAssembly("ChildOfMis", new Version(1, 0, 0, 0));
        // Root requests "MisIdent" version 1.0.0.0 — identity mismatch.
        var rootPath = scope.WriteAssembly(
            "RootMis",
            refs: new[] { ("MisIdent", new Version(1, 0, 0, 0)) });

        using var a = new AssemblyAnalyzer(rootPath);
        var graph = DependencyGraphBuilder.Build(a);

        var mis = graph.Nodes.FirstOrDefault(n => n.Name == "MisIdent");
        Assert.NotNull(mis);
        Assert.True(mis!.Unresolved);
        Assert.Equal(AssemblyProvenance.IdentityMismatch,
            graph.NavigationById[mis.Id].Provenance);
        Assert.DoesNotContain(graph.Nodes, n => n.Name == "ChildOfMis");
        Assert.NotNull(graph.NavigationById[mis.Id].CandidateProbePath);
    }

    /// <summary>
    /// Two references with the same simple name but different versions produce two distinct nodes.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void DuplicateSimpleName_DifferentVersion_ProducesTwoNodes()
    {
        using var scope = SyntheticAssemblyScope.Create();
        var rootPath = scope.WriteAssembly(
            "RootDupVersion",
            refs: new[]
            {
                ("TargetLib", new Version(1, 0, 0, 0)),
                ("TargetLib", new Version(2, 0, 0, 0)),
            });

        using var a = new AssemblyAnalyzer(rootPath);
        var graph = DependencyGraphBuilder.Build(a);

        var targets = graph.Nodes.Where(n => n.Name == "TargetLib").ToList();
        Assert.Equal(2, targets.Count);
        Assert.NotEqual(targets[0].Id, targets[1].Id);
    }

    /// <summary>
    /// Two references with the same simple name but different public key tokens produce two distinct nodes.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void DuplicateSimpleName_DifferentPublicKeyToken_ProducesTwoNodes()
    {
        using var scope = SyntheticAssemblyScope.Create();
        var rootPath = scope.WriteAssembly(
            "RootDupPkt",
            refs: new[]
            {
                ("TargetPktLib", new Version(1, 0, 0, 0), (byte[]?)new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }),
                ("TargetPktLib", new Version(1, 0, 0, 0), (byte[]?)new byte[] { 9, 9, 9, 9, 9, 9, 9, 9 }),
            });

        using var a = new AssemblyAnalyzer(rootPath);
        var graph = DependencyGraphBuilder.Build(a);

        var targets = graph.Nodes.Where(n => n.Name == "TargetPktLib").ToList();
        Assert.Equal(2, targets.Count);
        Assert.NotEqual(targets[0].Id, targets[1].Id);
    }

    /// <summary>
    /// TypeRefs whose resolution scope ultimately derives from an AssemblyRef are counted against
    /// the full identity of that AssemblyRef, so an edge's TypeRefCount is meaningful even when
    /// two refs share a simple name.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void TypeRefCount_GroupsByFullIdentity_NotSimpleName()
    {
        using var scope = SyntheticAssemblyScope.Create();
        var rootPath = scope.WriteAssembly(
            "RootCountCheck",
            refs: new[]
            {
                ("TargetCountLib", new Version(1, 0, 0, 0)),
                ("TargetCountLib", new Version(2, 0, 0, 0)),
            },
            typeRefs: new[]
            {
                ("Sample.T1", 0),
                ("Sample.T2", 0),
                ("Sample.T3", 1),
            });

        using var a = new AssemblyAnalyzer(rootPath);
        var graph = DependencyGraphBuilder.Build(a);

        var rootId = graph.Nodes.First(n => n.IsRoot).Id;
        var v1Id = graph.Nodes.Single(n => n.Name == "TargetCountLib" && n.Version == "1.0.0.0").Id;
        var v2Id = graph.Nodes.Single(n => n.Name == "TargetCountLib" && n.Version == "2.0.0.0").Id;

        var edgeToV1 = graph.Edges.Single(e => e.SourceId == rootId && e.TargetId == v1Id);
        var edgeToV2 = graph.Edges.Single(e => e.SourceId == rootId && e.TargetId == v2Id);

        Assert.Equal(2, edgeToV1.TypeRefCount);
        Assert.Equal(1, edgeToV2.TypeRefCount);
    }

    /// <summary>
    /// When a reference forms a cycle back to an ancestor, the cycle-closing edge is emitted but
    /// the ancestor is not re-expanded.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Cycle_EdgeEmittedButNotRecursed()
    {
        using var scope = SyntheticAssemblyScope.Create();
        scope.WriteAssembly("CycA", refs: new[] { ("CycB", new Version(1, 0, 0, 0)) });
        scope.WriteAssembly("CycB", refs: new[] { ("CycA", new Version(1, 0, 0, 0)) });
        var rootPath = Path.Combine(scope.Directory, "CycA.dll");

        using var a = new AssemblyAnalyzer(rootPath);
        var graph = DependencyGraphBuilder.Build(a);

        var cycA = graph.Nodes.Single(n => n.Name == "CycA");
        var cycB = graph.Nodes.Single(n => n.Name == "CycB");

        Assert.Contains(graph.Edges, e => e.SourceId == cycA.Id && e.TargetId == cycB.Id);
        Assert.Contains(graph.Edges, e => e.SourceId == cycB.Id && e.TargetId == cycA.Id);
        Assert.Equal(1, graph.Nodes.Count(n => n.Name == "CycA"));
        Assert.Equal(1, graph.Nodes.Count(n => n.Name == "CycB"));
    }

    /// <summary>
    /// When two different parents reference the same child, the child appears once (merged on
    /// identity) and both parent-to-child edges are emitted.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Diamond_MergedOnIdentity()
    {
        using var scope = SyntheticAssemblyScope.Create();
        scope.WriteAssembly("DiaRoot", refs: new[]
        {
            ("DiaLeftBranch", new Version(1, 0, 0, 0)),
            ("DiaRightBranch", new Version(1, 0, 0, 0)),
        });
        scope.WriteAssembly("DiaLeftBranch", refs: new[] { ("DiaCommon", new Version(1, 0, 0, 0)) });
        scope.WriteAssembly("DiaRightBranch", refs: new[] { ("DiaCommon", new Version(1, 0, 0, 0)) });
        scope.WriteAssembly("DiaCommon");

        var rootPath = Path.Combine(scope.Directory, "DiaRoot.dll");
        using var a = new AssemblyAnalyzer(rootPath);
        var graph = DependencyGraphBuilder.Build(a);

        Assert.Equal(1, graph.Nodes.Count(n => n.Name == "DiaCommon"));
        var commonId = graph.Nodes.Single(n => n.Name == "DiaCommon").Id;
        Assert.Equal(2, graph.Edges.Count(e => e.TargetId == commonId));
    }

    /// <summary>
    /// An assembly whose PE has no CLR metadata (for example a native binary or unknown format)
    /// still produces a root-only graph and does not throw.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NativeOrNoMetadata_ReturnsRootOnlyGraph()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null || !File.Exists(samples.NativeAotConsoleExe),
            "Native AOT sample is not available on this platform.");
        using var a = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);
        var graph = DependencyGraphBuilder.Build(a);
        Assert.Single(graph.Nodes);
        Assert.True(graph.Nodes[0].IsRoot);
        Assert.Empty(graph.Edges);
    }

    /// <summary>
    /// A bundle-backed analyzer opened via <see cref="AssemblyLoader"/> still yields a transitive
    /// graph without throwing when the builder walks through bundle-extracted refs.
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void BundleBacked_Traversal_Works()
    {
        Assert.SkipWhen(samples.SelfContainedConsoleExe is null || !File.Exists(samples.SelfContainedConsoleExe),
            "Self-contained sample is not available on this platform.");
        var result = AssemblyLoader.Open(samples.SelfContainedConsoleExe!);
        using var analyzer = result switch
        {
            AssemblyOpenResult.Direct d => d.Analyzer,
            AssemblyOpenResult.ApphostWithCompanion ac => ac.HostAnalyzer,
            AssemblyOpenResult.BundleEntry be => be.EntryAnalyzer,
            _ => throw new InvalidOperationException("unexpected open result"),
        };

        var graph = DependencyGraphBuilder.Build(analyzer);
        Assert.Contains(graph.Nodes, n => n.IsRoot);
        Assert.Contains(graph.Nodes, n => n.Depth > 0);
    }

    /// <summary>
    /// Framework classification identifies well-known Microsoft framework assemblies even when
    /// they are resolved outside of the shared runtime directory (e.g., from app-local copies
    /// in a self-contained publish).
    /// </summary>
    [Fact(Timeout = 60_000)]
    public void FrameworkAssemblies_AreClassifiedAcrossDeploymentModels()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var graph = DependencyGraphBuilder.Build(a);

        var systemRuntime = graph.Nodes.FirstOrDefault(n => n.Name == "System.Runtime");
        Assert.NotNull(systemRuntime);
        Assert.True(graph.NavigationById[systemRuntime!.Id].IsFrameworkAssembly);
    }

}
