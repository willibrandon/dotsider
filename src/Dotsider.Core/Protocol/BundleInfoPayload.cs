namespace Dotsider.Core.Protocol;

/// <summary>
/// Single-file bundle identity and total content size.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record BundleInfoPayload(
    bool IsBundle,
    int? MajorVersion = null,
    int? MinorVersion = null,
    int? FileCount = null,
    string? BundleId = null,
    long? TotalSize = null,
    string? Error = null);
