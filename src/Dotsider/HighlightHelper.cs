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
    private const string AnsiReset = "\x1b[0m";

    /// <summary>
    /// Wraps each case-insensitive match of <paramref name="query"/> in <paramref name="text"/>
    /// with ANSI background (yellow) + foreground (black) codes, returning an ANSI-annotated string.
    /// </summary>
    public static string HighlightSubstring(string text, string? query)
    {
        if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(text))
            return text;

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
            sb.Append(AnsiReset);

            pos = idx + query.Length;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Creates a table cell with optional inline match highlighting.
    /// When a query is active and the row matches, matching substrings render
    /// with yellow background + black foreground via embedded ANSI codes.
    /// </summary>
    public static Hex1bWidget HighlightCell<T>(
        WidgetContext<T> ctx, string text, string? query, bool isMatch) where T : Hex1bWidget
    {
        if (!string.IsNullOrEmpty(query) && isMatch)
            return ctx.Text(HighlightSubstring(text, query));

        return ctx.Text(text);
    }

    /// <summary>
    /// Creates a text widget with optional inline match highlighting.
    /// For use in non-table contexts such as the IL Inspector disassembly.
    /// </summary>
    public static Hex1bWidget HighlightText<T>(
        WidgetContext<T> ctx, string text, string? query) where T : Hex1bWidget
    {
        if (!string.IsNullOrEmpty(query))
            return ctx.Text(HighlightSubstring(text, query));

        return ctx.Text(text);
    }
}
