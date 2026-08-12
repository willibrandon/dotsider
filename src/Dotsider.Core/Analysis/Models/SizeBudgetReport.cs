namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// The outcome of checking a build against a set of size budgets. The check fails —
/// <see cref="Passed"/> is false — only when an error-severity budget breached; warning
/// breaches surface through <see cref="HasWarnings"/> without failing the check.
/// </summary>
/// <param name="Passed">False when at least one error-severity budget breached.</param>
/// <param name="HasWarnings">True when at least one warning-severity budget breached.</param>
/// <param name="TotalBasis">What the total figures count: file size when the inputs were binaries, mstat totals otherwise.</param>
/// <param name="LeftTotal">The baseline total on <see cref="TotalBasis"/>, or 0 when the check ran without a baseline.</param>
/// <param name="RightTotal">The current total on <see cref="TotalBasis"/>.</param>
/// <param name="LeftMstatTotal">The baseline's mstat attributable total, surfaced alongside when <see cref="TotalBasis"/> is file size; null otherwise.</param>
/// <param name="RightMstatTotal">The current build's mstat attributable total, surfaced alongside when <see cref="TotalBasis"/> is file size; null otherwise.</param>
/// <param name="Evaluations">One outcome per budget, in input order.</param>
public sealed record SizeBudgetReport(
    bool Passed,
    bool HasWarnings,
    SizeBasis TotalBasis,
    long LeftTotal,
    long RightTotal,
    long? LeftMstatTotal,
    long? RightMstatTotal,
    IReadOnlyList<SizeBudgetEvaluation> Evaluations)
{
    /// <summary>
    /// True when one or more growth limits were deferred because no baseline exists.
    /// </summary>
    public bool HasDeferred { get; init; }
}
