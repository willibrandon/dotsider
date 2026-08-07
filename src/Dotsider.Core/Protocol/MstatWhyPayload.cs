namespace Dotsider.Core.Protocol;

/// <summary>
/// Outcome of a Native AOT dependency explanation query.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record MstatWhyPayload(
    string Target,
    MstatSourceSummaryPayload Source,
    string Outcome,
    string? Message = null,
    int? CandidateCount = null,
    IReadOnlyList<MstatCandidatePayload>? Candidates = null,
    bool? Truncated = null,
    MstatContributorPayload? Contributor = null);
