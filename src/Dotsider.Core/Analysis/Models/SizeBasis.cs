namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// What a total-size figure measures. A binary on disk and the sum of its mstat entries are
/// different numbers (headers, alignment, and unreported bytes sit between them), so every
/// report states which basis it used.
/// </summary>
public enum SizeBasis
{
    /// <summary>The binary's file size on disk.</summary>
    FileSize,

    /// <summary>The sum of the mstat report's attributable entries.</summary>
    MstatTotal
}
