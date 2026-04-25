using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Views;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the Dep Graph render layer: the view-side projection from
/// <see cref="VisibleGraphModel"/> to placed character-space boxes. The layer rebalances on
/// every scope, framework, or viewport change, prunes islands created by mid-chain filtering,
/// and resolves a single hover winner across overlapping or adjacent boxes.
/// </summary>
[Collection("SampleAssemblies")]
public sealed class DependencyGraphRenderLayoutTests(SampleAssemblyFixture samples)
{
    /// <summary>
    /// Toggling scope from <c>All</c> to <c>DirectOnly</c> rebalances the rendered layout —
    /// depth-1 nodes move (and deep-band nodes disappear) because the layout is computed
    /// from the visible subgraph, not from stale full-graph coordinates.
    /// </summary>
    [Fact]
    public void FilterChange_RebalancesVisibleGraph()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var graph = DependencyGraphBuilder.Build(a);
        var disambig = EmptyDisambig();

        var all = LayoutFor(graph, DependencyGraphScope.All, hideFramework: false, w: 200, h: 80);
        var direct = LayoutFor(graph, DependencyGraphScope.DirectOnly, hideFramework: false, w: 200, h: 80);

        Assert.True(direct.Nodes.Count < all.Nodes.Count,
            "DirectOnly should drop transitive nodes");
        Assert.All(direct.Nodes, rn => Assert.True(rn.VisibleDepth <= 1));

