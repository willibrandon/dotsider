namespace Dotsider.Core.Analysis.Disasm.x64;

/// <summary>
/// The operand kinds an x86-64 table row carries, in Intel opcode-map notation (addressing letter
/// + size code) so a table row diffs 1:1 against the manual. The addressing letter picks the
/// field — <c>E</c> = ModRM r/m, <c>G</c> = ModRM reg, <c>V</c>/<c>H</c>/<c>W</c>/<c>L</c> = vector
/// registers, <c>I</c> = immediate, <c>J</c> = relative, <c>O</c> = moffs, <c>Z</c> =
/// opcode-embedded register — and the size code plus the decoded operand size / REX.W / VEX.L pick
/// the register file and width. The presence of any <c>E</c>/<c>M</c>/<c>G</c>/<c>V</c>/<c>W</c>
/// operand (or a group row) means the instruction carries a ModRM byte.
/// </summary>
internal enum OperandKind : byte
{
    /// <summary>No operand in this slot.</summary>
    None = 0,

    // ModRM r/m (E) — general-purpose, sized.
    /// <summary>ModRM r/m, byte.</summary>
    Eb,
    /// <summary>ModRM r/m, word.</summary>
    Ew,
    /// <summary>ModRM r/m, dword.</summary>
    Ed,
    /// <summary>ModRM r/m, quadword.</summary>
    Eq,
    /// <summary>ModRM r/m, operand-size (16/32/64).</summary>
    Ev,
    /// <summary>ModRM r/m, always 64-bit for a memory reference (e.g. LEA/branch targets).</summary>
    Ey,

    // ModRM reg (G) — general-purpose, sized.
    /// <summary>ModRM reg, byte.</summary>
    Gb,
    /// <summary>ModRM reg, word.</summary>
    Gw,
    /// <summary>ModRM reg, dword.</summary>
    Gd,
    /// <summary>ModRM reg, operand-size.</summary>
    Gv,
    /// <summary>ModRM reg, quadword.</summary>
    Gy,

    // Memory-only.
    /// <summary>A memory reference (ModRM must be a memory form).</summary>
    M,
    /// <summary>A memory reference, operand-size (e.g. LEA source).</summary>
    Mv,

    // Immediates.
    /// <summary>Immediate byte.</summary>
    Ib,
    /// <summary>Immediate word.</summary>
    Iw,
    /// <summary>Immediate dword.</summary>
    Id,
    /// <summary>Immediate, 16/32 by operand-size (never 64).</summary>
    Iz,
    /// <summary>Immediate, 16/32/64 by operand-size.</summary>
    Iv,

    // Relative code targets.
    /// <summary>Relative byte target (rel8).</summary>
    Jb,
    /// <summary>Relative 16/32 target by operand-size.</summary>
    Jz,

    // Moffs / opcode-embedded registers / implicit.
    /// <summary>Absolute moffs address, byte operand (mov AL,[moffs]).</summary>
    Ob,
    /// <summary>Absolute moffs address, operand-size (mov eAX,[moffs]).</summary>
    Ov,
    /// <summary>General-purpose register from VEX/EVEX vvvv, operand-size (BMI ops).</summary>
    By,
    /// <summary>Opcode-embedded register (opcode low 3 bits + REX.B), byte.</summary>
    Zb,
    /// <summary>Opcode-embedded register, operand-size.</summary>
    Zv,
    /// <summary>The accumulator (AL/AX/EAX/RAX) by operand-size.</summary>
    RAX,
    /// <summary>The AL register.</summary>
    AL,
    /// <summary>The DX register (for IN/OUT).</summary>
    DX,
    /// <summary>The CL register (shift/rotate count).</summary>
    CL,
    /// <summary>The constant 1 (shift/rotate by one).</summary>
    One,
    /// <summary>A segment register (ModRM reg as sreg).</summary>
    Sw,

    // Vector operands (populated by the SSE/AVX/AVX-512 tables).
    /// <summary>Vector reg from ModRM reg (xmm/ymm/zmm by length).</summary>
    Vx,
    /// <summary>Vector reg from ModRM r/m (xmm/ymm/zmm by length).</summary>
    Wx,
    /// <summary>Vector reg or memory from ModRM r/m, scalar-single (4-byte memory size hint).</summary>
    Wss,
    /// <summary>Vector reg or memory from ModRM r/m, scalar-double (8-byte memory size hint).</summary>
    Wsd,
    /// <summary>Vector reg or memory from ModRM r/m, always 128-bit xmm regardless of vector length (insert/extract-128).</summary>
    Wxmm,
    /// <summary>Vector reg from VEX/EVEX vvvv (xmm/ymm/zmm by length).</summary>
    Hx,
    /// <summary>Vector reg from imm8[7:4] (is4 operand).</summary>
    Lx,
    /// <summary>Mask register (k0-k7) from ModRM reg.</summary>
    Kr,
    /// <summary>Mask register from ModRM r/m.</summary>
    Km,
    /// <summary>Mask register from VEX/EVEX vvvv.</summary>
    Kv,
    /// <summary>MMX register from ModRM reg.</summary>
    Pq,
    /// <summary>MMX register or memory from ModRM r/m.</summary>
    Qq,
    /// <summary>x87 ST(0).</summary>
    St0,
    /// <summary>x87 ST(i) from opcode low 3 bits.</summary>
    Sti,
}
