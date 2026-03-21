using System.Text;
using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider;

/// <summary>
/// Shared helper for highlighting search matches with inline ANSI substring coloring.
/// Matching substrings render with a warm yellow background and black foreground;
/// non-matching text stays default. Works in both table cells and free-text contexts.
/// </summary>
public static class HighlightHelper
{
    /// <summary>The highlight background color applied to matching substrings (warm yellow).</summary>
    public static readonly Hex1bColor MatchBgColor = Hex1bColor.FromRgb(255, 220, 100);

    /// <summary>The dim color applied to non-matching items in spatial views.</summary>
    public static readonly Hex1bColor DimColor = Hex1bColor.FromRgb(50, 50, 60);

    private static readonly string MatchBgAnsi = MatchBgColor.ToBackgroundAnsi();
    private static readonly string BlackFgAnsi = Hex1bColor.Black.ToForegroundAnsi();

    /// <summary>
    /// Wraps each case-insensitive match of <paramref name="query"/> in <paramref name="text"/>
    /// with ANSI background (yellow) + foreground (black) codes, returning an ANSI-annotated string.
    /// After each match, restores <paramref name="restoreFg"/> and <paramref name="restoreBg"/>
    /// so the caller's active styling (e.g. focused-row colors) is preserved.
    /// </summary>
    public static string HighlightSubstring(string text, string? query,
        Hex1bColor? restoreFg = null, Hex1bColor? restoreBg = null)
    {
        if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(text))
            return text;

        // Build the restore sequence from the caller's colors, falling back to
        // default fg/bg resets when no explicit color is provided.
        var restoreAnsi = (restoreFg?.ToForegroundAnsi() ?? "\x1b[39m")
                        + (restoreBg?.ToBackgroundAnsi() ?? "\x1b[49m");

        var sb = new StringBuilder(text.Length + 32);
        var pos = 0;

        while (pos < text.Length)
        {
            var idx = text.IndexOf(query, pos, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                sb.Append(text, pos, text.Length - pos);
                break;
            }

            if (idx > pos)
                sb.Append(text, pos, idx - pos);

            sb.Append(MatchBgAnsi);
            sb.Append(BlackFgAnsi);
            sb.Append(text, idx, query.Length);
            sb.Append(restoreAnsi);

            pos = idx + query.Length;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Creates a table cell with optional inline match highlighting.
    /// When a query is active and the row matches, matching substrings render
    /// with yellow background + black foreground via embedded ANSI codes.
    /// After each match, <paramref name="restoreFg"/> and <paramref name="restoreBg"/>
    /// are re-applied so focused-row or cell-specific styling is preserved.
    /// </summary>
    public static Hex1bWidget HighlightCell<T>(
        WidgetContext<T> ctx, string text, string? query, bool isMatch,
        Hex1bColor? restoreFg = null, Hex1bColor? restoreBg = null) where T : Hex1bWidget
    {
        if (!string.IsNullOrEmpty(query) && isMatch)
            return ctx.Text(HighlightSubstring(text, query, restoreFg, restoreBg));

        return ctx.Text(text);
    }

    /// <summary>
    /// Creates a text widget with optional inline match highlighting.
    /// For use in non-table contexts such as the IL Inspector disassembly.
    /// </summary>
    public static Hex1bWidget HighlightText<T>(
        WidgetContext<T> ctx, string text, string? query,
        Hex1bColor? restoreFg = null, Hex1bColor? restoreBg = null) where T : Hex1bWidget
    {
        if (!string.IsNullOrEmpty(query))
            return ctx.Text(HighlightSubstring(text, query, restoreFg, restoreBg));

        return ctx.Text(text);
    }
}
