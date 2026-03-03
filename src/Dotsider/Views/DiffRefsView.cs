using Dotsider.Analysis.Models;
using Hex1b;
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
        var filtered = FilterEntries(state.DiffResult.AssemblyRefDiffs, state.FilterMode);

        return ctx.Table(filtered)
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
                return
                [
                    r.Cell(c => c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, color), c.Text(prefix))),
                    r.Cell(c => c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, color), c.Text(name))),
                    r.Cell(c => c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, color), c.Text(leftVer))),
                    r.Cell(c => c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, color), c.Text(rightVer))),
                    r.Cell(c => c.ThemePanel(t => t.Set(GlobalTheme.ForegroundColor, color), c.Text(entry.ChangeDescription ?? "")))
                ];
            })
            .Focus(state.DiffFocusedKey)
            .OnFocusChanged(key => state.DiffFocusedKey = key)
            .Compact()
            .Empty(e => e.Text("  No reference differences with current filter"))
            .Fill();
    }

    private static IReadOnlyList<DiffEntry<AssemblyRefInfo>> FilterEntries(
        IReadOnlyList<DiffEntry<AssemblyRefInfo>> entries, DiffFilterMode mode) => mode switch
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
