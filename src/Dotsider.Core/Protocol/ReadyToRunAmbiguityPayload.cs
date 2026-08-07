using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Protocol;

/// <summary>
/// An ambiguous ReadyToRun method query.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record ReadyToRunAmbiguityPayload(
    string Error,
    string Target,
    IReadOnlyList<CorrelationCandidate> Candidates);
