namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// The outcome of evaluating one size budget: the measured values, any breached limits, and
/// the top positive regressions inside the budget's scope — the rows that explain a growth
/// breach, never diluted by improvements elsewhere.
/// </summary>
/// <param name="Budget">The budget that was evaluated.</param>
/// <param name="Passed">True when no limit was breached.</param>
/// <param name="Basis">
/// What the measured values count: total budgets use the check's total basis (file size for
/// binaries, mstat total for bare reports); namespace and assembly budgets always measure
/// mstat aggregates.
/// </param>
/// <param name="ActualBytes">The scope's current size in bytes.</param>
/// <param name="BaselineBytes">The scope's baseline size in bytes, or null when the check ran without a baseline.</param>
/// <param name="Violations">Each breached limit, or empty when the budget passed.</param>
/// <param name="TopContributors">
/// The scope's largest positive regressions (delta &gt; 0), ordered by delta descending, up to
/// the budget's or the caller's top-N.
/// </param>
public sealed record SizeBudgetEvaluation(
    SizeBudget Budget,
    bool Passed,
    SizeBasis Basis,
    long ActualBytes,
    long? BaselineBytes,
    IReadOnlyList<SizeBudgetViolation> Violations,
    IReadOnlyList<SizeDiffContributor> TopContributors);
