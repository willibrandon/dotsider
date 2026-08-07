namespace Dotsider.Core.Protocol;

/// <summary>
/// An ambiguous native-symbol query.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record NativeSymbolAmbiguityPayload(
    string Error,
    string Target,
    IReadOnlyList<NativeSymbolCandidatePayload> Candidates);
