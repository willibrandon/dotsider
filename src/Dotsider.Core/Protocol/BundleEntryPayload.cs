namespace Dotsider.Core.Protocol;

/// <summary>
/// One single-file bundle manifest entry.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record BundleEntryPayload(
    string RelativePath,
    string Type,
    long Size,
    long CompressedSize);
