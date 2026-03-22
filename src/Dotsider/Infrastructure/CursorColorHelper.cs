namespace Dotsider.Infrastructure;

/// <summary>
/// Helpers for managing the terminal cursor color via OSC escape sequences.
/// </summary>
public static class CursorColorHelper
{
    /// <summary>The OSC 12 sequence that sets the cursor color to the dotsider theme teal.</summary>
    public const string SetTealSequence = "\x1b]12;rgb:00/c8/b4\x1b\\";

    /// <summary>The OSC 112 sequence that resets the cursor color to the terminal default.</summary>
    public const string ResetSequence = "\x1b]112\x1b\\";

    /// <summary>
    /// Writes the OSC 12 sequence to set the cursor color to the dotsider theme teal.
    /// </summary>
    public static void SetThemeCursorColor() => Console.Write(SetTealSequence);

    /// <summary>
    /// Writes the OSC 112 sequence to reset the cursor color to the terminal default.
    /// </summary>
    public static void ResetCursorColor() => Console.Write(ResetSequence);
}
