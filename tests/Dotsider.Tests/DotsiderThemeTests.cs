using Hex1b.Theming;

namespace Dotsider.Tests;

public class DotsiderThemeTests
{
    /// <summary>
    /// The dotsider theme must NOT set GlobalTheme.BackgroundColor.
    /// When set, hex1b includes an explicit background in every text cell's ANSI codes.
    /// This prevents the surface compositor from inheriting the row highlight background
    /// (transparent cells inherit; explicit cells don't), causing broken row highlighting
    /// in the live demo's xterm.js terminal.
    /// </summary>
    [Fact]
    public void Theme_DoesNotSetGlobalBackground()
    {
        var theme = DotsiderTheme.Create();

        var bg = theme.GetGlobalBackground();

        Assert.True(bg.IsDefault, "GlobalTheme.BackgroundColor must not be set — " +
            "it breaks table row highlighting in xterm.js by preventing transparent " +
            "background compositing. The terminal background is set by the terminal " +
            "emulator itself (xterm.js theme / native terminal profile).");
    }
}
