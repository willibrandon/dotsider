namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// A single decoded IL (Intermediate Language) instruction.
/// </summary>
/// <param name="Offset">The byte offset of this instruction within the method body.</param>
/// <param name="OpCode">The IL opcode mnemonic (e.g., "ldstr", "call", "ret").</param>
/// <param name="Operand">The decoded operand as a display string, or empty if the opcode takes no operand.</param>
/// <param name="MetadataToken">The raw metadata token for token-bearing operands (methods, fields, types), or null.</param>
public sealed record IlInstruction(
    int Offset,
    string OpCode,
    string Operand,
    int? MetadataToken = null);
