using Dotsider.Core.Analysis.Models;

namespace Dotsider.Infrastructure;

/// <summary>
/// A complete CLI size-difference report.
/// Preserves the command's public JSON contract with a fixed typed shape.
/// Is registered with source-generated JSON metadata for Native AOT.
/// </summary>
internal sealed record CliSizeReportPayload(
    int SchemaVersion,
    string Target,
    string? Baseline,
    SizeBasis TotalBasis,
    long? LeftTotal,
    long RightTotal,
    long? LeftMstatTotal,
    long? RightMstatTotal,
    string LeftFormatVersion,
    string RightFormatVersion,
    SizeDiffSummary Summary,
    IReadOnlyList<SizeDiffAggregate> AssemblyDeltas,
    IReadOnlyList<SizeDiffAggregate> NamespaceDeltas,
    IReadOnlyList<CliSizeContributorPayload> Contributors,
    SizeBudgetReport? Budgets);
