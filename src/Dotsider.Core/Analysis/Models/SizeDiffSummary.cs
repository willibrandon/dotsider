namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// The headline figures of a size diff between two Native AOT builds. Totals are mstat
/// attributable bytes — the same figures <c>analyze --size</c> reports for each build alone.
/// </summary>
/// <param name="LeftTotal">The baseline build's total attributable bytes.</param>
/// <param name="RightTotal">The comparison build's total attributable bytes.</param>
/// <param name="Delta"><see cref="RightTotal"/> minus <see cref="LeftTotal"/>.</param>
/// <param name="UnchangedTotal">
/// The bytes carried by entries identical in both builds. The delta tree omits these; a
/// self-diff has <see cref="UnchangedTotal"/> equal to the build total and an empty tree.
/// </param>
/// <param name="Counts">Per-kind entry counts split by direction.</param>
/// <param name="LeftDeduplicatedMethods">The baseline build's deduplicated-method count (format 2.2+; informational — the entries carry no bytes).</param>
/// <param name="RightDeduplicatedMethods">The comparison build's deduplicated-method count.</param>
public sealed record SizeDiffSummary(
    long LeftTotal,
    long RightTotal,
    long Delta,
    long UnchangedTotal,
    IReadOnlyList<SizeDiffKindCounts> Counts,
    int LeftDeduplicatedMethods,
    int RightDeduplicatedMethods);
