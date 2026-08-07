using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Protocol;

/// <summary>
/// IL and optional portable-PDB data for one method.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record IlDisassemblyPayload(
    MethodDefInfo Method,
    PdbProvenance Pdb,
    SourceLinkInfo SourceLink,
    MethodDebugInfo? DebugInfo,
    IReadOnlyList<IlInstruction> Instructions);
