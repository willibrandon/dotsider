namespace Dotsider.Core.Protocol;

/// <summary>
/// A single-file bundle probe result.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record BundleProbePayload(bool IsBundle, long HeaderOffset);
