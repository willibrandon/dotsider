namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// The column ranges of the mnemonic, operands, and target within a rendered disassembly line,
/// set by <see cref="Dotsider.Core.Analysis.Disasm.NativeDisassembler"/>'s text formatter. The
/// TUI decoration providers highlight and hit-test by these spans rather than re-parsing the line,
/// so the rendered text stays a pure projection of the structured instruction.
/// </summary>
/// <param name="MnemonicStart">The column (0-based) where the mnemonic begins in the rendered line.</param>
/// <param name="MnemonicLength">The mnemonic's length in characters.</param>
/// <param name="OperandsStart">The column where the operand text begins, or -1 when there are no operands.</param>
/// <param name="OperandsLength">The operand text's length, or 0.</param>
/// <param name="TargetStart">The column where the resolved-target comment begins, or -1 when there is none.</param>
/// <param name="TargetLength">The target comment's length, or 0.</param>
public readonly record struct NativeLineLayout(
    int MnemonicStart,
    int MnemonicLength,
    int OperandsStart,
    int OperandsLength,
    int TargetStart,
    int TargetLength);
