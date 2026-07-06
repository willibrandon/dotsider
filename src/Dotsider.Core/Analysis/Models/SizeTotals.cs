namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// The basis-resolved totals of a size comparison: file sizes when every provided input is a
/// binary, mstat attributable totals when a bare <c>.mstat</c> is anywhere in the pair — both
/// sides always share one basis so the figures stay comparable.
/// </summary>
/// <param name="Basis">What the totals count.</param>
/// <param name="RightTotal">The current build's total on <see cref="Basis"/>.</param>
/// <param name="LeftTotal">The baseline's total on <see cref="Basis"/>, or null when there is no baseline.</param>
public sealed record SizeTotals(
    SizeBasis Basis,
    long RightTotal,
    long? LeftTotal);
