using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Protocol;

/// <summary>
/// Decoded native instructions for one symbol.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record NativeDisassemblyPayload(
    string Symbol,
    string Architecture,
    IReadOnlyList<NativeInstruction> Instructions);
