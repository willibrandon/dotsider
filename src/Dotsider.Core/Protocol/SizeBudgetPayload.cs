using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Protocol;

/// <summary>
/// Size-budget results for one mstat-backed input.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record SizeBudgetPayload(
    string Target,
    string? Baseline,
    bool Passed,
    bool HasWarnings,
    SizeBasis TotalBasis,
    long LeftTotal,
    long RightTotal,
    long? LeftMstatTotal,
    long? RightMstatTotal,
    IReadOnlyList<SizeBudgetEvaluation> Evaluations);
