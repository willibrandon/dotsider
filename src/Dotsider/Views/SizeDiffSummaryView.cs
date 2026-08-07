using Dotsider.Core.Analysis.Models;
using Dotsider.Infrastructure;
using Hex1b;
using Hex1b.Documents;
using Hex1b.Input;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Builds the size-diff summary tab: totals on every applicable basis, per-kind direction
/// counts, and the top regressions and improvements — the same rows the headless
/// <c>size-check</c> report prints, so the interactive and CI views never disagree.
/// </summary>
public static class SizeDiffSummaryView
{
    private const int TopCount = 15;

    /// <summary>
    /// Builds the size-diff summary widget tree.
    /// </summary>
    /// <param name="ctx">The widget context for building widgets.</param>
    /// <param name="state">The size-diff state.</param>
    /// <returns>The root widget for the Summary tab.</returns>
    public static Hex1bWidget Build(WidgetContext<VStackWidget> ctx, SizeDiffState state)
    {
        var search = state.Search[0];

        state.NavigateNextMatch = null;
        state.NavigatePrevMatch = null;

        var text = BuildSummaryText(state);
        if (state.SummaryEditorText != text)
        {
            state.SummaryEditorText = text;
            state.SummaryEditorState = new EditorState(
                new Hex1bDocument(TerminalText.EscapeMultiline(text)))
            {
                IsReadOnly = true
            };
        }

        var searchProvider = new DiffSearchDecorationProvider { Query = search.Query };

        return ctx.VStack(outer =>
        {
            var widgets = new List<Hex1bWidget>();

            SearchBarHelper.AddSearchBar(widgets, outer, search, state.App);

            widgets.Add(outer.Border(
                outer.ThemePanel(t => t
                    .Set(EditorTheme.SelectionForegroundColor, Hex1bColor.Default)
                    .Set(EditorTheme.SelectionBackgroundColor, Hex1bColor.FromRgb(79, 82, 88)),
                outer.Editor(state.SummaryEditorState!)
                    .ViewRenderer(InfoEditorViewRenderer.Instance)
                    .Decorations(new InfoLabelDecorationProvider())
                    .Decorations(searchProvider)
                    .InputBindings(bindings =>
                    {
                        TextObjectHelper.ConfigureReadOnlyEditorBindings(
                            bindings,
                            state.SummaryEditorState!,
                            () => state.VimPending,
                            () => state.VimPendingEditor,
                            () => state.VimPendingCursorOffset,
                            () => state.VimPendingTimestamp,
                            (s, e, o) =>
                            {
                                state.VimPending = s;
                                state.VimPendingEditor = e;
                                state.VimPendingCursorOffset = o;
                                state.VimPendingTimestamp = DateTime.UtcNow;
                            },
                            state.PerformEditorYank,
                            () => state.App.Invalidate());
                    })
                    .FillWidth().FillHeight())
            ).Title(" Size Diff Summary ").Fill());

            return [.. widgets];
        })
        .InputBindings(bindings =>
        {
            bindings.Key(Hex1bKey.Escape).Global().OverridesCapture().Action(_ =>
            {
                state.VimPending = VimMotionState.Idle;
                if (search.IsActive)
                {
                    search.Dismiss();
                    state.App.Invalidate();
                }
            }, "Esc");
        })
        .Fill();
    }

    private static string BuildSummaryText(SizeDiffState state)
    {
        var diff = state.Diff;
        var summary = diff.Summary;
        var lines = new List<string>
        {
            $"  Baseline:   {state.LeftSource.BinaryPath ?? state.LeftSource.MstatPath}",
            $"  Current:    {state.RightSource.BinaryPath ?? state.RightSource.MstatPath}",
            $"  Formats:    {diff.LeftFormatVersion} → {diff.RightFormatVersion}",
            "",
            $"  Mstat total: {DotsiderState.FormatSize(summary.LeftTotal)} → "
                + $"{DotsiderState.FormatSize(summary.RightTotal)} "
                + $"(Δ{SizeDiffTreemapView.FormatDelta(summary.Delta)})",
        };

        if (state.LeftSource.BinaryFileSize is { } leftFile
            && state.RightSource.BinaryFileSize is { } rightFile)
        {
            lines.Add($"  File size:   {DotsiderState.FormatSize(leftFile)} → "
                + $"{DotsiderState.FormatSize(rightFile)} "
                + $"(Δ{SizeDiffTreemapView.FormatDelta(rightFile - leftFile)})");
        }

        lines.Add($"  Unchanged:   {DotsiderState.FormatSize(summary.UnchangedTotal)}");
        if (summary.LeftDeduplicatedMethods > 0 || summary.RightDeduplicatedMethods > 0)
        {
            lines.Add($"  Dedup'd methods: {summary.LeftDeduplicatedMethods} → "
                + $"{summary.RightDeduplicatedMethods}");
        }

        lines.Add("");
        lines.Add("  Changes by kind:      added  removed    grown   shrunk  unchanged");
        foreach (var counts in summary.Counts)
        {
            lines.Add($"  {counts.Kind,-18} {counts.Added,8} {counts.Removed,8} "
                + $"{counts.Grown,8} {counts.Shrunk,8} {counts.Unchanged,10}");
        }

        AppendContributors(lines, "Top regressions", diff.Contributors.Where(c => c.Delta > 0));
        AppendContributors(lines, "Top improvements", diff.Contributors.Where(c => c.Delta < 0));

        return string.Join('\n', lines);
    }

    private static void AppendContributors(
        List<string> lines, string title, IEnumerable<SizeDiffContributor> contributors)
    {
        var top = contributors.Take(TopCount).ToList();
        if (top.Count == 0) return;

        lines.Add("");
        lines.Add($"  {title}:");
        foreach (var c in top)
        {
            var entries = Math.Max(c.LeftEntryCount, c.RightEntryCount);
            lines.Add($"  {SizeDiffTreemapView.FormatDelta(c.Delta),12}  {c.Diff,-8} {c.Name}"
                + (entries > 1 ? $" ({entries} entries)" : "")
                + (c.AssemblyName.Length > 0 ? $"  [{c.AssemblyName}]" : ""));
        }
    }
}
