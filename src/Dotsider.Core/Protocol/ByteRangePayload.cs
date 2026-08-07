namespace Dotsider.Core.Protocol;

/// <summary>
/// Bytes read from a binary at a requested offset.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record ByteRangePayload(int Offset, int Length, string Hex, string Base64);
