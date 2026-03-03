using Dotsider.Analysis.Models;
using Hex1b;
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
        var filtered = FilterEntries(state.DiffResult.MethodDiffs, state.FilterMode);

        return ctx.Table(filtered)
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
                    r.Cell(c => c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, color), c.Text(method.DeclaringType))),
                    r.Cell(c => c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, color), c.Text(method.Name))),
                    r.Cell(c => c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, color), c.Text(method.Signature))),
                    r.Cell(c => c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, color), c.Text(entry.ChangeDescription ?? "")))
                ];
            })
            .Focus(state.DiffFocusedKey)
            .OnFocusChanged(key => state.DiffFocusedKey = key)
            .Compact()
            .Empty(e => e.Text("  No method differences with current filter"))
            .Fill();
    }

    private static IReadOnlyList<DiffEntry<MethodDefInfo>> FilterEntries(
        IReadOnlyList<DiffEntry<MethodDefInfo>> entries, DiffFilterMode mode) => mode switch
    {
        DiffFilterMode.AddedOnly => entries.Where(e => e.Kind == DiffKind.Added).ToList(),
        DiffFilterMode.RemovedOnly => entries.Where(e => e.Kind == DiffKind.Removed).ToList(),
        DiffFilterMode.ChangedOnly => entries.Where(e => e.Kind != DiffKind.Unchanged).ToList(),
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
