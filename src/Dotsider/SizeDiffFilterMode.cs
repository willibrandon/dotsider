namespace Dotsider;

/// <summary>
/// Filter applied to the size-diff treemap. Size regressions need direction filters beyond
/// the managed diff's added/removed/changed: grown and shrunk are the two signs of a changed
/// entry, and CI triage flips between them constantly.
/// </summary>
public enum SizeDiffFilterMode
{
    /// <summary>Every changed entry.</summary>
    All,

    /// <summary>Entries present only in the right (newer) build.</summary>
    Added,

    /// <summary>Entries present only in the left (baseline) build.</summary>
    Removed,

    /// <summary>Entries present in both builds whose size increased.</summary>
    Grown,

    /// <summary>Entries present in both builds whose size decreased.</summary>
    Shrunk
}
