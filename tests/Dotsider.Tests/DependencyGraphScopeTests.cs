using Dotsider.Core.Analysis;
using Dotsider.Views;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the Dep Graph scope control. Scope (all / direct-only) and the framework filter
/// compose through a single visible-model projection, so rendering, search, yank, and Enter
/// all operate on the same view. Transitive-only is intentionally not offered: hiding direct
/// parents would produce disconnected islands.
/// </summary>
[Collection("SampleAssemblies")]
public sealed class DependencyGraphScopeTests(SampleAssemblyFixture samples)
{
    /// <summary>
    /// <see cref="DependencyGraphScope.DirectOnly"/> keeps only the root and depth-1 nodes
    /// plus the edges between them; deeper transitive refs drop out.
    /// </summary>
    [Fact]
    public void DirectOnly_KeepsOnlyRootAndDepthOne()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var graph = DependencyGraphBuilder.Build(a);

        Assert.Contains(graph.Nodes, n => n.Depth > 1);

        var visible = DependencyGraphView.BuildVisibleModel(
            graph.Nodes, graph.Edges, graph.NavigationById,
            DependencyGraphScope.DirectOnly, hideFramework: false);

        Assert.All(visible.Nodes, n => Assert.True(n.IsRoot || n.Depth == 1));
        var rootId = visible.Nodes.First(n => n.IsRoot).Id;
        Assert.All(visible.Edges, e => Assert.Equal(rootId, e.SourceId));
        Assert.All(visible.Edges, e => Assert.Contains(visible.Nodes, n => n.Id == e.TargetId));
    }

    /// <summary>
    /// <see cref="DependencyGraphScope.All"/> is the default and returns the full closure
    /// unchanged — anything the builder produced is visible, edges included.
    /// </summary>
    [Fact]
    public void All_ReturnsFullClosure()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var graph = DependencyGraphBuilder.Build(a);

        var visible = DependencyGraphView.BuildVisibleModel(
            graph.Nodes, graph.Edges, graph.NavigationById,
            DependencyGraphScope.All, hideFramework: false);

        Assert.Equal(graph.Nodes.Count, visible.Nodes.Count);
        Assert.Equal(graph.Edges.Count, visible.Edges.Count);
    }

    /// <summary>
    /// Scope and framework filter compose: DirectOnly plus hideFramework yields only the root
    /// and the non-framework direct references. For RichLibrary that collapses to root plus
    /// Newtonsoft.Json.
    /// </summary>
    [Fact]
    public void DirectOnly_ComposesWithFrameworkFilter()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var graph = DependencyGraphBuilder.Build(a);

        var visible = DependencyGraphView.BuildVisibleModel(
            graph.Nodes, graph.Edges, graph.NavigationById,
            DependencyGraphScope.DirectOnly, hideFramework: true);

        Assert.Contains(visible.Nodes, n => n.IsRoot);
        Assert.All(visible.Nodes, n => Assert.True(n.IsRoot || n.Depth == 1));
        Assert.All(visible.Nodes, n =>
        {
            if (n.IsRoot) return;
            var ctx = graph.NavigationById[n.Id];
            Assert.False(ctx.IsFrameworkAssembly);
        });
    }

    /// <summary>
    /// Root stays visible under every combination of scope and framework filter, so the
    /// anchor of the graph is never lost.
    /// </summary>
    [Theory]
    [InlineData(DependencyGraphScope.All, false)]
    [InlineData(DependencyGraphScope.All, true)]
    [InlineData(DependencyGraphScope.DirectOnly, false)]
    [InlineData(DependencyGraphScope.DirectOnly, true)]
    public void Root_IsAlwaysVisible(DependencyGraphScope scope, bool hideFramework)
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var graph = DependencyGraphBuilder.Build(a);

        var visible = DependencyGraphView.BuildVisibleModel(
            graph.Nodes, graph.Edges, graph.NavigationById, scope, hideFramework);

        Assert.Contains(visible.Nodes, n => n.IsRoot);
    }

    /// <summary>
    /// The <see cref="VisibleGraphModel.IndexById"/> map exposes exactly the visible nodes,
    /// so index-based consumers (search, selection, yank) cannot accidentally reach into
    /// hidden nodes.
    /// </summary>
    [Fact]
    public void VisibleIndex_MapsOnlyVisibleNodes()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var graph = DependencyGraphBuilder.Build(a);

        var visible = DependencyGraphView.BuildVisibleModel(
            graph.Nodes, graph.Edges, graph.NavigationById,
            DependencyGraphScope.DirectOnly, hideFramework: false);

        Assert.Equal(visible.Nodes.Count, visible.IndexById.Count);
        foreach (var n in visible.Nodes)
            Assert.Equal(n.Id, visible.Nodes[visible.IndexById[n.Id]].Id);
    }
}
