namespace Dotsider.Core.Analysis.Disasm.arm64;

/// <summary>
/// One row of an A64 decode group: a mask/match pair over the 32-bit instruction word, the
/// mnemonic, and the operand <see cref="Arm64Format"/>. Rows are scanned most-specific-first (by
/// descending mask population) so a specific alias matches before its generic parent. A word that
/// matches no row in its group decodes as a <c>.word</c>.
/// </summary>
/// <param name="Mask">The bits that must match <see cref="Match"/>.</param>
/// <param name="Match">The required values of the masked bits.</param>
/// <param name="Mnemonic">The instruction mnemonic.</param>
/// <param name="Format">How the decoder extracts and renders the operands.</param>
internal readonly record struct Arm64Entry(uint Mask, uint Match, string Mnemonic, Arm64Format Format)
{
    /// <summary>Whether <paramref name="word"/> matches this row.</summary>
    /// <param name="word">The 32-bit instruction word.</param>
    public bool Matches(uint word) => (word & Mask) == Match;
}
