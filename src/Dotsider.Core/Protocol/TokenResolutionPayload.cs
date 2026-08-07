namespace Dotsider.Core.Protocol;

/// <summary>
/// A metadata token resolution.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record TokenResolutionPayload(int Token, string Resolved);
