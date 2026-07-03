namespace Dotsider.Core.Analysis.Disasm.arm64;

/// <summary>
/// The operand-decoding shape of an A64 table row. AArch64 packs operands differently per
/// instruction class, so each row names a format the decoder's formatter switches on to extract the
/// register, immediate, shift, condition, and branch-offset fields and render them. New groups
/// (loads/stores, SIMD/FP, SVE) extend this enum.
/// </summary>
internal enum Arm64Format
{
    /// <summary>No operands.</summary>
    None,

    // Data processing — immediate.
    /// <summary>Rd, Rn, #imm12 with optional LSL #12 (add/sub immediate).</summary>
    AddSubImm,
    /// <summary>Rd|SP, Rn, #bitmask (logical immediate).</summary>
    LogicalImm,
    /// <summary>Rd, #imm16 with optional LSL (movz/movn/movk).</summary>
    MoveWide,
    /// <summary>Rd, Rn, #immr, #imms (bitfield: sbfm/bfm/ubfm and aliases).</summary>
    Bitfield,
    /// <summary>Rd, Rn, Rm, #lsb (extract: extr).</summary>
    Extract,
    /// <summary>Rd, label (adr).</summary>
    Adr,
    /// <summary>Rd, page-label (adrp).</summary>
    Adrp,

    // Branches, exception, system.
    /// <summary>label from imm26 (b/bl).</summary>
    BranchImm26,
    /// <summary>label from imm19 with a condition (b.cond).</summary>
    BranchCond,
    /// <summary>Rt, label from imm19 (cbz/cbnz).</summary>
    CompareBranch,
    /// <summary>Rt, #bit, label from imm14 (tbz/tbnz).</summary>
    TestBranch,
    /// <summary>Rn register indirect (br/blr/ret).</summary>
    BranchReg,
    /// <summary>#imm16 (svc/hvc/brk).</summary>
    Exception,
    /// <summary>No operands or a hint immediate (nop/yield/…).</summary>
    Hint,
    /// <summary>Barrier with an optional option (dmb/dsb/isb).</summary>
    Barrier,
    /// <summary>System register move (mrs/msr).</summary>
    SystemReg,

    // Data processing — register.
    /// <summary>Rd, Rn, Rm, shift #amount (add/sub/logical shifted register).</summary>
    ShiftedReg,
    /// <summary>Rd|SP, Rn|SP, Rm, extend #amount (add/sub extended register).</summary>
    ExtendedReg,
    /// <summary>Rd, Rn, Rm (two-source: udiv/sdiv/lslv/…).</summary>
    DataProc2,
    /// <summary>Rd, Rn (one-source: rbit/clz/rev/…).</summary>
    DataProc1,
    /// <summary>Rd, Rn, Rm, Ra (three-source: madd/msub/…).</summary>
    DataProc3,
    /// <summary>Rd, Rn, Rm, cond (conditional select: csel/csinc/…).</summary>
    CondSelect,
    /// <summary>Rn, Rm, #nzcv, cond (conditional compare register).</summary>
    CondCompareReg,
    /// <summary>Rn, #imm5, #nzcv, cond (conditional compare immediate).</summary>
    CondCompareImm,
}
