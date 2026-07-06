namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// How a failed size budget affects the outcome: an error fails the check (the CI gate exits
/// non-zero), a warning is reported but never changes the exit code.
/// </summary>
public enum SizeBudgetSeverity
{
    /// <summary>A breach fails the check.</summary>
    Error,

    /// <summary>A breach is reported without failing the check.</summary>
    Warning
}
