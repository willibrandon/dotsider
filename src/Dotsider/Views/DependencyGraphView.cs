using Dotsider.Analysis;
using Dotsider.Analysis.Models;
using Hex1b;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Builds the Dependency Graph tab (Tab 6), rendering an interactive
/// assembly reference graph using SurfaceWidget.
/// </summary>
public static class DependencyGraphView
{
    private static readonly Hex1bColor RootColor = Hex1bColor.FromRgb(0, 200, 180);
    private static readonly Hex1bColor RefColor = Hex1bColor.FromRgb(100, 130, 180);
    private static readonly Hex1bColor EdgeColor = Hex1bColor.FromRgb(80, 80, 100);
    private static readonly Hex1bColor HighlightColor = Hex1bColor.FromRgb(255, 220, 100);

    /// <summary>
    /// Builds the Dependency Graph view widget tree.
    /// </summary>
    /// <param name="ctx">The widget context for building widgets.</param>
    /// <param name="state">The shared application state.</param>
    /// <returns>The root widget for the Dependency Graph tab.</returns>
    public static Hex1bWidget Build(WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        var (nodes, edges) = state.CachedGraph ??= DependencyGraphBuilder.Build(state.Analyzer);

        return ctx.VStack(outer =>
        [
            outer.HStack(row =>
            [
                row.Text($" Nodes: {nodes.Count}  Edges: {edges.Count}"),
                row.Text(state.GraphSelectedNode is { } sel
                    ? $"  | {sel}"
                    : "  | Hover over a node for details").Fill()
            ]).FixedHeight(1),

            outer.Surface(s =>
            [
                s.Layer(surface => DrawGraph(surface, nodes, edges, state, s.MouseX, s.MouseY))
            ]).Fill()
        ]).Fill();
    }

    private static void DrawGraph(Surface surface, IReadOnlyList<GraphNode> nodes,
        IReadOnlyList<GraphEdge> edges, DotsiderState state, int mouseX, int mouseY)
    {
        var w = surface.Width;
        var h = surface.Height;
        if (w < 10 || h < 5) return;

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
            var edgeC = edge.TypeRefCount > 10 ? Hex1bColor.FromRgb(140, 140, 180) : EdgeColor;

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
        foreach (var node in nodes)
        {
            var cx = (int)(node.X * (w - 1));
            var cy = (int)(node.Y * (h - 1));
            var label = node.Name;
            var halfW = Math.Min(label.Length / 2 + 2, w / 2 - 1);
            var boxW = halfW * 2 + 1;
            var x0 = Math.Max(0, cx - halfW);
            var y0 = Math.Max(0, cy - 1);

            if (x0 + boxW > w) x0 = w - boxW;
            if (y0 + 3 > h) y0 = h - 3;

            var bg = node.IsRoot ? RootColor : RefColor;
            var fg = Hex1bColor.Black;

            // Check mouse hover
            if (mouseX >= x0 && mouseX < x0 + boxW && mouseY >= y0 && mouseY < y0 + 3)
            {
                bg = HighlightColor;
                var version = node.Version is not null ? $" v{node.Version}" : "";
                state.GraphSelectedNode = $"{node.Name}{version}";
            }

            // Draw box
            surface.WriteChar(x0, y0, '┌', bg);
            surface.WriteChar(x0 + boxW - 1, y0, '┐', bg);
            surface.WriteChar(x0, y0 + 2, '└', bg);
            surface.WriteChar(x0 + boxW - 1, y0 + 2, '┘', bg);
            for (var x = x0 + 1; x < x0 + boxW - 1; x++)
            {
                surface.WriteChar(x, y0, '─', bg);
                surface.WriteChar(x, y0 + 2, '─', bg);
            }
            surface.WriteChar(x0, y0 + 1, '│', bg);
            surface.WriteChar(x0 + boxW - 1, y0 + 1, '│', bg);

            // Fill interior and write label
            for (var x = x0 + 1; x < x0 + boxW - 1; x++)
                surface.WriteChar(x, y0 + 1, ' ', fg, bg);

            var truncLabel = label.Length > boxW - 2 ? label[..(boxW - 4)] + ".." : label;
            surface.WriteText(x0 + 1, y0 + 1, truncLabel, fg, bg);
        }
    }
}
