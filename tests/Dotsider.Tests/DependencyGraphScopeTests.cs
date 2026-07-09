using Dotsider.Core.Analysis;
using Dotsider.Views;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the Dep Graph scope control. Scope (all / direct-only) and the framework filter
/// compose through a single visible-model projection, so rendering, search, yank, and Enter
/// all operate on the same view. Transitive-only is intentionally not offered: hiding direct
/// parents would produce disconnected islands.
/// </summary>
[TestClass]
public sealed class DependencyGraphScopeTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// <see cref="DependencyGraphScope.DirectOnly"/> keeps only the root and depth-1 nodes
    /// plus the edges between them; deeper transitive refs drop out.
    /// </summary>
    [TestMethod]
    public void DirectOnly_KeepsOnlyRootAndDepthOne()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var graph = DependencyGraphBuilder.Build(a);

        Assert.Contains(n => n.Depth > 1, graph.Nodes);

        var visible = DependencyGraphView.BuildVisibleModel(
            graph.Nodes, graph.Edges, graph.NavigationById,
            DependencyGraphScope.DirectOnly, hideFramework: false);

        TestAssert.All(visible.Nodes, n => Assert.IsTrue(n.IsRoot || n.Depth == 1));
        var rootId = visible.Nodes.First(n => n.IsRoot).Id;
        TestAssert.All(visible.Edges, e => Assert.AreEqual(rootId, e.SourceId));
        TestAssert.All(visible.Edges, e => Assert.Contains(n => n.Id == e.TargetId, visible.Nodes));
    }

    /// <summary>
    /// <see cref="DependencyGraphScope.All"/> is the default and returns the full closure
    /// unchanged — anything the builder produced is visible, edges included.
    /// </summary>
    [TestMethod]
    public void All_ReturnsFullClosure()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var graph = DependencyGraphBuilder.Build(a);

        var visible = DependencyGraphView.BuildVisibleModel(
            graph.Nodes, graph.Edges, graph.NavigationById,
            DependencyGraphScope.All, hideFramework: false);

        Assert.HasCount(graph.Nodes.Count, visible.Nodes);
        Assert.HasCount(graph.Edges.Count, visible.Edges);
    }

    /// <summary>
    /// Scope and framework filter compose: DirectOnly plus hideFramework yields only the root
    /// and the non-framework direct references. For RichLibrary that collapses to root plus
    /// Newtonsoft.Json.
    /// </summary>
    [TestMethod]
    public void DirectOnly_ComposesWithFrameworkFilter()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var graph = DependencyGraphBuilder.Build(a);

        var visible = DependencyGraphView.BuildVisibleModel(
            graph.Nodes, graph.Edges, graph.NavigationById,
            DependencyGraphScope.DirectOnly, hideFramework: true);

        Assert.Contains(n => n.IsRoot, visible.Nodes);
        TestAssert.All(visible.Nodes, n => Assert.IsTrue(n.IsRoot || n.Depth == 1));
        TestAssert.All(visible.Nodes, n =>
        {
            if (n.IsRoot) return;
            var ctx = graph.NavigationById[n.Id];
            Assert.IsFalse(ctx.IsFrameworkAssembly);
        });
    }

    /// <summary>
    /// Root stays visible under every combination of scope and framework filter, so the
    /// anchor of the graph is never lost.
    /// </summary>
    [TestMethod]
    [DataRow(DependencyGraphScope.All, false)]
    [DataRow(DependencyGraphScope.All, true)]
    [DataRow(DependencyGraphScope.DirectOnly, false)]
    [DataRow(DependencyGraphScope.DirectOnly, true)]
    public void Root_IsAlwaysVisible(DependencyGraphScope scope, bool hideFramework)
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var graph = DependencyGraphBuilder.Build(a);

        var visible = DependencyGraphView.BuildVisibleModel(
            graph.Nodes, graph.Edges, graph.NavigationById, scope, hideFramework);

        Assert.Contains(n => n.IsRoot, visible.Nodes);
    }

    /// <summary>
    /// The <see cref="VisibleGraphModel.IndexById"/> map exposes exactly the visible nodes,
    /// so index-based consumers (search, selection, yank) cannot accidentally reach into
    /// hidden nodes.
    /// </summary>
    [TestMethod]
    public void VisibleIndex_MapsOnlyVisibleNodes()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var graph = DependencyGraphBuilder.Build(a);

        var visible = DependencyGraphView.BuildVisibleModel(
            graph.Nodes, graph.Edges, graph.NavigationById,
            DependencyGraphScope.DirectOnly, hideFramework: false);

        Assert.HasCount(visible.Nodes.Count, visible.IndexById);
        foreach (var n in visible.Nodes)
            Assert.AreEqual(n.Id, visible.Nodes[visible.IndexById[n.Id]].Id);
    }
}
