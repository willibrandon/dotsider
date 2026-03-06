namespace Dotsider;

/// <summary>Vi-style editing mode for the hex dump tab.</summary>
public enum HexEditMode
{
    /// <summary>Read-only navigation mode (default).</summary>
    Normal,

    /// <summary>Byte-editing mode where keystrokes modify hex values.</summary>
    Insert
}
