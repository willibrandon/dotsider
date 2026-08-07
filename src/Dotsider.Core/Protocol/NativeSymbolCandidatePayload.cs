namespace Dotsider.Core.Protocol;

/// <summary>
/// One candidate for an ambiguous native-symbol query.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record NativeSymbolCandidatePayload(string Address, string Name);
