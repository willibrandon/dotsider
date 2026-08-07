using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Protocol;

/// <summary>
/// Opcode matches within one method.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record IlSearchResultPayload(
    string Method,
    IReadOnlyList<IlInstruction> Matches);
