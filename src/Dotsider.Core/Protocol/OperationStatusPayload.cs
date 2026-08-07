namespace Dotsider.Core.Protocol;

/// <summary>
/// A queued-operation status.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record OperationStatusPayload(string Status);
