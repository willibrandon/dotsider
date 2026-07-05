namespace Dotsider;

/// <summary>
/// The IL Inspector pane that owns keyboard focus for search and navigation when a
/// pre-ILC companion set is attached and IL and native code render side by side.
/// </summary>
public enum IlPane
{
    /// <summary>The left tree list.</summary>
    Tree,

    /// <summary>The IL disassembly editor.</summary>
    Il,

    /// <summary>The native disassembly pair pane.</summary>
    Native,
}
