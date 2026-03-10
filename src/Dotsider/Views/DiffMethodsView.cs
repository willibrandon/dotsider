using Dotsider.Core.Analysis.Models;
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

        // Set up match navigation
        state.NavigateNextMatch = null;
        state.NavigatePrevMatch = null;

        return ctx.VStack(outer =>
        {
            var widgets = new List<Hex1bWidget>();

            // Search bar
            SearchBarHelper.AddSearchBar(widgets, outer, search, state.App);

            // Table
            widgets.Add(outer.Table(filtered)
                .RowKey(r => r.Kind.ToString() + ":" + (r.Left?.DeclaringType ?? r.Right?.DeclaringType ?? "") + "::" +
                             (r.Left?.Name ?? r.Right?.Name ?? "") + (r.Left?.Signature ?? r.Right?.Signature ?? ""))
                .Header(h =>
                [
                    h.Cell("").Width(SizeHint.Fixed(3)),
                    h.Cell("Declaring Type").Width(SizeHint.Fixed(30)),
                    h.Cell("Method").Width(SizeHint.Fixed(25)),
                    h.Cell("Signature").Width(SizeHint.Fill),
                    h.Cell("Change").Width(SizeHint.Fixed(30))
                ])
                .Row((r, entry, rowState) =>
                {
                    var (prefix, color) = GetDiffStyle(entry.Kind);
                    var method = entry.Right ?? entry.Left!;
                    return
                    [
                        r.Cell(c => c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, color), c.Text(prefix))),
                        r.Cell(c => c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, color),
                            HighlightHelper.HighlightCell(c, method.DeclaringType, query, !string.IsNullOrEmpty(query)))),
                        r.Cell(c => c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, color),
                            HighlightHelper.HighlightCell(c, method.Name, query, !string.IsNullOrEmpty(query)))),
                        r.Cell(c => c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, color),
                            HighlightHelper.HighlightCell(c, method.Signature, query, !string.IsNullOrEmpty(query)))),
                        r.Cell(c => c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, color), c.Text(entry.ChangeDescription ?? "")))
                    ];
                })
                .Focus(state.DiffFocusedKey)
                .OnFocusChanged(key => state.DiffFocusedKey = key)
                .Compact()
                .Empty(e => e.Text("  No method differences with current filter"))
                .Fill());

            return [.. widgets];
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

    private static IReadOnlyList<DiffEntry<MethodDefInfo>> FilterEntries(
        IReadOnlyList<DiffEntry<MethodDefInfo>> entries, DiffFilterMode mode) => mode switch
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
