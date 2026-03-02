using Dotsider.Analysis.Models;
using Hex1b;
using Hex1b.Layout;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Builds the diff Types tab showing a color-coded table of type differences.
/// </summary>
public static class DiffTypesView
{
    private static readonly Hex1bColor Green = Hex1bColor.FromRgb(80, 200, 120);
    private static readonly Hex1bColor Red = Hex1bColor.FromRgb(200, 80, 80);
    private static readonly Hex1bColor Yellow = Hex1bColor.FromRgb(200, 200, 80);
    private static readonly Hex1bColor Gray = Hex1bColor.FromRgb(100, 100, 120);

    public static Hex1bWidget Build(WidgetContext<VStackWidget> ctx, DiffState state)
    {
        var filtered = FilterEntries(state.DiffResult.TypeDiffs, state.FilterMode);

        return ctx.Table(filtered)
            .RowKey(r => r.Kind.ToString() + ":" + (r.Left?.FullName ?? r.Right?.FullName ?? ""))
            .Header(h =>
            [
                h.Cell("").Width(SizeHint.Fixed(3)),
                h.Cell("Type").Width(SizeHint.Fill),
                h.Cell("Base Type").Width(SizeHint.Fixed(25)),
                h.Cell("Methods").Width(SizeHint.Fixed(9)),
                h.Cell("Fields").Width(SizeHint.Fixed(8)),
                h.Cell("Change").Width(SizeHint.Fixed(30))
            ])
            .Row((r, entry, rowState) =>
            {
                var (prefix, color) = GetDiffStyle(entry.Kind);
                var type = entry.Right ?? entry.Left!;
                return
                [
                    r.Cell(c => c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, color), c.Text(prefix))),
                    r.Cell(c => c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, color), c.Text(type.FullName))),
                    r.Cell(c => c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, color), c.Text(type.BaseType ?? ""))),
                    r.Cell(c => c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, color), c.Text(type.MethodCount.ToString()))),
                    r.Cell(c => c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, color), c.Text(type.FieldCount.ToString()))),
                    r.Cell(c => c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, color), c.Text(entry.ChangeDescription ?? "")))
                ];
            })
            .Focus(state.DiffFocusedKey)
            .OnFocusChanged(key => state.DiffFocusedKey = key)
            .Compact()
            .Empty(e => e.Text("  No type differences with current filter"))
            .Fill();
    }

    private static IReadOnlyList<DiffEntry<TypeDefInfo>> FilterEntries(
        IReadOnlyList<DiffEntry<TypeDefInfo>> entries, DiffFilterMode mode) => mode switch
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
