using Hex1b.Theming;

namespace Dotsider;

/// <summary>
/// Defines the custom color theme for the dotsider application.
/// Inspired by reverse engineering tool aesthetics with cyan/green accents on dark backgrounds.
/// </summary>
public static class DotsiderTheme
{
    /// <summary>
    /// Creates and returns the dotsider theme, locked for use.
    /// </summary>
    /// <returns>A finalized <see cref="Hex1bTheme"/> instance.</returns>
    public static Hex1bTheme Create()
    {
        var theme = new Hex1bTheme("Dotsider")
            .Set(GlobalTheme.ForegroundColor, Hex1bColor.White)
            .Set(GlobalTheme.BackgroundColor, Hex1bColor.FromRgb(18, 18, 24))

            // Buttons
            .Set(ButtonTheme.FocusedForegroundColor, Hex1bColor.Black)
            .Set(ButtonTheme.FocusedBackgroundColor, Hex1bColor.FromRgb(0, 200, 180))

            // Text input
            .Set(TextBoxTheme.CursorForegroundColor, Hex1bColor.Black)
            .Set(TextBoxTheme.CursorBackgroundColor, Hex1bColor.FromRgb(0, 255, 200))
            .Set(TextBoxTheme.SelectionBackgroundColor, Hex1bColor.FromRgb(0, 100, 80))

            // Lists and selections
            .Set(ListTheme.SelectedForegroundColor, Hex1bColor.Black)
            .Set(ListTheme.SelectedBackgroundColor, Hex1bColor.FromRgb(0, 200, 180))

            // Tables
            .Set(TableTheme.FocusedRowForeground, Hex1bColor.Black)
            .Set(TableTheme.FocusedRowBackground, Hex1bColor.FromRgb(0, 200, 180))

            // Trees
            .Set(TreeTheme.FocusedForegroundColor, Hex1bColor.Black)
            .Set(TreeTheme.FocusedBackgroundColor, Hex1bColor.FromRgb(0, 200, 180))

            // Tab panel
            .Set(TabBarTheme.SelectedForegroundColor, Hex1bColor.Black)
            .Set(TabBarTheme.SelectedBackgroundColor, Hex1bColor.FromRgb(0, 200, 180))
            .Set(TabBarTheme.ForegroundColor, Hex1bColor.FromRgb(140, 140, 160))

            // Splitter
            .Set(SplitterTheme.DividerColor, Hex1bColor.FromRgb(60, 60, 80))
            .Set(SplitterTheme.ThumbColor, Hex1bColor.FromRgb(80, 80, 100));

        theme.Lock();
        return theme;
    }
}
