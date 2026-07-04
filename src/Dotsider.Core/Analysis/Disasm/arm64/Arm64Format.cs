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
    /// <summary>#imm16 from bits[15:0] (udf — the permanently-undefined encoding; 0x00000000 is udf #0).</summary>
    Udf,
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

    // Loads and stores. The mnemonic and register width derive from the size/opc/V fields.
    /// <summary>Rt, [Rn|SP, #imm] (load/store register, unsigned scaled immediate offset).</summary>
    LdStUImm,
    /// <summary>Rt, [Rn|SP], #imm / [Rn|SP, #imm]! (load/store register, post/pre-indexed).</summary>
    LdStImmIndexed,
    /// <summary>Rt, [Rn|SP, #simm] (load/store register, unscaled signed offset: ldur/stur).</summary>
    LdStUnscaled,
    /// <summary>Rt, [Rn|SP, Rm{, extend/lsl}] (load/store register, register offset).</summary>
    LdStRegOff,
    /// <summary>Rt, Rt2, [Rn|SP{, #imm}] (load/store pair, with pre/post/signed variants).</summary>
    LdStPair,
    /// <summary>Rt, label (load register, PC-relative literal).</summary>
    LdLiteral,
    /// <summary>Ws, Rt, [Rn|SP] / Rt, [Rn|SP] (load/store exclusive).</summary>
    LdStExclusive,
    /// <summary>Rt, [Rn|SP] (load-acquire / store-release).</summary>
    LdStAcqRel,
    /// <summary>Rs, Rt, [Rn|SP] (LSE atomic read-modify-write).</summary>
    Atomic,

    // CRC32 (in the data-processing register group but with mixed register widths).
    /// <summary>Wd, Wn, Wm|Xm (crc32*/crc32c*).</summary>
    Crc,

    // Scalar floating-point and Advanced SIMD.
    /// <summary>Fd, Fn, Fm scalar (fadd/fsub/fmul/fdiv/fnmul).</summary>
    ScalarFp3,
    /// <summary>Fd, Fn scalar (fabs/fneg/fsqrt/fmov/frintX).</summary>
    ScalarFp2,
    /// <summary>Fn, Fm / Fn, #0.0 (fcmp/fcmpe).</summary>
    FpCompare,
    /// <summary>Fd, Fn with a type change (fcvt between half/single/double).</summary>
    FpCvt,
    /// <summary>Rd, Fn or Fd, Rn (fcvtzs/scvtf between integer and FP).</summary>
    FpToFromInt,
    /// <summary>Fd, Fn, Fm, cond (fcsel).</summary>
    FpCondSelect,
    /// <summary>Vd.T, Vn.T, Vm.T (SIMD three-same: add/mul/fadd/and/…).</summary>
    SimdReg3,
    /// <summary>Vd.T, Vn.T (SIMD two-register misc: neg/abs/not/fneg/…).</summary>
    SimdMisc2,
    /// <summary>Vd.T, Rn or Vd.T, Vn.Ts[i] (dup).</summary>
    SimdDup,
    /// <summary>Vd.Ts[i], Rn — insert a general register into a vector element (ins/mov).</summary>
    SimdInsGeneral,
    /// <summary>Rd, Vn.Ts[i] — move a vector element to a general register (smov/umov).</summary>
    SimdMovFromElement,
    /// <summary>Vd.T, #imm (movi/mvni).</summary>
    SimdModImm,
    /// <summary>Vd.T, Vn.T, Vm.T (SIMD dot-product: sdot/udot).</summary>
    SimdDot,
    /// <summary>Vd.16b, Vn.16b (AES round).</summary>
    CryptoAes,
    /// <summary>Vd, Vn, Vm (SHA update).</summary>
    CryptoSha,

    // SVE / SVE2 (scalable Z registers and predicate registers).
    /// <summary>Zd.T, Zn.T, Zm.T (SVE unpredicated arithmetic).</summary>
    SveArithUnpred,
    /// <summary>Zdn.T, Pg/m, Zdn.T, Zm.T (SVE predicated destructive arithmetic).</summary>
    SveArithPred,
    /// <summary>Zdn.T, Pg/m, Zn.T (SVE predicated unary).</summary>
    SveUnaryPred,
    /// <summary>Pd.T, pattern (ptrue/ptrues).</summary>
    SvePtrue,
    /// <summary>Pd.T, Rn, Rm (whilelt/whilelo/…).</summary>
    SveWhile,
    /// <summary>{Zt.T}, Pg/z, [Xn, Xm, lsl #n] (SVE contiguous load).</summary>
    SveLoad,
    /// <summary>{Zt.T}, Pg, [Xn, Xm, lsl #n] (SVE contiguous store).</summary>
    SveStore,
    /// <summary>Pd.T, Pg/z, Zn.T, #imm (SVE compare with immediate).</summary>
    SveCmpImm,
    /// <summary>Pd.T, Pg/z, Zn.T, Zm.T (SVE compare vectors).</summary>
    SveCmpVec,
    /// <summary>Zd, Zn (movprfx).</summary>
    SveMovprfx,
    /// <summary>Zd.T, #imm (mov/dup immediate).</summary>
    SveDupImm,
    /// <summary>Rd (incb/inch/incw/incd).</summary>
    SveInc,
}
