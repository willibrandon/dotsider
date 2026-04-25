using System.Text;
using Dotsider.Core.Analysis.Models;
using Hex1b;
using Hex1b.Input;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Builds the Dependency Graph tab (Tab 6). Renders the full transitive dependency closure
/// rooted at the analyzed assembly with id-based edge lookup, provenance-based framework
/// filtering, per-node Enter navigation through stored resolution context, and label
/// disambiguation for simple-name collisions.
/// </summary>
public static class DependencyGraphView
{
    private static readonly Hex1bColor RootColor = Hex1bColor.FromRgb(0, 200, 180);
    private static readonly Hex1bColor RefColor = Hex1bColor.FromRgb(100, 130, 180);
    private static readonly Hex1bColor UnresolvedColor = Hex1bColor.FromRgb(120, 100, 100);
    private static readonly Hex1bColor EdgeColor = Hex1bColor.FromRgb(80, 80, 100);
    private static readonly Hex1bColor HighlightColor = Hex1bColor.FromRgb(255, 220, 100);
    private static readonly Hex1bColor SelectionBorder = Hex1bColor.FromRgb(255, 255, 255);

    /// <summary>
    /// Builds the Dependency Graph view widget tree.
    /// </summary>
    /// <param name="ctx">The widget context for building widgets.</param>
    /// <param name="state">The shared application state.</param>
    /// <returns>The root widget for the Dependency Graph tab.</returns>
    public static Hex1bWidget Build(WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        // Kick off (or no-op on) a background build so opening tab 6 on a large assembly
        // does not stall the render loop. The Interactable tree below is always rendered
        // — even while the build runs — so keyboard focus works from the first frame and
        // existing startup-focus tests still land on the graph surface.
        state.EnsureCachedGraphAsync();

        var ready = state.CachedGraph is not null;
        IReadOnlyList<GraphNode> allNodes = ready ? state.CachedGraph!.Value.Nodes : [];
        IReadOnlyList<GraphEdge> allEdges = ready ? state.CachedGraph!.Value.Edges : [];
        var nav = state.GraphNavigation;
        var visible = BuildVisibleModel(
            allNodes, allEdges, nav, state.DepGraphScope, state.DepGraphHideFramework);
        var nodes = visible.Nodes;
        var edges = visible.Edges;
        var indexById = visible.IndexById;
        var disambig = ComputeDisambiguation(nodes);

        // Clamp selection index to current visible size. Preserve -1 (no selection) so
        // arrow keys advance from "none" to the first node, matching existing behavior.
        if (state.GraphSelectedIndex >= nodes.Count)
            state.GraphSelectedIndex = nodes.Count > 0 ? 0 : -1;

        var search = state.Search[TabId.DepGraph];
        var query = search.Query;

        var matchingNodes = new List<int>();
        if (!string.IsNullOrEmpty(query))
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                    matchingNodes.Add(i);
            }
            search.SetMatchCount(matchingNodes.Count);
        }

        if (state.GraphMatchIndex >= matchingNodes.Count)
            state.GraphMatchIndex = matchingNodes.Count > 0 ? 0 : -1;

        if (state.CurrentTab == TabId.DepGraph)
        {
            state.NavigateNextMatch = matchingNodes.Count > 0 ? () =>
            {
                state.GraphMatchIndex = (state.GraphMatchIndex + 1) % matchingNodes.Count;
            }
            : null;
            state.NavigatePrevMatch = matchingNodes.Count > 0 ? () =>
            {
                state.GraphMatchIndex = state.GraphMatchIndex <= 0
                    ? matchingNodes.Count - 1 : state.GraphMatchIndex - 1;
            }
            : null;
        }

        var totalNodes = allNodes.Count;
        var totalEdges = allEdges.Count;
        var hiddenNodes = totalNodes - nodes.Count;

        return ctx.VStack(outer =>
        {
            var widgets = new List<Hex1bWidget>();

            var displayNode = state.GraphSelectedNode;
            if (displayNode is null && state.GraphSelectedIndex >= 0
                && state.GraphSelectedIndex < nodes.Count)
            {
                displayNode = FormatLabel(nodes[state.GraphSelectedIndex], disambig);
            }
            if (displayNode is null && state.GraphMatchIndex >= 0
                && state.GraphMatchIndex < matchingNodes.Count)
            {
                displayNode = FormatLabel(nodes[matchingNodes[state.GraphMatchIndex]], disambig);
            }

            string baseCounts = hiddenNodes > 0
                ? $"Nodes: {nodes.Count}/{totalNodes}  Edges: {edges.Count}/{totalEdges}  Hidden: {hiddenNodes}"
                : $"Nodes: {nodes.Count}  Edges: {edges.Count}";

            string scrollSuffix = string.Empty;
            if (ready && state.CachedGraphRenderLayout is { } laidOut
                && state.CachedGraphRenderLayoutKey is { } laidOutKey)
            {
                var maxScroll = Math.Max(0, laidOut.ContentHeight - laidOutKey.Height);
                if (maxScroll > 0)
                    scrollSuffix = $"  Scroll: {Math.Clamp(state.DepGraphScrollY, 0, maxScroll)}/{maxScroll}";
            }

            var statusLeft = !ready
                ? " Building dependency graph..."
                : $" {baseCounts}{scrollSuffix}";

            string scopeSuffix, filterSuffix;
            if (!ready)
            {
                scopeSuffix = string.Empty;
                filterSuffix = string.Empty;
            }
            else
            {
                scopeSuffix = state.DepGraphScope == DependencyGraphScope.DirectOnly
                    ? "  [scope: direct]"
                    : "  [d: scope all]";
                filterSuffix = state.DepGraphHideFramework
                    ? "  [filter: f]"
                    : "  [f: hide framework]";
            }

            widgets.Add(outer.HStack(row =>
            [
                row.Text(statusLeft + scopeSuffix + filterSuffix),
                row.Text(displayNode is not null
                    ? $"  | {displayNode}"
                    : "  | Hover over a node for details").Fill()
            ]).FixedHeight(1));

            if (state.GraphNavigationError is not null)
                widgets.Add(outer.Text($" {state.GraphNavigationError}").FixedHeight(1));

            SearchBarHelper.AddSearchBar(widgets, outer, search, state.App);

            widgets.Add(outer.Interactable(ic =>
                ic.Surface(s =>
                [
                    s.Layer(surface => DrawGraph(surface, visible, nav, disambig,
                        state, s.MouseX, s.MouseY, query))
                ]).Fill()
            ).WithInputBindings(bindings =>
            {
                bindings.Key(Hex1bKey.RightArrow).Action(_ =>
                {
                    if (nodes.Count > 0)
                    {
                        state.GraphSelectedIndex = (state.GraphSelectedIndex + 1) % nodes.Count;
                        ScrollSelectionIntoView(state);
                        state.App.Invalidate();
                    }
                }, "Next node");

                bindings.Key(Hex1bKey.LeftArrow).Action(_ =>
                {
                    if (nodes.Count > 0)
                    {
                        state.GraphSelectedIndex = state.GraphSelectedIndex <= 0
                            ? nodes.Count - 1
                            : state.GraphSelectedIndex - 1;
                        ScrollSelectionIntoView(state);
                        state.App.Invalidate();
                    }
                }, "Previous node");

                bindings.Key(Hex1bKey.UpArrow).Action(_ =>
                {
                    state.DepGraphScrollY = Math.Max(0, state.DepGraphScrollY - 1);
                    state.App.Invalidate();
                }, "Scroll up");

                bindings.Key(Hex1bKey.DownArrow).Action(_ =>
                {
                    state.DepGraphScrollY += 1;
                    state.App.Invalidate();
                }, "Scroll down");

                bindings.Key(Hex1bKey.PageUp).Action(_ =>
                {
                    var page = Math.Max(1, state.CachedGraphRenderLayoutKey?.Height ?? 20);
                    state.DepGraphScrollY = Math.Max(0, state.DepGraphScrollY - page);
                    state.App.Invalidate();
                }, "Scroll up one page");

                bindings.Key(Hex1bKey.PageDown).Action(_ =>
                {
                    var page = Math.Max(1, state.CachedGraphRenderLayoutKey?.Height ?? 20);
                    state.DepGraphScrollY += page;
                    state.App.Invalidate();
                }, "Scroll down one page");

                bindings.Key(Hex1bKey.Home).Action(_ =>
                {
                    state.DepGraphScrollY = 0;
                    state.App.Invalidate();
                }, "Scroll to top");

                bindings.Key(Hex1bKey.End).Action(_ =>
                {
                    state.DepGraphScrollY = int.MaxValue;
                    state.App.Invalidate();
                }, "Scroll to bottom");

                bindings.Key(Hex1bKey.F).Action(_ =>
                {
                    state.DepGraphHideFramework = !state.DepGraphHideFramework;
                    state.GraphSelectedIndex = -1;
                    state.GraphMatchIndex = -1;
                    state.GraphNavigationError = null;
                    state.DepGraphScrollY = 0;
                    state.App.Invalidate();
                }, "Toggle framework filter");

                bindings.Key(Hex1bKey.D).Action(_ =>
                {
                    state.DepGraphScope = state.DepGraphScope == DependencyGraphScope.DirectOnly
                        ? DependencyGraphScope.All
                        : DependencyGraphScope.DirectOnly;
                    state.GraphSelectedIndex = -1;
                    state.GraphMatchIndex = -1;
                    state.GraphNavigationError = null;
                    state.DepGraphScrollY = 0;
                    state.App.Invalidate();
                }, "Toggle scope (all / direct)");

                bindings.Key(Hex1bKey.Enter).Action(_ =>
                {
                    if (state.GraphSelectedIndex < 0 || state.GraphSelectedIndex >= nodes.Count)
                        return;

                    var node = nodes[state.GraphSelectedIndex];

                    if (node.IsRoot)
                        return;

                    if (nav is null || !nav.TryGetValue(node.Id, out var nctx))
                    {
                        state.GraphNavigationError = $"{node.Name}: navigation context missing";
                        state.App.Invalidate();
                        return;
                    }

                    if (nctx.Resolved is null)
                    {
                        state.GraphNavigationError = nctx.Provenance switch
                        {
                            AssemblyProvenance.IdentityMismatch =>
                                $"{node.Name}: identity mismatch against {nctx.CandidateProbePath ?? "(unknown)"}",
                            AssemblyProvenance.CodeBaseMissing =>
                                $"{node.Name}: codeBase href not found: {nctx.CandidateProbePath ?? "(unknown)"}",
                            _ => $"{node.Name}: not resolvable",
                        };
                        state.App.Invalidate();
                        return;
                    }

                    if (state.PushAssembly(nctx.Resolved))
                    {
                        state.GraphNavigationError = null;
                        state.NavigateToTab(TabId.General);
                        state.App.RequestFocus(n =>
                            n.GetType().Name.StartsWith("TableNode"));
                        state.App.Invalidate();
                    }
                }, "Open assembly");
            }).Fill());

            return [.. widgets];
        })
        .WithInputBindings(bindings =>
        {
            bindings.Key(Hex1bKey.Escape).OverridesCapture().Action(_ =>
            {
                if (search.IsActive)
                {
                    search.Dismiss();
                    state.App.Invalidate();
                }
            }, "Esc");
        })
        .Fill();
    }

    private static void DrawGraph(
        Surface surface,
        VisibleGraphModel visible,
        IReadOnlyDictionary<string, GraphNavigationContext>? nav,
        IReadOnlyDictionary<string, IdentityDiscriminator> disambig,
        DotsiderState state,
        int mouseX,
        int mouseY,
        string? query)
    {
        var w = surface.Width;
        var h = surface.Height;
        if (w < 10 || h < 5) return;

        var layout = GetOrBuildRenderLayout(state, visible, nav, disambig, w, h);
        var renderNodes = layout.Nodes;
        if (renderNodes.Count == 0) return;

        var maxScroll = Math.Max(0, layout.ContentHeight - h);
        var selectedIndex = state.GraphSelectedIndex;

        // Clamp but do not force-follow the selection. Forcing every frame would fight user
        // scroll: pressing End or PageDown while a node is selected would snap scroll back
        // to the selected node's Y and never actually reach the bottom. Selection-follow
        // runs only when the user moves the selection, inside the Left/Right arrow handlers.
        state.DepGraphScrollY = Math.Clamp(state.DepGraphScrollY, 0, maxScroll);
        var scrollY = state.DepGraphScrollY;

        // Hover resolution works in layout space, so translate the mouse's surface Y back up
        // into layout coordinates. Skip hover entirely when the mouse is outside the surface.
        var layoutMouseY = mouseY + scrollY;
        var hoverWinner = ResolveHoverWinner(renderNodes, mouseX, layoutMouseY);

        var hasQuery = !string.IsNullOrEmpty(query);

        foreach (var edge in layout.Edges)
        {
            if (!layout.IndexById.TryGetValue(edge.SourceId, out var srcIdx)) continue;
            if (!layout.IndexById.TryGetValue(edge.TargetId, out var tgtIdx)) continue;

            var src = renderNodes[srcIdx];
            var tgt = renderNodes[tgtIdx];

            // Skip back-edges (cycles and same-level references). Their natural routing has
            // to go through the source's bottom row, which scatters stray `│` fragments at
            // the last visible row of the canvas and reads as phantom content beyond the
            // viewport — indistinguishable from "scroll isn't at the bottom yet." Cycle and
            // diamond presence is still visible through node multiplicity; only the edge
            // glyph is suppressed.
            if (tgt.Y <= src.Y + src.Height) continue;

            var x1 = src.X + src.Width / 2;
            var y1 = src.Y + src.Height - scrollY;
            var x2 = tgt.X + tgt.Width / 2;
            var y2 = tgt.Y - 1 - scrollY;
            if (y2 < 0 && y1 < 0) continue;
            if (y1 >= h && y2 >= h) continue;

            var midY = (y1 + y2) / 2;
            var edgeC = hasQuery ? HighlightHelper.DimColor :
                edge.TypeRefCount > 10 ? Hex1bColor.FromRgb(140, 140, 180) : EdgeColor;

            for (var y = Math.Min(y1, midY); y <= Math.Max(y1, midY); y++)
                if (y >= 0 && y < h) surface.WriteChar(x1, y, '│', edgeC);
            for (var x = Math.Min(x1, x2); x <= Math.Max(x1, x2); x++)
                if (midY >= 0 && midY < h) surface.WriteChar(x, midY, '─', edgeC);
            for (var y = Math.Min(midY, y2); y <= Math.Max(midY, y2); y++)
                if (y >= 0 && y < h) surface.WriteChar(x2, y, '│', edgeC);

            if (x1 != x2 && midY >= 0 && midY < h)
            {
                surface.WriteChar(x1, midY, x1 < x2 ? '└' : '┘', edgeC);
                surface.WriteChar(x2, midY, x1 < x2 ? '┐' : '┌', edgeC);
            }
        }

        // First pass: draw every non-winner node. Second pass: draw the winner on top so its
        // highlight is never overwritten by an overlapping neighbor. GraphSelectedNode is set
        // exactly once, from the winning node.
        state.GraphSelectedNode = null;
        for (var i = 0; i < renderNodes.Count; i++)
        {
            if (i == hoverWinner) continue;
            DrawRenderNode(surface, renderNodes[i], scrollY, h,
                isSelected: i == selectedIndex, isHovered: false, query, hasQuery);
        }
        if (hoverWinner >= 0)
        {
            var node = renderNodes[hoverWinner];
            DrawRenderNode(surface, node, scrollY, h,
                isSelected: hoverWinner == selectedIndex, isHovered: true, query, hasQuery);
            state.GraphSelectedNode = FormatLabel(node.Node, disambig);
        }
        else if (selectedIndex >= 0 && selectedIndex < renderNodes.Count)
        {
            // No hover winner — fall back to keyboard selection for the status line so the
            // user can see what they're about to open with Enter.
            state.GraphSelectedNode = FormatLabel(renderNodes[selectedIndex].Node, disambig);
        }
    }

    private static void DrawRenderNode(
        Surface surface, GraphRenderNode rn, int scrollY, int surfaceHeight,
        bool isSelected, bool isHovered, string? query, bool hasQuery)
    {
        var node = rn.Node;
        var x0 = rn.X;
        var y0 = rn.Y - scrollY;
        var boxW = rn.Width;

        if (y0 + rn.Height <= 0 || y0 >= surfaceHeight) return;

        var isMatch = hasQuery && node.Name.Contains(query!, StringComparison.OrdinalIgnoreCase);
        Hex1bColor baseColor = node.Unresolved ? UnresolvedColor
            : node.IsRoot ? RootColor : RefColor;
        var bg = isHovered ? HighlightColor
            : isMatch ? baseColor
            : hasQuery ? HighlightHelper.DimColor
            : baseColor;
        var fg = Hex1bColor.Black;
        var borderColor = isSelected ? SelectionBorder : bg;

        void Put(int x, int y, char ch, Hex1bColor c)
        {
            if (y < 0 || y >= surfaceHeight) return;
            surface.WriteChar(x, y, ch, c);
        }
        void Fill(int x, int y, char ch, Hex1bColor fgc, Hex1bColor bgc)
        {
            if (y < 0 || y >= surfaceHeight) return;
            surface.WriteChar(x, y, ch, fgc, bgc);
        }

        Put(x0, y0, '┌', borderColor);
        Put(x0 + boxW - 1, y0, '┐', borderColor);
        Put(x0, y0 + 2, '└', borderColor);
        Put(x0 + boxW - 1, y0 + 2, '┘', borderColor);
        for (var x = x0 + 1; x < x0 + boxW - 1; x++)
        {
            Put(x, y0, '─', borderColor);
            Put(x, y0 + 2, '─', borderColor);
        }
        Put(x0, y0 + 1, '│', borderColor);
        Put(x0 + boxW - 1, y0 + 1, '│', borderColor);

        for (var x = x0 + 1; x < x0 + boxW - 1; x++)
            Fill(x, y0 + 1, ' ', fg, bg);

        if (y0 + 1 >= 0 && y0 + 1 < surfaceHeight)
        {
            var truncLabel = rn.Label.Length > boxW - 2 ? rn.Label[..(boxW - 4)] + ".." : rn.Label;
            surface.WriteText(x0 + 1, y0 + 1, truncLabel, fg, bg);
        }
    }

    private static void ScrollSelectionIntoView(DotsiderState state)
    {
        // Pull the vertical scroll just enough to reveal the selected node. Runs in response
        // to a selection change (Left/Right arrow) rather than every frame, so explicit
        // scroll input (End, PageDown, Home, etc.) is never overridden while the selection
        // happens to be offscreen.
        if (state.CachedGraphRenderLayout is not { } layout) return;
        if (state.CachedGraphRenderLayoutKey is not { } key) return;
        var i = state.GraphSelectedIndex;
        if (i < 0 || i >= layout.Nodes.Count) return;

        var sel = layout.Nodes[i];
        if (sel.Y < state.DepGraphScrollY)
            state.DepGraphScrollY = sel.Y;
        else if (sel.Y + sel.Height > state.DepGraphScrollY + key.Height)
            state.DepGraphScrollY = sel.Y + sel.Height - key.Height;
    }

    private static GraphRenderLayout GetOrBuildRenderLayout(
        DotsiderState state,
        VisibleGraphModel visible,
        IReadOnlyDictionary<string, GraphNavigationContext>? nav,
        IReadOnlyDictionary<string, IdentityDiscriminator> disambig,
        int width,
        int height)
    {
        object? nodesRef = state.CachedGraph is { } cg ? cg.Nodes : null;
        var key = new GraphRenderLayoutKey(
            nodesRef, state.DepGraphScope, state.DepGraphHideFramework, width, height);

        if (state.CachedGraphRenderLayoutKey is { } existingKey
            && existingKey.Equals(key)
            && state.CachedGraphRenderLayout is { } existingLayout)
        {
            return existingLayout;
        }

        var built = BuildRenderLayout(visible, nav, disambig, width, height);
        state.CachedGraphRenderLayout = built;
        state.CachedGraphRenderLayoutKey = key;
        return built;
    }

    /// <summary>
    /// Computes the single visible projection used by rendering, search, selection, yank,
    /// and Enter navigation. Two orthogonal controls narrow the graph: <paramref name="scope"/>
    /// selects which nodes are in the view at all (all depths, or root + direct refs only),
    /// and <paramref name="hideFramework"/> hides nodes classified as .NET framework assemblies
    /// on top of whatever scope is active. The root is always visible regardless of the
    /// controls.
    /// </summary>
    /// <param name="allNodes">All nodes from the cached full graph.</param>
    /// <param name="allEdges">All edges from the cached full graph.</param>
    /// <param name="nav">Navigation metadata keyed by node id, or <see langword="null"/> while a build is in flight.</param>
    /// <param name="scope">The scope control — <c>All</c> or <c>DirectOnly</c>.</param>
    /// <param name="hideFramework">Whether the framework-filter toggle is active.</param>
    /// <returns>The filtered view. All downstream operations must be indexed against this model.</returns>
    internal static VisibleGraphModel BuildVisibleModel(
        IReadOnlyList<GraphNode> allNodes,
        IReadOnlyList<GraphEdge> allEdges,
        IReadOnlyDictionary<string, GraphNavigationContext>? nav,
        DependencyGraphScope scope,
        bool hideFramework)
    {
        var visibleIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var n in allNodes)
        {
            if (n.IsRoot)
            {
                visibleIds.Add(n.Id);
                continue;
            }

            if (scope == DependencyGraphScope.DirectOnly && n.Depth != 1)
                continue;

            if (hideFramework && nav is not null
                && nav.TryGetValue(n.Id, out var ctx) && ctx.IsFrameworkAssembly)
                continue;

            visibleIds.Add(n.Id);
        }

        var rootId = allNodes.FirstOrDefault(n => n.IsRoot)?.Id;

        // Under DirectOnly, restrict edges to root→direct only. Sibling edges between two
        // depth-1 nodes exist in the closure but would feel out of place in a "direct
        // dependencies" view — those cross-references are a transitive-closure concern.
        IEnumerable<GraphEdge> edgeSource = allEdges
            .Where(e => visibleIds.Contains(e.SourceId) && visibleIds.Contains(e.TargetId));
        if (scope == DependencyGraphScope.DirectOnly && rootId is not null)
            edgeSource = edgeSource.Where(e => e.SourceId == rootId);
        var preReachEdges = edgeSource.ToList();

        // Prune to nodes reachable from the root through the visible edges. When the framework
        // filter cuts through the middle of a chain — e.g. root → (hidden framework) → leaf —
        // the leaf becomes a disconnected island. Keeping it would show an explanationless box
        // drifting on the canvas; dropping it keeps the rooted subgraph coherent.
        var reachable = new HashSet<string>(StringComparer.Ordinal);
        if (rootId is not null && visibleIds.Contains(rootId))
        {
            reachable.Add(rootId);
            var outgoingBySource = preReachEdges
                .GroupBy(e => e.SourceId, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Select(e => e.TargetId).ToList(), StringComparer.Ordinal);
            var queue = new Queue<string>();
            queue.Enqueue(rootId);
            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                if (!outgoingBySource.TryGetValue(cur, out var children)) continue;
                foreach (var childId in children)
                {
                    if (reachable.Add(childId)) queue.Enqueue(childId);
                }
            }
        }

        var visibleNodes = allNodes.Where(n => reachable.Contains(n.Id)).ToList();
        var visibleEdges = preReachEdges
            .Where(e => reachable.Contains(e.SourceId) && reachable.Contains(e.TargetId))
            .ToList();

        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < visibleNodes.Count; i++)
            index[visibleNodes[i].Id] = i;

        return new VisibleGraphModel(visibleNodes, visibleEdges, index);
    }

    /// <summary>
    /// Projects a <see cref="VisibleGraphModel"/> into placed character-space boxes for the
    /// supplied viewport. Recomputes BFS depth on the visible edge set so DirectOnly and
    /// filtered views collapse into a compact rooted layout without inheriting stale positions
    /// from the full-graph layout. Packs each depth band into rows of actual box widths plus
    /// a fixed 2-column horizontal gap and wraps to a new sub-row when the next box would
    /// overflow the canvas.
    /// </summary>
    /// <param name="visible">The topology-filtered visible graph.</param>
    /// <param name="nav">Navigation metadata keyed by node id.</param>
    /// <param name="disambig">Collision discriminator map used by the label formatter.</param>
    /// <param name="width">Surface width in columns.</param>
    /// <param name="height">Surface height in rows.</param>
    /// <returns>The placed render layout. Empty when the visible graph has no root.</returns>
    internal static GraphRenderLayout BuildRenderLayout(
        VisibleGraphModel visible,
        IReadOnlyDictionary<string, GraphNavigationContext>? nav,
        IReadOnlyDictionary<string, IdentityDiscriminator> disambig,
        int width,
        int height)
    {
        const int horizontalGap = 2;
        const int verticalGap = 1;
        const int boxHeight = 3;
        const int minBoxWidth = 6;
        const int boxPadding = 2;

        if (visible.Nodes.Count == 0 || width <= 0 || height <= 0)
        {
            return new GraphRenderLayout(
                [], visible.Edges, new Dictionary<string, int>(StringComparer.Ordinal), ContentHeight: 0);
        }

        var rootId = visible.Nodes.FirstOrDefault(n => n.IsRoot)?.Id;
        if (rootId is null)
        {
            return new GraphRenderLayout(
                [], visible.Edges, new Dictionary<string, int>(StringComparer.Ordinal), ContentHeight: 0);
        }

        // Compute each node's rendered label up-front so box widths reflect what will
        // actually be drawn — matches the user-visible collision discriminator.
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        var widths = new Dictionary<string, int>(StringComparer.Ordinal);
        var maxBoxWidth = Math.Max(minBoxWidth, width - 2);
        foreach (var n in visible.Nodes)
        {
            var ctx = nav is not null && nav.TryGetValue(n.Id, out var c) ? c : null;
            var label = FormatNodeBoxLabel(n, ctx, disambig);
            labels[n.Id] = label;
            var desired = label.Length + boxPadding;
            widths[n.Id] = Math.Clamp(desired, minBoxWidth, maxBoxWidth);
        }

        // BFS depth from root on the visible edge set. Gives DirectOnly a single band below
        // root and lets framework-filtered graphs rebalance after islands are pruned.
        var outgoingBySource = visible.Edges
            .GroupBy(e => e.SourceId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(e => e.TargetId).ToList(), StringComparer.Ordinal);

        var visibleDepth = new Dictionary<string, int>(StringComparer.Ordinal) { [rootId] = 0 };
        var bfs = new Queue<string>();
        bfs.Enqueue(rootId);
        while (bfs.Count > 0)
        {
            var cur = bfs.Dequeue();
            if (!outgoingBySource.TryGetValue(cur, out var children)) continue;
            foreach (var childId in children)
            {
                if (visibleDepth.ContainsKey(childId)) continue;
                visibleDepth[childId] = visibleDepth[cur] + 1;
                bfs.Enqueue(childId);
            }
        }

        // Preserve the stable builder order (visible.Nodes comes from the builder's
        // deterministic ordering) so toggling filters does not jitter sibling placement.
        var nodeOrder = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < visible.Nodes.Count; i++)
            nodeOrder[visible.Nodes[i].Id] = i;

        var depthGroups = visibleDepth
            .GroupBy(kv => kv.Value, kv => kv.Key)
            .OrderBy(g => g.Key)
            .ToList();

        // Phase 1: pack each depth band into rows keyed by actual label widths.
        var bandRows = new List<List<List<string>>>();
        foreach (var group in depthGroups)
        {
            var idsInDepth = group
                .OrderBy(id => nodeOrder.TryGetValue(id, out var o) ? o : int.MaxValue)
                .ToList();

            var rows = new List<List<string>>();
            var currentRow = new List<string>();
            var currentRowWidth = 0;
            foreach (var id in idsInDepth)
            {
                var w = widths[id];
                var added = currentRow.Count == 0 ? w : currentRowWidth + horizontalGap + w;
                if (added > width && currentRow.Count > 0)
                {
                    rows.Add(currentRow);
                    currentRow = [id];
                    currentRowWidth = w;
                }
                else
                {
                    currentRow.Add(id);
                    currentRowWidth = added;
                }
            }
            if (currentRow.Count > 0) rows.Add(currentRow);
            bandRows.Add(rows);
        }

        // Phase 2: vertical spacing. Within a band sub-rows are separated by verticalGap (1
        // row). Between depth bands a fixed aesthetic gap gives visual separation without
        // stretching the graph to fill every pixel of the viewport — stretching pushes
        // direct children to the bottom of the canvas on small graphs, which reads as
        // broken rather than rebalanced. If the aesthetic gap would itself overflow a small
        // viewport, shrink it but never below verticalGap so bands stay distinguishable.
        const int defaultInterBandExtra = 3;
        var totalRows = bandRows.Sum(b => b.Count);
        var bandCount = bandRows.Count;
        var tightHeight = totalRows * boxHeight + Math.Max(0, totalRows - 1) * verticalGap;

        var interBandExtra = defaultInterBandExtra;
        if (bandCount > 1)
        {
            var roomForExtras = height - tightHeight;
            if (roomForExtras < defaultInterBandExtra * (bandCount - 1))
            {
                var perGap = roomForExtras / (bandCount - 1);
                interBandExtra = Math.Max(0, perGap);
            }
        }
        else
        {
            interBandExtra = 0;
        }

        // Phase 3: assign positions.
        var renderNodes = new List<GraphRenderNode>();
        var indexById = new Dictionary<string, int>(StringComparer.Ordinal);
        var yCursor = 0;
        var contentHeight = 0;

        for (var b = 0; b < bandCount; b++)
        {
            var rows = bandRows[b];
            for (var r = 0; r < rows.Count; r++)
            {
                var row = rows[r];
                var rowWidth = row.Sum(id => widths[id]) + horizontalGap * Math.Max(0, row.Count - 1);
                var xStart = Math.Max(0, (width - rowWidth) / 2);
                var x = xStart;
                foreach (var id in row)
                {
                    var w = widths[id];
                    var depth = visibleDepth[id];
                    var node = visible.Nodes[visible.IndexById[id]];
                    var rn = new GraphRenderNode(node, labels[id], x, yCursor, w, boxHeight, depth);
                    indexById[id] = renderNodes.Count;
                    renderNodes.Add(rn);
                    x += w + horizontalGap;
                }
                contentHeight = yCursor + boxHeight;
                yCursor = contentHeight + verticalGap;
            }

            if (b < bandCount - 1)
                yCursor += interBandExtra;
        }

        return new GraphRenderLayout(renderNodes, visible.Edges, indexById, contentHeight);
    }

    /// <summary>
    /// Resolves a single hover winner for the supplied mouse position. Candidates are all
    /// render nodes whose bounding box contains the mouse; the winner is the candidate with
    /// the smallest distance from the mouse to its box center, with ties broken by draw
    /// order (later wins). Returns <c>-1</c> when no box contains the mouse.
    /// </summary>
    internal static int ResolveHoverWinner(
        IReadOnlyList<GraphRenderNode> nodes, int mouseX, int mouseY)
    {
        var winner = -1;
        var bestDist = double.MaxValue;
        for (var i = 0; i < nodes.Count; i++)
        {
            var rn = nodes[i];
            if (mouseX < rn.X || mouseX >= rn.X + rn.Width) continue;
            if (mouseY < rn.Y || mouseY >= rn.Y + rn.Height) continue;
            var cx = rn.X + rn.Width / 2.0;
            var cy = rn.Y + rn.Height / 2.0;
            var dx = mouseX - cx;
            var dy = mouseY - cy;
            var d = dx * dx + dy * dy;
            if (d <= bestDist)
            {
                bestDist = d;
                winner = i;
            }
        }
        return winner;
    }

    internal static IReadOnlyDictionary<string, IdentityDiscriminator> ComputeDisambiguation(
        IReadOnlyList<GraphNode> nodes)
    {
        var result = new Dictionary<string, IdentityDiscriminator>(StringComparer.OrdinalIgnoreCase);
        var groups = nodes.GroupBy(n => n.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            var list = group.ToList();
            if (list.Count <= 1) continue;
            var versionDiffers = list.Select(n => n.Version ?? string.Empty)
                .Distinct(StringComparer.Ordinal).Count() > 1;
            var cultureDiffers = list.Select(n => n.Culture)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1;
            var pktDiffers = list.Select(n => n.PublicKeyToken ?? string.Empty)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1;
            result[group.Key] = new IdentityDiscriminator(versionDiffers, cultureDiffers, pktDiffers);
        }
        return result;
    }

    internal static string FormatLabel(
        GraphNode node,
        IReadOnlyDictionary<string, IdentityDiscriminator> disambig)
    {
        if (!disambig.TryGetValue(node.Name, out var fields))
        {
            return node.Version is not null ? $"{node.Name} v{node.Version}" : node.Name;
        }

        var sb = new StringBuilder(node.Name);
        if (fields.IncludeVersion && node.Version is not null) sb.Append(" v").Append(node.Version);
        if (fields.IncludeCulture) sb.Append(" [").Append(node.Culture).Append(']');
        if (fields.IncludePkt && node.PublicKeyToken is not null)
        {
            var pkt = node.PublicKeyToken.Length > 8 ? node.PublicKeyToken[..8] : node.PublicKeyToken;
            sb.Append(" (").Append(pkt).Append(')');
        }
        return sb.ToString();
    }

    private static string FormatNodeBoxLabel(
        GraphNode node,
        GraphNavigationContext? ctx,
        IReadOnlyDictionary<string, IdentityDiscriminator> disambig)
    {
        var prefix = ctx?.Provenance switch
        {
            AssemblyProvenance.CodeBaseMissing => "x ",
            AssemblyProvenance.IdentityMismatch => "! ",
            _ when node.Unresolved => "? ",
            _ when ctx?.AppliedPolicy is not null => "-> ",
            _ => string.Empty,
        };
        return prefix + FormatLabel(node, disambig);
    }
}
