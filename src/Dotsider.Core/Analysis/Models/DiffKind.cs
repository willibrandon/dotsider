namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Describes the kind of difference detected between two assembly elements.
/// </summary>
public enum DiffKind
{
    /// <summary>The element exists only in the right (newer) assembly.</summary>
    Added,

    /// <summary>The element exists only in the left (older) assembly.</summary>
    Removed,

    /// <summary>The element exists in both assemblies but has been modified.</summary>
    Changed,

    /// <summary>The element is identical in both assemblies.</summary>
    Unchanged
}
