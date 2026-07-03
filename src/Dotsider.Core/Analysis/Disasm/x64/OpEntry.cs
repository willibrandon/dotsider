namespace Dotsider.Core.Analysis.Disasm.x64;

/// <summary>
/// One slot of an x86-64 opcode table: the mnemonic (null for an empty slot), up to four operand
/// kinds, the row flags, and a group/tuple byte. Length and text are both projections of these
/// fields — the operand kinds imply ModRM presence, displacement, and immediate size — so there is
/// no separate length table to drift from the mnemonic table.
/// </summary>
/// <param name="Mnemonic">The instruction mnemonic, or null for an empty/undefined slot.</param>
/// <param name="Op1">The first operand kind.</param>
/// <param name="Op2">The second operand kind.</param>
/// <param name="Op3">The third operand kind.</param>
/// <param name="Op4">The fourth operand kind.</param>
/// <param name="Flags">Row-level properties.</param>
/// <param name="GroupOrTuple">The group-table index when <see cref="OpFlags.Group"/> is set; the EVEX tuple type otherwise.</param>
internal readonly record struct OpEntry(
    string? Mnemonic,
    OperandKind Op1,
    OperandKind Op2,
    OperandKind Op3,
    OperandKind Op4,
    OpFlags Flags,
    byte GroupOrTuple)
{
    /// <summary>Whether this slot holds no instruction.</summary>
    public bool IsEmpty => Mnemonic is null && (Flags & (OpFlags.Group | OpFlags.Prefix)) == 0;

    /// <summary>
    /// Whether decoding this row consumes a ModRM byte — any register/memory operand, an explicit
    /// flag, or a group row.
    /// </summary>
    public bool HasModRm =>
        (Flags & (OpFlags.HasModRm | OpFlags.Group)) != 0
        || IsModRmOperand(Op1) || IsModRmOperand(Op2) || IsModRmOperand(Op3) || IsModRmOperand(Op4);

    private static bool IsModRmOperand(OperandKind k) => k is
        OperandKind.Eb or OperandKind.Ew or OperandKind.Ed or OperandKind.Eq or OperandKind.Ev or OperandKind.Ey
        or OperandKind.Gb or OperandKind.Gw or OperandKind.Gd or OperandKind.Gv or OperandKind.Gy
        or OperandKind.M or OperandKind.Mv or OperandKind.Sw
        or OperandKind.Vx or OperandKind.Wx or OperandKind.Kr or OperandKind.Km
        or OperandKind.Pq or OperandKind.Qq;
}
