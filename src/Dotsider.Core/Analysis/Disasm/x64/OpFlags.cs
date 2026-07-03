namespace Dotsider.Core.Analysis.Disasm.x64;

/// <summary>
/// Row-level properties an x86-64 opcode entry carries beyond its operand kinds — the bits the
/// decoder needs for exact length and for 64-bit operand-size defaults.
/// </summary>
[Flags]
internal enum OpFlags : byte
{
    /// <summary>No special properties.</summary>
    None = 0,

    /// <summary>Carries a ModRM byte even when the operand kinds do not obviously imply one.</summary>
    HasModRm = 1,

    /// <summary><see cref="OpEntry.GroupOrTuple"/> indexes the group table by ModRM.reg.</summary>
    Group = 2,

    /// <summary>Default operand size is 64-bit in long mode (push/pop/call/jmp/branch); 66 → 16-bit.</summary>
    Default64 = 4,

    /// <summary>Operand size is forced to 64-bit regardless of prefixes/REX.W.</summary>
    Force64 = 8,

    /// <summary>This "opcode" byte is a legacy prefix, not an instruction.</summary>
    Prefix = 16,

    /// <summary>Explicitly reserved/undefined (#UD); length is not defined — one-byte fallback.</summary>
    Undefined = 32,

    /// <summary>The mnemonic is complete as written and must not receive the automatic VEX <c>v</c> prefix (BMI/GPR VEX ops).</summary>
    NoVexPrefix = 64,

    /// <summary>Under EVEX the mnemonic takes a <c>d</c> (W=0) or <c>q</c> (W=1) element-width suffix (EVEX-only ops).</summary>
    EvexDQ = 128,
}
