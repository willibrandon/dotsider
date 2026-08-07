namespace Dotsider.Core.Protocol;

/// <summary>
/// Filters applied to an mstat contributor query.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record MstatFiltersPayload(
    string? Query,
    string? Section,
    string? AssemblyName,
    string? Namespace);
