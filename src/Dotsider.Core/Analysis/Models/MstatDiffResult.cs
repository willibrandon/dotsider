namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// The complete result of comparing two ILC size reports: a hierarchical delta tree for
/// treemap rendering, headline figures, the flat contributor list a CI log prints, and the
/// per-assembly / per-namespace aggregates that size budgets evaluate against.
/// </summary>
/// <param name="LeftFormatVersion">The baseline report's format version (for example <c>"2.2"</c>; <c>"0.0"</c> for the empty baseline).</param>
/// <param name="RightFormatVersion">The comparison report's format version.</param>
/// <param name="Root">The delta tree — changed subtrees only, children ordered by absolute delta.</param>
/// <param name="Summary">Totals, unchanged mass, and per-kind direction counts.</param>
/// <param name="Contributors">Every changed entry, ordered by absolute delta descending. Callers trim to their top-N.</param>
/// <param name="AssemblyDeltas">Attributable bytes per assembly on both sides, ordered by absolute delta descending.</param>
/// <param name="NamespaceDeltas">Attributable bytes per namespace on both sides, folded across assemblies, ordered by absolute delta descending.</param>
public sealed record MstatDiffResult(
    string LeftFormatVersion,
    string RightFormatVersion,
    SizeDiffNode Root,
    SizeDiffSummary Summary,
    IReadOnlyList<SizeDiffContributor> Contributors,
    IReadOnlyList<SizeDiffAggregate> AssemblyDeltas,
    IReadOnlyList<SizeDiffAggregate> NamespaceDeltas);
