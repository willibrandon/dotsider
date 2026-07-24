using Dotsider.Core.Analysis.Models;
using Dotsider.Infrastructure;
using Hex1b;
using Hex1b.Input;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Builds the diff Methods tab showing a color-coded table of method differences.
/// </summary>
public static class DiffMethodsView
{
    private static readonly Hex1bColor Green = Hex1bColor.FromRgb(80, 200, 120);
    private static readonly Hex1bColor Red = Hex1bColor.FromRgb(200, 80, 80);
    private static readonly Hex1bColor Yellow = Hex1bColor.FromRgb(200, 200, 80);
    private static readonly Hex1bColor Gray = Hex1bColor.FromRgb(100, 100, 120);

    /// <summary>
    /// Builds the diff methods view widget tree.
    /// </summary>
    /// <param name="ctx">The widget context for building widgets.</param>
    /// <param name="state">The diff mode application state.</param>
    /// <returns>The root widget for the Methods tab.</returns>
    public static Hex1bWidget Build(WidgetContext<VStackWidget> ctx, DiffState state)
    {
        var search = state.Search[2]; // Methods = tab 2
        var query = search.Query;
        var filtered = FilterEntries(state.DiffResult.MethodDiffs, state.FilterMode);

        // Apply search filter by method name/signature
        if (!string.IsNullOrEmpty(query))
        {
            filtered = [.. filtered.Where(e =>
            {
                var method = e.Right ?? e.Left!;
                return method.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                       method.Signature.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                       method.DeclaringType.Contains(query, StringComparison.OrdinalIgnoreCase);
            })];
            search.SetMatchCount(filtered.Count);
        }

        // Set up match navigation — cycle through filtered rows
        if (filtered.Count > 0 && !string.IsNullOrEmpty(query))
        {
            state.NavigateNextMatch = () => NavigateMatch(state, forward: true);
            state.NavigatePrevMatch = () => NavigateMatch(state, forward: false);
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
                .RowKey(KeyFor)
                .Header(h =>
                [
                    h.Cell("").Width(SizeHint.Fixed(3)),
                    h.Cell("Declaring Type").Width(SizeHint.Fixed(30)),
                    h.Cell("Method").Width(SizeHint.Fixed(25)),
                    h.Cell("Signature").Width(SizeHint.Fill),
                    h.Cell("Change").Width(SizeHint.Fixed(35))
                ])
                .Row((r, entry, rowState) =>
                {
                    var (prefix, color) = GetDiffStyle(entry.Kind);
                    var method = entry.Right ?? entry.Left!;
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
                            HighlightHelper.HighlightCell(c, method.DeclaringType, query, !string.IsNullOrEmpty(query), fg))),
                        r.Cell(c => c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, fg).Set(GlobalTheme.BackgroundColor, bg),
                            HighlightHelper.HighlightCell(c, method.Name, query, !string.IsNullOrEmpty(query), fg))),
                        r.Cell(c => c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, fg).Set(GlobalTheme.BackgroundColor, bg),
                            HighlightHelper.HighlightCell(c, method.Signature, query, !string.IsNullOrEmpty(query), fg))),
                        r.Cell(c => c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, fg).Set(GlobalTheme.BackgroundColor, bg), c.Text(TerminalText.Escape(entry.ChangeDescription ?? ""))))
                    ];
                })
                .Focus(state.DiffFocusedKey)
                .OnFocusChanged(key => state.DiffFocusedKey = key)
                .Compact()
                .Empty(e => e.Text("  No method differences with current filter"))
                .Fill());

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

    private static IReadOnlyList<DiffEntry<MethodDefInfo>> FilterEntries(
        IReadOnlyList<DiffEntry<MethodDefInfo>> entries, DiffFilterMode mode) => mode switch
        {
            DiffFilterMode.AddedOnly => [.. entries.Where(e => e.Kind == DiffKind.Added)],
            DiffFilterMode.RemovedOnly => [.. entries.Where(e => e.Kind == DiffKind.Removed)],
            DiffFilterMode.ChangedOnly => [.. entries.Where(e => e.Kind != DiffKind.Unchanged)],
            _ => entries
        };

    private static List<string> GetMatchingKeys(DiffState state)
    {
        var search = state.Search[2];
        var query = search.Query;
        if (string.IsNullOrEmpty(query)) return [];

        return [.. FilterEntries(state.DiffResult.MethodDiffs, state.FilterMode)
            .Where(e =>
            {
                var method = e.Right ?? e.Left!;
                return method.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                       method.Signature.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                       method.DeclaringType.Contains(query, StringComparison.OrdinalIgnoreCase);
            })
            .Select(KeyFor)];
    }

    private static void NavigateMatch(DiffState state, bool forward)
    {
        var keys = GetMatchingKeys(state);
        if (keys.Count == 0) return;

        var idx = keys.IndexOf(state.DiffFocusedKey as string ?? "");
        idx = forward
            ? (idx + 1) % keys.Count
            : idx <= 0 ? keys.Count - 1 : idx - 1;
        state.DiffFocusedKey = keys[idx];
    }

    private static string KeyFor(DiffEntry<MethodDefInfo> entry) =>
        entry.Kind + ":"
        + (entry.Left?.DeclaringType ?? entry.Right?.DeclaringType ?? "")
        + "::"
        + (entry.Left?.Name ?? entry.Right?.Name ?? "")
        + (entry.Left?.Signature ?? entry.Right?.Signature ?? "");

    private static (string Prefix, Hex1bColor Color) GetDiffStyle(DiffKind kind) => kind switch
    {
        DiffKind.Added => ("+", Green),
        DiffKind.Removed => ("-", Red),
        DiffKind.Changed => ("~", Yellow),
        _ => (" ", Gray)
    };
}
