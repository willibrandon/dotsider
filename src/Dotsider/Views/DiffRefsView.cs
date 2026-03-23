using Dotsider.Core.Analysis.Models;
using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Builds the diff References tab showing a color-coded table of assembly reference differences.
/// </summary>
public static class DiffRefsView
{
    private static readonly Hex1bColor Green = Hex1bColor.FromRgb(80, 200, 120);
    private static readonly Hex1bColor Red = Hex1bColor.FromRgb(200, 80, 80);
    private static readonly Hex1bColor Yellow = Hex1bColor.FromRgb(200, 200, 80);
    private static readonly Hex1bColor Gray = Hex1bColor.FromRgb(100, 100, 120);

    /// <summary>
    /// Builds the diff references view widget tree.
    /// </summary>
    /// <param name="ctx">The widget context for building widgets.</param>
    /// <param name="state">The diff mode application state.</param>
    /// <returns>The root widget for the References tab.</returns>
    public static Hex1bWidget Build(WidgetContext<VStackWidget> ctx, DiffState state)
    {
        var search = state.Search[3]; // References = tab 3
        var query = search.Query;
        var filtered = FilterEntries(state.DiffResult.AssemblyRefDiffs, state.FilterMode);

        // Apply search filter by ref name/version
        if (!string.IsNullOrEmpty(query))
        {
            filtered = [.. filtered.Where(e =>
            {
                var name = e.Right?.Name ?? e.Left?.Name ?? "";
                var leftVer = e.Left?.Version ?? "";
                var rightVer = e.Right?.Version ?? "";
                return $"{name} {leftVer} {rightVer}"
                    .Contains(query, StringComparison.OrdinalIgnoreCase);
            })];
            search.SetMatchCount(filtered.Count);
        }

        // Set up match navigation — cycle through filtered rows
        if (filtered.Count > 0 && !string.IsNullOrEmpty(query))
        {
            var keys = filtered.Select(e =>
                e.Kind.ToString() + ":" + (e.Left?.Name ?? e.Right?.Name ?? "")).ToList();
            state.NavigateNextMatch = () =>
            {
                var idx = keys.IndexOf(state.DiffFocusedKey as string ?? "");
                idx = (idx + 1) % keys.Count;
                state.DiffFocusedKey = keys[idx];
            };
            state.NavigatePrevMatch = () =>
            {
                var idx = keys.IndexOf(state.DiffFocusedKey as string ?? "");
                idx = idx <= 0 ? keys.Count - 1 : idx - 1;
                state.DiffFocusedKey = keys[idx];
            };
        }
        else
        {
            state.NavigateNextMatch = null;
            state.NavigatePrevMatch = null;
        }

        return ctx.VStack(outer =>
        {
            var widgets = new List<Hex1bWidget>();

            // Search bar
            SearchBarHelper.AddSearchBar(widgets, outer, search, state.App);

            // Table
            widgets.Add(outer.Table(filtered)
                .RowKey(r => r.Kind.ToString() + ":" + (r.Left?.Name ?? r.Right?.Name ?? ""))
                .Header(h =>
                [
                    h.Cell("").Width(SizeHint.Fixed(3)),
                    h.Cell("Assembly").Width(SizeHint.Fill),
                    h.Cell("Left Version").Width(SizeHint.Fixed(16)),
                    h.Cell("Right Version").Width(SizeHint.Fixed(16)),
                    h.Cell("Change").Width(SizeHint.Fixed(30))
                ])
                .Row((r, entry, rowState) =>
                {
                    var (prefix, color) = GetDiffStyle(entry.Kind);
                    var name = entry.Right?.Name ?? entry.Left?.Name ?? "";
                    var leftVer = entry.Left?.Version ?? "-";
                    var rightVer = entry.Right?.Version ?? "-";
                    var flash = rowState.IsFocused && state.YankFlashRow;
                    var focused = rowState.IsFocused && !flash;
                    var fg = flash ? Hex1bColor.FromRgb(24, 24, 37)
                        : focused ? Hex1bColor.Black
                        : color;
                    var bg = flash ? Hex1bColor.FromRgb(126, 201, 216)
                        : focused ? Hex1bColor.FromRgb(0, 200, 180)
                        : Hex1bColor.Default;
                    return
                    [
                        r.Cell(c => c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, fg).Set(GlobalTheme.BackgroundColor, bg), c.Text(prefix))),
                        r.Cell(c => c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, fg).Set(GlobalTheme.BackgroundColor, bg),
                            HighlightHelper.HighlightCell(c, name, query, !string.IsNullOrEmpty(query), fg))),
                        r.Cell(c => c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, fg).Set(GlobalTheme.BackgroundColor, bg), c.Text(leftVer))),
                        r.Cell(c => c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, fg).Set(GlobalTheme.BackgroundColor, bg), c.Text(rightVer))),
                        r.Cell(c => c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, fg).Set(GlobalTheme.BackgroundColor, bg), c.Text(entry.ChangeDescription ?? "")))
                    ];
                })
                .Focus(state.DiffFocusedKey)
                .OnFocusChanged(key => state.DiffFocusedKey = key)
                .Compact()
                .Empty(e => e.Text("  No reference differences with current filter"))
                .Fill());

            return [.. widgets];
        })
        .WithInputBindings(bindings =>
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

    private static IReadOnlyList<DiffEntry<AssemblyRefInfo>> FilterEntries(
        IReadOnlyList<DiffEntry<AssemblyRefInfo>> entries, DiffFilterMode mode) => mode switch
        {
            DiffFilterMode.AddedOnly => [.. entries.Where(e => e.Kind == DiffKind.Added)],
            DiffFilterMode.RemovedOnly => [.. entries.Where(e => e.Kind == DiffKind.Removed)],
            DiffFilterMode.ChangedOnly => [.. entries.Where(e => e.Kind != DiffKind.Unchanged)],
            _ => entries
        };

    private static (string Prefix, Hex1bColor Color) GetDiffStyle(DiffKind kind) => kind switch
    {
        DiffKind.Added => ("+", Green),
        DiffKind.Removed => ("-", Red),
        DiffKind.Changed => ("~", Yellow),
        _ => (" ", Gray)
    };
}
