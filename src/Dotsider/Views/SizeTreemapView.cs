using Dotsider.Analysis;
using Dotsider.Analysis.Models;
using Hex1b;
using Hex1b.Input;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Builds the Size Treemap tab (Tab 7), showing a squarified treemap of
/// assembly size by namespace/type/method using SurfaceWidget.
/// </summary>
public static class SizeTreemapView
{
    private static readonly Hex1bColor[] Palette =
    [
        Hex1bColor.FromRgb(0, 180, 160),
        Hex1bColor.FromRgb(80, 140, 200),
        Hex1bColor.FromRgb(200, 150, 60),
        Hex1bColor.FromRgb(160, 80, 140),
        Hex1bColor.FromRgb(80, 170, 80),
        Hex1bColor.FromRgb(200, 80, 80),
        Hex1bColor.FromRgb(120, 120, 170),
    ];

    public static Hex1bWidget Build(WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        var sizeTree = state.CachedSizeTree ??= SizeAnalyzer.BuildSizeTree(state.Analyzer, state.IlDisassembler);
        var currentLevel = state.TreemapCurrentLevel ?? sizeTree;

        return ctx.VStack(outer =>
        [
            // Breadcrumb
            outer.HStack(row =>
            {
                var parts = new List<Hex1bWidget>();
                parts.Add(row.Text($" {BuildBreadcrumb(state)} "));
                parts.Add(row.Text($"| Total: {state.FormatSizeToggleable(currentLevel.Size)}").Fill());
                return parts.ToArray();
            }).FixedHeight(1),

            // Treemap surface
            outer.Surface(s =>
            [
                s.Layer(surface =>
                {
                    if (currentLevel.Children.Count == 0)
                    {
                        surface.WriteText(2, 1, "No code size data available", Hex1bColor.FromRgb(140, 140, 160));
                        return;
                    }
                    var rects = TreemapLayout.Layout(
                        currentLevel.Children, 0, 0, surface.Width, surface.Height);
                    state.TreemapHoveredItem = null;
                    DrawTreemap(surface, rects, state, s.MouseX, s.MouseY);
                })
            ]).Fill(),

            // Detail bar
            outer.Text(state.TreemapHoveredItem ?? " Click to drill down | Backspace to go up").FixedHeight(1)
        ])
        .WithInputBindings(bindings =>
        {
            bindings.Key(Hex1bKey.Backspace).Action(_ =>
            {
                if (state.TreemapBreadcrumb.Count > 0)
                {
                    state.TreemapCurrentLevel = state.TreemapBreadcrumb.Pop();
                    state.App.Invalidate();
                }
            }, "Go up");
        })
        .Fill();
    }

    private static string BuildBreadcrumb(DotsiderState state)
    {
        var parts = new List<string>();
        foreach (var node in state.TreemapBreadcrumb.Reverse())
            parts.Add(node.Name);
        if (state.TreemapCurrentLevel is not null)
            parts.Add(state.TreemapCurrentLevel.Name);
        else if (state.CachedSizeTree is not null)
            parts.Add(state.CachedSizeTree.Name);
        return string.Join(" > ", parts);
    }

    private static void DrawTreemap(Surface surface, IReadOnlyList<TreemapRect> rects,
        DotsiderState state, int mouseX, int mouseY)
    {
        for (var i = 0; i < rects.Count; i++)
        {
            var rect = rects[i];
            var color = Palette[i % Palette.Length];

            var x1 = (int)rect.X;
            var y1 = (int)rect.Y;
            var x2 = (int)(rect.X + rect.Width);
            var y2 = (int)(rect.Y + rect.Height);

            if (x2 <= x1 || y2 <= y1) continue;

            // Fill background
            for (var y = y1; y < y2 && y < surface.Height; y++)
                for (var x = x1; x < x2 && x < surface.Width; x++)
                    surface.WriteChar(x, y, ' ', color, color);

            // Draw border (darker shade)
            var borderColor = Hex1bColor.FromRgb(
                (byte)(color.R * 40 / 100),
                (byte)(color.G * 40 / 100),
                (byte)(color.B * 40 / 100));

            for (var x = x1; x < x2 && x < surface.Width; x++)
            {
                if (y1 < surface.Height) surface.WriteChar(x, y1, '▁', borderColor, color);
            }
            for (var y = y1; y < y2 && y < surface.Height; y++)
            {
                if (x1 < surface.Width) surface.WriteChar(x1, y, '▕', borderColor, color);
            }

            // Write label if fits
            var cellW = x2 - x1;
            var cellH = y2 - y1;
            if (cellW > 3 && cellH > 0)
            {
                var label = rect.Node.Name;
                if (label.Length > cellW - 2) label = label[..(cellW - 4)] + "..";
                surface.WriteText(x1 + 1, y1, label, Hex1bColor.Black, color);

                if (cellH > 1)
                {
                    var sizeLabel = state.FormatSizeToggleable(rect.Node.Size);
                    if (sizeLabel.Length <= cellW - 2)
                        surface.WriteText(x1 + 1, y1 + 1, sizeLabel, Hex1bColor.Black, color);
                }
            }

            // Mouse hover detection
            if (mouseX >= x1 && mouseX < x2 && mouseY >= y1 && mouseY < y2)
            {
                state.TreemapHoveredItem = $" {rect.Node.FullPath}: {state.FormatSizeToggleable(rect.Node.Size)} ({rect.Node.Children.Count} children)";
            }
        }
    }
}
