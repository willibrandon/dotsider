using Dotsider.Infrastructure;
using Hex1b;
using Hex1b.Widgets;

namespace Dotsider;

/// <summary>
/// Shared helper that adds a search bar widget to a VStack's widget list.
/// Handles both editing mode (TextBox) and confirmed mode (static text with match count).
/// </summary>
public static class SearchBarHelper
{
    /// <summary>
    /// Conditionally adds a search bar to the widget list.
    /// When search is inactive, nothing is added.
    /// When editing, renders a TextBox for query input.
    /// When confirmed, renders static text showing query, match count, and keyboard hints.
    /// </summary>
    /// <param name="widgets">The list of widgets to append the search bar to.</param>
    /// <param name="ctx">The VStack widget context for creating child widgets.</param>
    /// <param name="search">The search state for the current tab.</param>
    /// <param name="app">The Hex1b application instance for invalidation.</param>
    /// <param name="isHexTab">Whether this is the hex dump tab (shows mode indicator).</param>
    /// <param name="hexModeHex">When hex tab, true = hex byte mode, false = ASCII text mode.</param>
    public static void AddSearchBar(
        List<Hex1bWidget> widgets, WidgetContext<VStackWidget> ctx,
        SearchState search, Hex1bApp app,
        bool isHexTab = false, bool hexModeHex = false)
    {
        if (!search.IsActive) return;

        var modePrefix = isHexTab ? (hexModeHex ? "[Hex] " : "[Text] ") : "";

        if (search.IsConfirmed)
        {
            var countText = search.MatchCount switch
            {
                -1 => "",
                0 => "  No matches",
                _ => $"  {search.MatchCount} matches"
            };
            widgets.Add(ctx.HStack(row =>
            [
                row.Text(TerminalText.Escape($" {modePrefix}/ {search.Query ?? ""}")),
                row.Text(countText),
                row.Text("  n/N: navigate | /: edit | Esc: close").Fill()
            ]).FixedHeight(1));
        }
        else
        {
            widgets.Add(ctx.HStack(row =>
            [
                row.Text($" {modePrefix}/ ").FixedWidth(3 + modePrefix.Length),
                row.TextBox(search.Query ?? "")
                    .OnTextChanged(e =>
                    {
                        search.UpdateQuery(e.NewText);
                        app.Invalidate();
                    })
                    .Fill()
            ]).FixedHeight(1));
        }
    }
}
