namespace Dotsider.Core.Protocol;

/// <summary>
/// Native AOT size-contributor query results.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record MstatContributorsPayload(
    MstatSourceSummaryPayload Source,
    MstatFiltersPayload Filters,
    int TotalMatches,
    int Returned,
    bool Truncated,
    bool? WhyAvailable,
    string? WhyUnavailableReason,
    IReadOnlyList<MstatContributorPayload> Contributors);
