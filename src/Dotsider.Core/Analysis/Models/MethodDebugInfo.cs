namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Portable PDB debug information for a method.
/// </summary>
/// <param name="MethodToken">The method definition metadata token.</param>
/// <param name="Pdb">The portable PDB provenance.</param>
/// <param name="SequencePoints">Decoded sequence points for the method.</param>
/// <param name="Locals">Decoded local slots and PDB names for the method.</param>
public sealed record MethodDebugInfo(
    int MethodToken,
    PdbProvenance Pdb,
    IReadOnlyList<SequencePointInfo> SequencePoints,
    IReadOnlyList<LocalSlotInfo> Locals);
