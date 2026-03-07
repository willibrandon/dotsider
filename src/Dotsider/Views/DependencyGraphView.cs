using Dotsider.Analysis;
using Dotsider.Analysis.Models;
using Hex1b;
using Hex1b.Input;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Builds the Dependency Graph tab (Tab 6), rendering an interactive
/// assembly reference graph using SurfaceWidget with search highlighting.
/// </summary>
public static class DependencyGraphView
{
    private static readonly Hex1bColor RootColor = Hex1bColor.FromRgb(0, 200, 180);
    private static readonly Hex1bColor RefColor = Hex1bColor.FromRgb(100, 130, 180);
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
        var (nodes, edges) = state.CachedGraph ??= DependencyGraphBuilder.Build(state.Analyzer);
        var search = state.Search[TabId.DepGraph];
        var query = search.Query;

        // Find matching node indices for navigation
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

        // Clamp match index to current results
        if (state.GraphMatchIndex >= matchingNodes.Count)
            state.GraphMatchIndex = matchingNodes.Count > 0 ? 0 : -1;

        // Set up match navigation using stable index
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

        return ctx.VStack(outer =>
        {
            var widgets = new List<Hex1bWidget>();

            // Display: hover > keyboard selection > search match > default
            var displayNode = state.GraphSelectedNode;
            if (displayNode is null && state.GraphSelectedIndex >= 0
                && state.GraphSelectedIndex < nodes.Count)
            {
                var node = nodes[state.GraphSelectedIndex];
                var version = node.Version is not null ? $" v{node.Version}" : "";
                displayNode = $"{node.Name}{version}";
            }
            if (displayNode is null && state.GraphMatchIndex >= 0
                && state.GraphMatchIndex < matchingNodes.Count)
            {
                var node = nodes[matchingNodes[state.GraphMatchIndex]];
                var version = node.Version is not null ? $" v{node.Version}" : "";
                displayNode = $"{node.Name}{version}";
            }

            widgets.Add(outer.HStack(row =>
            [
                row.Text($" Nodes: {nodes.Count}  Edges: {edges.Count}"),
                row.Text(displayNode is not null
                    ? $"  | {displayNode}"
                    : "  | Hover over a node for details").Fill()
            ]).FixedHeight(1));

            // Search bar
            SearchBarHelper.AddSearchBar(widgets, outer, search, state.App);

            // Graph surface wrapped in Interactable for keyboard/click support
            widgets.Add(outer.Interactable(ic =>
                ic.Surface(s =>
                [
                    s.Layer(surface => DrawGraph(surface, nodes, edges, state, s.MouseX, s.MouseY, query))
                ]).Fill()
            ).WithInputBindings(bindings =>
            {
                bindings.Key(Hex1bKey.RightArrow).Action(_ =>
                {
                    if (nodes.Count > 0)
                    {
                        state.GraphSelectedIndex = (state.GraphSelectedIndex + 1) % nodes.Count;
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
                        state.App.Invalidate();
                    }
                }, "Previous node");

                bindings.Key(Hex1bKey.Enter).Action(_ =>
                {
                    if (state.GraphSelectedIndex >= 0 && state.GraphSelectedIndex < nodes.Count)
                    {
                        var node = nodes[state.GraphSelectedIndex];
                        var resolvedPath = Analysis.AssemblyAnalyzer.ResolveAssemblyPath(
                            state.Analyzer.FilePath, node.Name);
                        if (resolvedPath is not null && state.PushAssembly(resolvedPath))
                        {
                            state.NavigateToTab(TabId.General);
                            state.App.RequestFocus(n =>
                                n.GetType().Name.StartsWith("TableNode"));
                            state.App.Invalidate();
                        }
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

    private static void DrawGraph(Surface surface, IReadOnlyList<GraphNode> nodes,
        IReadOnlyList<GraphEdge> edges, DotsiderState state, int mouseX, int mouseY, string? query)
    {
        var w = surface.Width;
        var h = surface.Height;
        if (w < 10 || h < 5) return;

        var hasQuery = !string.IsNullOrEmpty(query);
        var selectedIndex = state.GraphSelectedIndex;

        // Draw edges first (underneath nodes)
        foreach (var edge in edges)
        {
            var src = nodes.FirstOrDefault(n => n.Name == edge.SourceName);
            var tgt = nodes.FirstOrDefault(n => n.Name == edge.TargetName);
            if (src is null || tgt is null) continue;

            var x1 = (int)(src.X * (w - 1));
            var y1 = (int)(src.Y * (h - 1)) + 1;
            var x2 = (int)(tgt.X * (w - 1));
            var y2 = (int)(tgt.Y * (h - 1)) - 1;

            // Simple vertical-first routing
            var midY = (y1 + y2) / 2;
            var edgeC = hasQuery ? HighlightHelper.DimColor :
                edge.TypeRefCount > 10 ? Hex1bColor.FromRgb(140, 140, 180) : EdgeColor;

            for (var y = Math.Min(y1, midY); y <= Math.Max(y1, midY); y++)
                surface.WriteChar(x1, y, '│', edgeC);
            for (var x = Math.Min(x1, x2); x <= Math.Max(x1, x2); x++)
                surface.WriteChar(x, midY, '─', edgeC);
            for (var y = Math.Min(midY, y2); y <= Math.Max(midY, y2); y++)
                surface.WriteChar(x2, y, '│', edgeC);

            // Corners
            if (x1 != x2)
            {
                surface.WriteChar(x1, midY, x1 < x2 ? '└' : '┘', edgeC);
                surface.WriteChar(x2, midY, x1 < x2 ? '┐' : '┌', edgeC);
            }
        }

        // Draw nodes on top
        state.GraphSelectedNode = null;
        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            var cx = (int)(node.X * (w - 1));
            var cy = (int)(node.Y * (h - 1));
            var label = node.Name;
            var halfW = Math.Min(label.Length / 2 + 2, w / 2 - 1);
            var boxW = halfW * 2 + 1;
            var x0 = Math.Max(0, cx - halfW);
            var y0 = Math.Max(0, cy - 1);

            if (x0 + boxW > w) x0 = w - boxW;
            if (y0 + 3 > h) y0 = h - 3;

            var isMatch = hasQuery && node.Name.Contains(query!, StringComparison.OrdinalIgnoreCase);
            var isSelected = i == selectedIndex;
            var bg = isMatch ? (node.IsRoot ? RootColor : RefColor)
                : hasQuery ? HighlightHelper.DimColor
                : node.IsRoot ? RootColor : RefColor;
            var fg = Hex1bColor.Black;

            // Check mouse hover
            if (mouseX >= x0 && mouseX < x0 + boxW && mouseY >= y0 && mouseY < y0 + 3)
            {
                bg = HighlightColor;
                var version = node.Version is not null ? $" v{node.Version}" : "";
                state.GraphSelectedNode = $"{node.Name}{version}";
            }

            var borderColor = isSelected ? SelectionBorder : bg;

            // Draw box
            surface.WriteChar(x0, y0, '┌', borderColor);
            surface.WriteChar(x0 + boxW - 1, y0, '┐', borderColor);
            surface.WriteChar(x0, y0 + 2, '└', borderColor);
            surface.WriteChar(x0 + boxW - 1, y0 + 2, '┘', borderColor);
            for (var x = x0 + 1; x < x0 + boxW - 1; x++)
            {
                surface.WriteChar(x, y0, '─', borderColor);
                surface.WriteChar(x, y0 + 2, '─', borderColor);
            }
            surface.WriteChar(x0, y0 + 1, '│', borderColor);
            surface.WriteChar(x0 + boxW - 1, y0 + 1, '│', borderColor);

            // Fill interior and write label
            for (var x = x0 + 1; x < x0 + boxW - 1; x++)
                surface.WriteChar(x, y0 + 1, ' ', fg, bg);

            var truncLabel = label.Length > boxW - 2 ? label[..(boxW - 4)] + ".." : label;
            surface.WriteText(x0 + 1, y0 + 1, truncLabel, fg, bg);
        }
    }
}
