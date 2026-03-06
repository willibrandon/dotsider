namespace Dotsider;

/// <summary>
/// Specifies which diff entries to display.
/// </summary>
public enum DiffFilterMode
{
    /// <summary>Show all diff entries (added, removed, and changed).</summary>
    All,

    /// <summary>Show only entries present in the new assembly but not the old.</summary>
    AddedOnly,

    /// <summary>Show only entries present in the old assembly but not the new.</summary>
    RemovedOnly,

    /// <summary>Show only entries that exist in both assemblies but differ.</summary>
    ChangedOnly
}
