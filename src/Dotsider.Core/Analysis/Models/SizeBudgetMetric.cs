namespace Dotsider.Core.Analysis.Models;

/// <summary>The limit a size budget enforces.</summary>
public enum SizeBudgetMetric
{
    /// <summary>An absolute cap on the current value, in bytes.</summary>
    MaxBytes,

    /// <summary>A cap on growth versus the baseline, in bytes.</summary>
    MaxGrowthBytes,

    /// <summary>A cap on growth versus the baseline, as a percentage of the baseline.</summary>
    MaxGrowthPercent
}
