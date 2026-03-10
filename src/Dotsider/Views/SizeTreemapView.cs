using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Hex1b;
using Hex1b.Input;
using Hex1b.Surfaces;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Builds the Size Treemap tab (Tab 7), showing a squarified treemap of
/// assembly size by namespace/type/method using SurfaceWidget with search highlighting.
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

    /// <summary>
    /// Builds the Size Treemap view widget tree.
    /// </summary>
    /// <param name="ctx">The widget context for building widgets.</param>
    /// <param name="state">The shared application state.</param>
    /// <returns>The root widget for the Size Map tab.</returns>
    public static Hex1bWidget Build(WidgetContext<VStackWidget> ctx, DotsiderState state)
    {
        var sizeTree = state.CachedSizeTree ??= SizeAnalyzer.BuildSizeTree(state.Analyzer);
        var currentLevel = state.TreemapCurrentLevel ?? sizeTree;
        var search = state.Search[TabId.SizeMap];
        var query = search.Query;

        // Find matching items for navigation
        var matchingItems = new List<int>();
        if (!string.IsNullOrEmpty(query))
        {
            for (var i = 0; i < currentLevel.Children.Count; i++)
            {
                if (currentLevel.Children[i].Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                    matchingItems.Add(i);
            }
            search.SetMatchCount(matchingItems.Count);
        }

        // Clamp match index to current results
        if (state.TreemapMatchIndex >= matchingItems.Count)
            state.TreemapMatchIndex = matchingItems.Count > 0 ? 0 : -1;

        // Set up match navigation using stable index
        state.NavigateNextMatch = matchingItems.Count > 0 ? () =>
        {
            state.TreemapMatchIndex = state.TreemapMatchIndex < 0
                ? 0 : (state.TreemapMatchIndex + 1) % matchingItems.Count;
        }
        : null;
        state.NavigatePrevMatch = matchingItems.Count > 0 ? () =>
        {
            state.TreemapMatchIndex = state.TreemapMatchIndex <= 0
                ? matchingItems.Count - 1 : state.TreemapMatchIndex - 1;
        }
        : null;

        return ctx.VStack(outer =>
        {
            var widgets = new List<Hex1bWidget>
            {
                // Breadcrumb
                outer.HStack(row =>
                [
                    row.Text($" {BuildBreadcrumb(state)} "),
                    row.Text($"| Total: {state.FormatSizeToggleable(currentLevel.Size)}").Fill()
                ]).FixedHeight(1)
            };

            // Search bar
            SearchBarHelper.AddSearchBar(widgets, outer, search, state.App);

            // Treemap surface wrapped in Interactable for click/Enter/arrow/Backspace support
            widgets.Add(outer.Interactable(ic =>
                ic.Surface(s =>
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
                        state.TreemapHoveredNode = null;
                        DrawTreemap(surface, rects, state, s.MouseX, s.MouseY, query);
                    })
                ]).Fill()
            ).OnClick(e =>
            {
                SizeNode? drillTarget = null;

                if (e.Context.MouseX >= 0)
                {
                    // Mouse click: compute target from click coordinates
                    var relX = e.Context.MouseX - e.Node.Bounds.X;
                    var relY = e.Context.MouseY - e.Node.Bounds.Y;
                    var rects = TreemapLayout.Layout(
                        currentLevel.Children, 0, 0, e.Node.Bounds.Width, e.Node.Bounds.Height);
                    foreach (var rect in rects)
                    {
                        if (relX >= (int)rect.X && relX < (int)(rect.X + rect.Width) &&
                            relY >= (int)rect.Y && relY < (int)(rect.Y + rect.Height))
                        {
                            drillTarget = rect.Node;
                            break;
                        }
                    }
                }
                else
                {
                    // Keyboard (Enter/Space): prefer search match over stale selection
                    if (state.TreemapMatchIndex >= 0 && state.TreemapMatchIndex < matchingItems.Count)
                        drillTarget = currentLevel.Children[matchingItems[state.TreemapMatchIndex]];
                    else if (state.TreemapSelectedIndex >= 0 && state.TreemapSelectedIndex < currentLevel.Children.Count)
                        drillTarget = currentLevel.Children[state.TreemapSelectedIndex];
                }

                if (drillTarget is { Children.Count: > 0 })
                {
                    state.TreemapBreadcrumb.Push(currentLevel);
                    state.TreemapCurrentLevel = drillTarget;
                    state.TreemapSelectedIndex = -1;
                    state.TreemapMatchIndex = -1;
                    state.TreemapHoveredNode = null;
                    state.App.Invalidate();
                }
                else if (drillTarget is { Kind: SizeNodeKind.Method, FullPath: var fullPath })
                {
                    // Leaf method node — navigate to IL Inspector
                    // FullPath format: "DeclaringType::MethodName@0xTOKEN"
                    var atIdx = fullPath.LastIndexOf('@');
                    if (atIdx > 0 && fullPath.Length > atIdx + 3
                        && int.TryParse(fullPath[(atIdx + 3)..],
                            System.Globalization.NumberStyles.HexNumber, null, out var token))
                    {
                        var method = state.Analyzer.MethodDefs.FirstOrDefault(m => m.Token == token);
                        if (method is not null)
                            state.NavigateToIlMethod(method);
                    }
                }
            }).WithInputBindings(bindings =>
            {
                bindings.Key(Hex1bKey.Backspace).Action(_ =>
                {
                    if (state.TreemapBreadcrumb.Count > 0)
                    {
                        state.TreemapCurrentLevel = state.TreemapBreadcrumb.Pop();
                        state.TreemapSelectedIndex = -1;
                        state.App.Invalidate();
                    }
                }, "Go up");

                bindings.Mouse(MouseButton.Right).Action(_ =>
                {
                    if (state.TreemapBreadcrumb.Count > 0)
                    {
                        state.TreemapCurrentLevel = state.TreemapBreadcrumb.Pop();
                        state.TreemapSelectedIndex = -1;
                        state.App.Invalidate();
                    }
                }, "Go up");

                bindings.Key(Hex1bKey.RightArrow).Action(_ =>
                {
                    if (currentLevel.Children.Count > 0)
                    {
                        state.TreemapSelectedIndex = (state.TreemapSelectedIndex + 1) % currentLevel.Children.Count;
                        state.App.Invalidate();
                    }
                }, "Next item");

                bindings.Key(Hex1bKey.LeftArrow).Action(_ =>
                {
                    if (currentLevel.Children.Count > 0)
                    {
                        state.TreemapSelectedIndex = state.TreemapSelectedIndex <= 0
                            ? currentLevel.Children.Count - 1
                            : state.TreemapSelectedIndex - 1;
                        state.App.Invalidate();
                    }
                }, "Previous item");
            }).Fill());

            // Detail bar: hover > selected > search match > default hint
            var detailText = state.TreemapHoveredItem;
            if (detailText is null && state.TreemapSelectedIndex >= 0
                && state.TreemapSelectedIndex < currentLevel.Children.Count)
            {
                var child = currentLevel.Children[state.TreemapSelectedIndex];
                detailText = $" {child.FullPath}: {state.FormatSizeToggleable(child.Size)} ({child.Children.Count} children)";
            }
            if (detailText is null && state.TreemapMatchIndex >= 0
                && state.TreemapMatchIndex < matchingItems.Count)
            {
                var child = currentLevel.Children[matchingItems[state.TreemapMatchIndex]];
                detailText = $" {child.FullPath}: {state.FormatSizeToggleable(child.Size)} ({child.Children.Count} children)";
            }
            widgets.Add(outer.Text(detailText ?? "").FixedHeight(1));

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

    private static readonly Hex1bColor SelectionBorder = Hex1bColor.FromRgb(255, 255, 255);

    private static void DrawTreemap(Surface surface, IReadOnlyList<TreemapRect> rects,
        DotsiderState state, int mouseX, int mouseY, string? query)
    {
        var hasQuery = !string.IsNullOrEmpty(query);
        var selectedIndex = state.TreemapSelectedIndex;

        for (var i = 0; i < rects.Count; i++)
        {
            var rect = rects[i];
            var isMatch = hasQuery && rect.Node.Name.Contains(query!, StringComparison.OrdinalIgnoreCase);
            var isSelected = i == selectedIndex;
            var color = isMatch ? Palette[i % Palette.Length]
                : hasQuery ? HighlightHelper.DimColor
                : Palette[i % Palette.Length];

            var x1 = (int)rect.X;
            var y1 = (int)rect.Y;
            var x2 = (int)(rect.X + rect.Width);
            var y2 = (int)(rect.Y + rect.Height);

            if (x2 <= x1 || y2 <= y1) continue;

            // Fill background
            for (var y = y1; y < y2 && y < surface.Height; y++)
                for (var x = x1; x < x2 && x < surface.Width; x++)
                    surface.WriteChar(x, y, ' ', color, color);

            // Draw border: bright white for selected, dark shade for normal
            var borderColor = isSelected
                ? SelectionBorder
                : Hex1bColor.FromRgb(
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

            // Selected items also get right and bottom borders
            if (isSelected)
            {
                for (var x = x1; x < x2 && x < surface.Width; x++)
                {
                    if (y2 - 1 < surface.Height) surface.WriteChar(x, y2 - 1, '▔', borderColor, color);
                }
                for (var y = y1; y < y2 && y < surface.Height; y++)
                {
                    if (x2 - 1 < surface.Width) surface.WriteChar(x2 - 1, y, '▏', borderColor, color);
                }
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
                state.TreemapHoveredNode = rect.Node;
            }
        }
    }
}
