namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// A single decoded IL (Intermediate Language) instruction.
/// </summary>
/// <param name="Offset">The byte offset of this instruction within the method body.</param>
/// <param name="OpCode">The IL opcode mnemonic (e.g., "ldstr", "call", "ret").</param>
/// <param name="Operand">The decoded operand as a display string, or empty if the opcode takes no operand.</param>
/// <param name="MetadataToken">The raw metadata token for token-bearing operands (methods, fields, types), or null.</param>
/// <param name="SequenceDocument">The source document for a sequence point starting at this instruction, or null.</param>
/// <param name="SequenceStartLine">The sequence point start line, or null.</param>
/// <param name="SequenceStartColumn">The sequence point start column, or null.</param>
/// <param name="SequenceEndLine">The sequence point end line, or null.</param>
/// <param name="SequenceEndColumn">The sequence point end column, or null.</param>
/// <param name="SequenceHidden">Whether the sequence point is hidden.</param>
/// <param name="SourceLinkUrl">The Source Link URL resolved for the sequence point document, or null.</param>
/// <param name="HasEmbeddedSource">Whether the sequence point document has embedded source.</param>
/// <param name="LocalSlot">The local variable slot referenced by this instruction, or null.</param>
/// <param name="LocalName">The active PDB local variable name for <paramref name="LocalSlot"/>, or null.</param>
/// <param name="DisplayLine">The 1-based rendered line number in formatted disassembly, or null.</param>
public sealed record IlInstruction(
    int Offset,
    string OpCode,
    string Operand,
    int? MetadataToken = null,
    string? SequenceDocument = null,
    int? SequenceStartLine = null,
    int? SequenceStartColumn = null,
    int? SequenceEndLine = null,
    int? SequenceEndColumn = null,
    bool SequenceHidden = false,
    string? SourceLinkUrl = null,
    bool HasEmbeddedSource = false,
    int? LocalSlot = null,
    string? LocalName = null,
    int? DisplayLine = null);