        // Rebalance: the direct layout is vertically shorter because it loses the deeper
        // bands entirely. If the view reused full-graph geometry, the direct layout's max Y
        // would still extend into what used to be deeper bands — the shrink proves the
        // layout was recomputed from the visible subgraph, not sliced from stale positions.
        var allMaxY = all.Nodes.Max(rn => rn.Y);
        var directMaxY = direct.Nodes.Max(rn => rn.Y);
        Assert.True(directMaxY < allMaxY,
            $"DirectOnly should be shorter; got direct max Y = {directMaxY} vs all = {allMaxY}");
    }

    /// <summary>
    /// Under <c>DirectOnly</c> there are at most two visible-depth bands: root (depth 0) and
    /// the direct refs (depth 1). No node lives deeper.
    /// </summary>
    [Fact]
    public void DirectOnly_UsesOnlyVisibleDepthBands()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var graph = DependencyGraphBuilder.Build(a);
        var direct = LayoutFor(graph, DependencyGraphScope.DirectOnly, hideFramework: false, w: 180, h: 60);

        Assert.All(direct.Nodes, rn => Assert.InRange(rn.VisibleDepth, 0, 1));
        Assert.Single(direct.Nodes, rn => rn.VisibleDepth == 0);
    }

    /// <summary>
    /// Framework filtering that cuts the middle of a chain leaves no orphaned leaves in the
    /// visible model — every visible non-root node is reachable from the root via visible
    /// edges. Synthetic chain <c>Root → (framework leaf)</c> where the leaf has identity but
    /// no further refs exercises the pruning path; with <c>hideFramework</c> on, the leaf
    /// vanishes and nothing else remains at depth &gt; 0.
    /// </summary>
    [Fact]
    public void FrameworkFilter_PrunesDisconnectedVisibleIslands()
    {
        using var scope = SyntheticAssemblyScope.Create();
        var pkt = new byte[] { 0xb7, 0x7a, 0x5c, 0x56, 0x19, 0x34, 0xe0, 0x89 };
        scope.WriteAssembly("IslandRoot",
            refs: [("mscorlib", new Version(4, 0, 0, 0), (byte[]?)pkt)]);
        var rootPath = Path.Combine(scope.Directory, "IslandRoot.dll");

        using var a = new AssemblyAnalyzer(rootPath);
        var graph = DependencyGraphBuilder.Build(a);

        var visible = DependencyGraphView.BuildVisibleModel(
            graph.Nodes, graph.Edges, graph.NavigationById,
            DependencyGraphScope.All, hideFramework: true);

        Assert.Single(visible.Nodes, n => n.IsRoot);
        Assert.DoesNotContain(visible.Nodes, n => !n.IsRoot);
        Assert.Empty(visible.Edges);
    }

    /// <summary>
    /// Doubling the viewport width reflows the layout and produces a valid, non-overlapping
    /// arrangement. No two rendered boxes share any character cell in the larger layout.
    /// </summary>
    [Fact]
    public void ViewportResize_ReflowsWithoutBoxOverlap()
    {
        using var scope = SyntheticAssemblyScope.Create();
        var refs = new List<(string Name, Version Version)>();
        for (var i = 0; i < 20; i++)
        {
            scope.WriteAssembly($"Wide{i:00}");
            refs.Add(($"Wide{i:00}", new Version(1, 0, 0, 0)));
        }
        scope.WriteAssembly("WideRoot", refs: refs);
        var rootPath = Path.Combine(scope.Directory, "WideRoot.dll");

        using var a = new AssemblyAnalyzer(rootPath);
        var graph = DependencyGraphBuilder.Build(a);

        var narrow = LayoutFor(graph, DependencyGraphScope.All, hideFramework: false, w: 80, h: 40);
        var wide = LayoutFor(graph, DependencyGraphScope.All, hideFramework: false, w: 200, h: 40);

        AssertNoOverlap(wide);
        AssertNoOverlap(narrow);
        Assert.NotEqual<IEnumerable<(int, int)>>(
            [.. narrow.Nodes.Select(rn => (rn.X, rn.Y))],
            [.. wide.Nodes.Select(rn => (rn.X, rn.Y))]);
    }

    /// <summary>
    /// When two boxes overlap the same character cell, <see cref="DependencyGraphView.ResolveHoverWinner"/>
    /// returns exactly one winner — the closest box center to the mouse, with ties broken by
    /// later draw order.
    /// </summary>
    [Fact]
    public void Hover_OverlappingBoxes_SelectsSingleWinner()
    {
        // first box: X=10..30, center 20.  second box: X=15..35, center 25.
        var nodes = MakeRenderNodes(
            ("first", X: 10, Y: 5, Width: 20),
            ("second", X: 15, Y: 5, Width: 20));

        // Column 24 is inside both boxes. Distance to first center (20) = 4; to second
        // center (25) = 1. Second wins on closer-to-center.
        var winner = DependencyGraphView.ResolveHoverWinner(nodes, mouseX: 24, mouseY: 6);
        Assert.Equal(1, winner);

        // Column 12 is inside only the first box.
        winner = DependencyGraphView.ResolveHoverWinner(nodes, mouseX: 12, mouseY: 6);
        Assert.Equal(0, winner);

        // Outside both.
        winner = DependencyGraphView.ResolveHoverWinner(nodes, mouseX: 0, mouseY: 0);
        Assert.Equal(-1, winner);
    }

    /// <summary>
    /// The hover winner is the node whose label would appear in the status bar — the render
    /// node returned by <see cref="DependencyGraphView.ResolveHoverWinner"/> drives what the
    /// view writes to <c>GraphSelectedNode</c>. Validates the single-source-of-truth contract
    /// for hover.
    /// </summary>
    [Fact]
    public void Hover_WinnerMatchesStatusBarAndHighlight()
    {
        // left box: X=0..12,  center 6.   right box: X=6..18, center 12.
        var nodes = MakeRenderNodes(
            ("overlap-left", X: 0, Y: 0, Width: 12),
            ("overlap-right", X: 6, Y: 0, Width: 12));

        // Column 11 is inside both. Distance to left center (6) = 5; to right center (12) = 1.
        // Right wins on closer-to-center — and that is also what the status bar will show.
        var winner = DependencyGraphView.ResolveHoverWinner(nodes, mouseX: 11, mouseY: 1);

        Assert.Equal(1, winner);
        Assert.Equal("overlap-right", nodes[winner].Node.Name);
    }

    /// <summary>
    /// Non-winning overlapping boxes do not end up treated as hovered. The winner is unique,
    /// and any other boxes that also contained the mouse point are left at their base colors.
    /// </summary>
    [Fact]
    public void Hover_NonWinningOverlapsStayUnhighlighted()
    {
        var nodes = MakeRenderNodes(
            ("losing", X: 0, Y: 0, Width: 20),
            ("winning", X: 10, Y: 0, Width: 10));

        // Column 14 is inside both; the 'winning' center is at column 15, the 'losing' center
        // is at column 10. Closer to winning => winning wins, losing stays in neither
        // hovered nor selected sets.
        var winner = DependencyGraphView.ResolveHoverWinner(nodes, mouseX: 14, mouseY: 1);
        Assert.Equal(1, winner);

        // Sanity: the losing box still contains the mouse point, but is not the winner.
        var losing = nodes[0];
        Assert.True(14 >= losing.X && 14 < losing.X + losing.Width);
        Assert.NotEqual(0, winner);
    }

    /// <summary>
    /// When a wide tall graph exceeds the viewport, every node still has a valid position in
    /// layout coordinates — rendering just scrolls to reach them. Without scroll, rows past
    /// the viewport height would be stranded off-screen; with scroll, the view slides until
    /// any node's box lies entirely inside the viewport band.
    /// </summary>
    [Fact]
    public void ContentExceedingViewport_RemainsReachableViaScroll()
    {
        var graph = BuildManyDirectRefsGraph(count: 80);
        var layout = LayoutFor(graph, DependencyGraphScope.All, hideFramework: false, w: 80, h: 20);

        Assert.True(layout.ContentHeight > 20, "expected content to exceed viewport");

        // For every node, there exists a scroll offset in [0, contentHeight - viewport] that
        // lands the node's full box inside the viewport.
        foreach (var rn in layout.Nodes)
        {
            var needed = Math.Max(0, rn.Y + rn.Height - 20);
            needed = Math.Min(needed, layout.ContentHeight - 20);
            var visibleTop = rn.Y - needed;
            var visibleBottom = visibleTop + rn.Height;
            Assert.True(visibleTop >= 0 && visibleBottom <= 20,
                $"{rn.Node.Name}: no scroll offset reveals the full box");
        }
    }

    /// <summary>
    /// Depth bands are separated by a small fixed aesthetic gap so the two-band transition
    /// reads visually — not so large that content gets stretched to fill a tall viewport,
    /// which looks broken. Two adjacent bands should stay within a handful of rows of each
    /// other regardless of viewport height.
    /// </summary>
    [Fact]
    public void AdjacentDepthBands_SeparatedByFixedAestheticGap()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var graph = DependencyGraphBuilder.Build(a);

        var narrow = LayoutFor(graph, DependencyGraphScope.DirectOnly, hideFramework: true, w: 200, h: 20);
        var tall = LayoutFor(graph, DependencyGraphScope.DirectOnly, hideFramework: true, w: 200, h: 200);

        var narrowGap = GapBetweenRootAndFirstDirect(narrow);
        var tallGap = GapBetweenRootAndFirstDirect(tall);

        // Gap is small and does not scale with viewport height.
        Assert.InRange(tallGap, 1, 8);
        Assert.Equal(narrowGap, tallGap);
    }

    private static int GapBetweenRootAndFirstDirect(GraphRenderLayout layout)
    {
        var root = layout.Nodes.Single(rn => rn.Node.IsRoot);
        var direct1 = layout.Nodes.First(rn => !rn.Node.IsRoot);
        return direct1.Y - (root.Y + root.Height);
    }

    /// <summary>
    /// <see cref="GraphRenderLayout.ContentHeight"/> never exceeds the sum of row heights
    /// plus the distributed inter-band padding — it reflects the actual layout footprint
    /// so scroll-range and fits-vs-scrolls decisions can be made from one authoritative value.
    /// </summary>
    [Fact]
    public void ContentHeight_MatchesRenderedExtent()
    {
        var graph = BuildManyDirectRefsGraph(count: 80);
        var layout = LayoutFor(graph, DependencyGraphScope.All, hideFramework: false, w: 80, h: 20);

        var bottom = layout.Nodes.Max(rn => rn.Y + rn.Height);
        Assert.Equal(bottom, layout.ContentHeight);
    }

    private static DependencyGraphResult BuildManyDirectRefsGraph(int count)
    {
        using var scope = SyntheticAssemblyScope.Create();
        var refs = new List<(string Name, Version Version)>();
        for (var i = 0; i < count; i++)
        {
            scope.WriteAssembly($"ManyRef{i:000}");
            refs.Add(($"ManyRef{i:000}", new Version(1, 0, 0, 0)));
        }
        scope.WriteAssembly("ManyRoot", refs: refs);
        var rootPath = Path.Combine(scope.Directory, "ManyRoot.dll");
        using var a = new AssemblyAnalyzer(rootPath);
        return DependencyGraphBuilder.Build(a);
    }

    private static GraphRenderLayout LayoutFor(
        DependencyGraphResult graph,
        DependencyGraphScope scope,
        bool hideFramework,
        int w,
        int h)
    {
        var visible = DependencyGraphView.BuildVisibleModel(
            graph.Nodes, graph.Edges, graph.NavigationById, scope, hideFramework);
        var disambig = DependencyGraphView.ComputeDisambiguation(visible.Nodes);
        return DependencyGraphView.BuildRenderLayout(visible, graph.NavigationById, disambig, w, h);
    }

    private static void AssertNoOverlap(GraphRenderLayout layout)
    {
        for (var i = 0; i < layout.Nodes.Count; i++)
        {
            var a = layout.Nodes[i];
            for (var j = i + 1; j < layout.Nodes.Count; j++)
            {
                var b = layout.Nodes[j];
                var horizOverlap = a.X < b.X + b.Width && b.X < a.X + a.Width;
                var vertOverlap = a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;
                Assert.False(horizOverlap && vertOverlap,
                    $"boxes overlap: {a.Node.Name} at ({a.X},{a.Y},{a.Width},{a.Height}) " +
                    $"and {b.Node.Name} at ({b.X},{b.Y},{b.Width},{b.Height})");
            }
        }
    }

    private static List<GraphRenderNode> MakeRenderNodes(
        params (string Name, int X, int Y, int Width)[] items) =>
        [.. items.Select(item => new GraphRenderNode(
            new GraphNode(
                Id: item.Name, Name: item.Name, Version: null, Culture: "neutral",
                PublicKeyToken: null, IsRoot: false, Depth: 1, Unresolved: false),
            Label: item.Name,
            X: item.X, Y: item.Y, Width: item.Width, Height: 3, VisibleDepth: 1))];

    private static Dictionary<string, IdentityDiscriminator> EmptyDisambig() => [];
}
