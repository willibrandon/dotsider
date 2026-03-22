using Hex1b;
using Hex1b.Documents;
using Hex1b.Input;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Builds the diff summary tab showing side-by-side assembly info and change statistics.
/// </summary>
public static class DiffSummaryView
{
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

        // Build left/right info text for read-only editors
        var leftText = string.Join("\n",
            $"  Name:       {state.Left.AssemblyName ?? ""}",
            $"  Version:    {state.Left.AssemblyVersion ?? ""}",
            $"  Size:       {DotsiderState.FormatSize(state.Left.FileSize)}",
            $"  Types:      {state.Left.TypeDefs.Count}",
            $"  Methods:    {state.Left.MethodDefs.Count}",
            $"  References: {state.Left.AssemblyRefs.Count}");

        if (state.LeftInfoEditorText != leftText)
        {
            state.LeftInfoEditorText = leftText;
            state.LeftInfoEditorState = new EditorState(new Hex1bDocument(leftText)) { IsReadOnly = true };
        }

        var rightText = string.Join("\n",
            $"  Name:       {state.Right.AssemblyName ?? ""}",
            $"  Version:    {state.Right.AssemblyVersion ?? ""}",
            $"  Size:       {DotsiderState.FormatSize(state.Right.FileSize)}",
            $"  Types:      {state.Right.TypeDefs.Count}",
            $"  Methods:    {state.Right.MethodDefs.Count}",
            $"  References: {state.Right.AssemblyRefs.Count}");

        if (state.RightInfoEditorText != rightText)
        {
            state.RightInfoEditorText = rightText;
            state.RightInfoEditorState = new EditorState(new Hex1bDocument(rightText)) { IsReadOnly = true };
        }

        // Build change stats text for read-only editor (fixed-width columns for alignment)
        var statsText = string.Join("\n",
            $"  Types:      {$"+{summary.TypesAdded}",-6}{$"-{summary.TypesRemoved}",-6}~{summary.TypesChanged}",
            $"  Methods:    {$"+{summary.MethodsAdded}",-6}{$"-{summary.MethodsRemoved}",-6}~{summary.MethodsChanged}",
            $"  References: {$"+{summary.RefsAdded}",-6}{$"-{summary.RefsRemoved}",-6}~{summary.RefsChanged}",
            "",
            $"  Size delta: {(summary.SizeDelta >= 0 ? "+" : "")}{DotsiderState.FormatSize(Math.Abs(summary.SizeDelta))}");

        if (state.ChangeStatsEditorText != statsText)
        {
            state.ChangeStatsEditorText = statsText;
            state.ChangeStatsEditorState = new EditorState(new Hex1bDocument(statsText)) { IsReadOnly = true };
        }

        var leftSearchProvider = new DiffSearchDecorationProvider { Query = query };
        var rightSearchProvider = new DiffSearchDecorationProvider { Query = query };
        var statsSearchProvider = new DiffSearchDecorationProvider { Query = query };

        return ctx.VStack(outer =>
        {
            var widgets = new List<Hex1bWidget>();

            // Search bar
            SearchBarHelper.AddSearchBar(widgets, outer, search, state.App);

            // Side-by-side assembly info (read-only editors)
            widgets.Add(outer.HSplitter(
                left =>
                [
                    left.Border(
                        left.ThemePanel(t => t
                            .Set(EditorTheme.SelectionForegroundColor, Hex1bColor.Default)
                            .Set(EditorTheme.SelectionBackgroundColor, Hex1bColor.FromRgb(79, 82, 88)),
                        left.Editor(state.LeftInfoEditorState!)
                            .WithViewRenderer(InfoEditorViewRenderer.Instance)
                            .Decorations(new InfoLabelDecorationProvider())
                            .Decorations(leftSearchProvider)
                            .FillWidth().FillHeight())
                    ).Title($" {state.Left.FileName} (Left) ").Fill()
                ],
                right =>
                [
                    right.Border(
                        right.ThemePanel(t => t
                            .Set(EditorTheme.SelectionForegroundColor, Hex1bColor.Default)
                            .Set(EditorTheme.SelectionBackgroundColor, Hex1bColor.FromRgb(79, 82, 88)),
                        right.Editor(state.RightInfoEditorState!)
                            .WithViewRenderer(InfoEditorViewRenderer.Instance)
                            .Decorations(new InfoLabelDecorationProvider())
                            .Decorations(rightSearchProvider)
                            .FillWidth().FillHeight())
                    ).Title($" {state.Right.FileName} (Right) ").Fill()
                ],
                leftWidth: 50).FixedHeight(9));

            // Change statistics (read-only editor with decoration providers)
            widgets.Add(outer.Border(
                outer.ThemePanel(t => t
                    .Set(EditorTheme.SelectionForegroundColor, Hex1bColor.Default)
                    .Set(EditorTheme.SelectionBackgroundColor, Hex1bColor.FromRgb(79, 82, 88)),
                outer.Editor(state.ChangeStatsEditorState!)
                    .WithViewRenderer(InfoEditorViewRenderer.Instance)
                    .Decorations(new InfoLabelDecorationProvider())
                    .Decorations(new DiffStatsDecorationProvider())
                    .Decorations(statsSearchProvider)
                    .FillWidth().FillHeight())
            ).Title(" Change Summary ").Fill());

            return [.. widgets];
        })
        .WithInputBindings(bindings =>
        {
            // Tab cycles focus: Left Info → Right Info → Change Stats → Left Info
            bindings.Key(Hex1bKey.Tab).Global().Action(_ =>
            {
                if (state.App.FocusedNode is EditorNode { State: var es })
                {
                    if (es == state.LeftInfoEditorState)
                        state.App.RequestFocus(node =>
                            node is EditorNode e && e.State == state.RightInfoEditorState);
                    else if (es == state.RightInfoEditorState)
                        state.App.RequestFocus(node =>
                            node is EditorNode e && e.State == state.ChangeStatsEditorState);
                    else
                        state.App.RequestFocus(node =>
                            node is EditorNode e && e.State == state.LeftInfoEditorState);
                }
                else
                {
                    state.App.RequestFocus(node =>
                        node is EditorNode e && e.State == state.LeftInfoEditorState);
                }
                state.App.Invalidate();
            }, "Cycle focus");

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
}
