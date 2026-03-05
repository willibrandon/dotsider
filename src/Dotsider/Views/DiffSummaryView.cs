using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Builds the diff summary tab showing side-by-side assembly info and change statistics.
/// </summary>
public static class DiffSummaryView
{
    private static readonly Hex1bColor Green = Hex1bColor.FromRgb(80, 200, 120);
    private static readonly Hex1bColor Red = Hex1bColor.FromRgb(200, 80, 80);
    private static readonly Hex1bColor Yellow = Hex1bColor.FromRgb(200, 200, 80);

    /// <summary>
    /// Builds the diff summary view widget tree.
    /// </summary>
    /// <param name="ctx">The widget context for building widgets.</param>
    /// <param name="state">The diff mode application state.</param>
    /// <returns>The root widget for the Summary tab.</returns>
    public static Hex1bWidget Build(WidgetContext<VStackWidget> ctx, DiffState state)
    {
        var search = state.Search[0]; // Summary = tab 0
        var query = search.Query;
        var summary = state.DiffResult.MetadataSummary;

        // Set up match navigation (not applicable for static view)
        state.NavigateNextMatch = null;
        state.NavigatePrevMatch = null;

        return ctx.VStack(outer =>
        {
            var widgets = new List<Hex1bWidget>();

            // Search bar
            SearchBarHelper.AddSearchBar(widgets, outer, search, state.App);

            // Side-by-side assembly info
            widgets.Add(outer.HSplitter(
                left =>
                [
                    left.Border(
                        left.VStack(info =>
                        [
                            DiffInfoLine(info, "Name", state.Left.AssemblyName ?? "", query),
                            DiffInfoLine(info, "Version", state.Left.AssemblyVersion ?? "", query),
                            DiffInfoLine(info, "Size", DotsiderState.FormatSize(state.Left.FileSize), query),
                            DiffInfoLine(info, "Types", state.Left.TypeDefs.Count.ToString(), query),
                            DiffInfoLine(info, "Methods", state.Left.MethodDefs.Count.ToString(), query),
                            DiffInfoLine(info, "References", state.Left.AssemblyRefs.Count.ToString(), query)
                        ])
                    ).Title($" {state.Left.FileName} (Left) ").Fill()
                ],
                right =>
                [
                    right.Border(
                        right.VStack(info =>
                        [
                            DiffInfoLine(info, "Name", state.Right.AssemblyName ?? "", query),
                            DiffInfoLine(info, "Version", state.Right.AssemblyVersion ?? "", query),
                            DiffInfoLine(info, "Size", DotsiderState.FormatSize(state.Right.FileSize), query),
                            DiffInfoLine(info, "Types", state.Right.TypeDefs.Count.ToString(), query),
                            DiffInfoLine(info, "Methods", state.Right.MethodDefs.Count.ToString(), query),
                            DiffInfoLine(info, "References", state.Right.AssemblyRefs.Count.ToString(), query)
                        ])
                    ).Title($" {state.Right.FileName} (Right) ").Fill()
                ],
                leftWidth: 50).FixedHeight(9));

            // Change statistics
            widgets.Add(outer.Border(
                outer.VStack(stats =>
                [
                    stats.HStack(h =>
                    [
                        h.Text("  Types:      ").FixedWidth(14),
                        h.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, Green), h.Text($"+{summary.TypesAdded}")).FixedWidth(6),
                        h.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, Red), h.Text($"-{summary.TypesRemoved}")).FixedWidth(6),
                        h.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, Yellow), h.Text($"~{summary.TypesChanged}"))
                    ]).FixedHeight(1),
                    stats.HStack(h =>
                    [
                        h.Text("  Methods:    ").FixedWidth(14),
                        h.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, Green), h.Text($"+{summary.MethodsAdded}")).FixedWidth(6),
                        h.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, Red), h.Text($"-{summary.MethodsRemoved}")).FixedWidth(6),
                        h.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, Yellow), h.Text($"~{summary.MethodsChanged}"))
                    ]).FixedHeight(1),
                    stats.HStack(h =>
                    [
                        h.Text("  References: ").FixedWidth(14),
                        h.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, Green), h.Text($"+{summary.RefsAdded}")).FixedWidth(6),
                        h.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, Red), h.Text($"-{summary.RefsRemoved}")).FixedWidth(6),
                        h.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, Yellow), h.Text($"~{summary.RefsChanged}"))
                    ]).FixedHeight(1),
                    stats.Text(""),
                    stats.Text($"  Size delta: {(summary.SizeDelta >= 0 ? "+" : "")}{DotsiderState.FormatSize(Math.Abs(summary.SizeDelta))}")
                ])
            ).Title(" Change Summary ").Fill());

            return widgets.ToArray();
        })
        .WithInputBindings(bindings =>
        {
            bindings.Key(Hex1bKey.Escape).Global().OverridesCapture().Action(_ =>
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

    private static Hex1bWidget DiffInfoLine<T>(WidgetContext<T> ctx, string label, string value, string? query) where T : Hex1bWidget
    {
        return ctx.HStack(row =>
        [
            row.Text($"  {label}: ").FixedWidth(16),
            HighlightHelper.HighlightText(row, value, query)
        ]).FixedHeight(1);
    }
}
