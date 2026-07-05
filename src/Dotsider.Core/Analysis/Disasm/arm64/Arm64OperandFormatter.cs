using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis.Disasm.arm64;

using R = Arm64Registers;

/// <summary>
/// Extracts and renders the operands of a matched A64 row per its <see cref="Arm64Format"/>: the
/// register fields, the decoded immediates (including the logical bitmask immediate), the shift/
/// extend qualifiers, the condition, and the absolute branch/label target. It also rewrites the
/// common architectural aliases (mov/cmp/cmn/tst/neg/mul/lsl/lsr/asr/cset/…) the way the assembler
/// and objdump present them.
/// </summary>
internal static partial class Arm64OperandFormatter
{
    /// <summary>Formats one instruction word matched to <paramref name="entry"/>.</summary>
    /// <param name="entry">The matched decode row.</param>
    /// <param name="word">The 32-bit instruction word.</param>
    /// <param name="address">The instruction's virtual address (for branch targets).</param>
    public static Arm64Decoded Format(Arm64Entry entry, uint word, ulong address)
    {
        var sf = ((word >> 31) & 1) == 1;
        int rd = (int)(word & 0x1F), rn = (int)((word >> 5) & 0x1F);
        int rm = (int)((word >> 16) & 0x1F), ra = (int)((word >> 10) & 0x1F);
        var ops = new List<NativeOperand>(4);
        var mnem = entry.Mnemonic;
        var cat = NativeInstructionCategory.Integer;
        var flow = NativeFlowKind.Sequential;
        ulong? target = null;

        switch (entry.Format)
        {
            case Arm64Format.AddSubImm:
            {
                var imm = (word >> 10) & 0xFFF;
                var shift = ((word >> 22) & 1) == 1;
                Reg(ops, R.GprSp(rd, sf));
                Reg(ops, R.GprSp(rn, sf));
                Imm(ops, shift ? $"#0x{imm:x}, lsl #12" : $"#0x{imm:x}", imm);
                RewriteAddSubImm(ref mnem, ops, rd, rn, imm, shift);
                break;
            }

            case Arm64Format.LogicalImm:
            {
                var n = (word >> 22) & 1;
                var immr = (word >> 16) & 0x3F;
                var imms = (word >> 10) & 0x3F;
                var value = DecodeBitMask(sf, n, imms, immr);
                Reg(ops, R.GprSp(rd, sf));
                Reg(ops, R.Gpr(rn, sf));
                Imm(ops, $"#0x{value:x}", (long)value);
                if (mnem == "ands" && rd == 31) { mnem = "tst"; ops.RemoveAt(0); }
                else if (mnem == "orr" && rn == 31) { mnem = "mov"; ops.RemoveAt(1); }
                break;
            }

            case Arm64Format.MoveWide:
            {
                var imm16 = (word >> 5) & 0xFFFF;
                var hw = (int)((word >> 21) & 3);
                Reg(ops, R.Gpr(rd, sf));

                // movz with no shift is the mov (wide immediate) alias.
                if (mnem == "movz" && hw == 0)
                {
                    mnem = "mov";
                    Imm(ops, $"#0x{imm16:x}", imm16);
                }
                else
                {
                    Imm(ops, hw == 0 ? $"#0x{imm16:x}" : $"#0x{imm16:x}, lsl #{hw * 16}", imm16);
                }

                break;
            }

            case Arm64Format.Bitfield:
                FormatBitfield(ref mnem, ops, word, sf, rd, rn);
                break;

            case Arm64Format.Extract:
            {
                var imms = (word >> 10) & 0x3F;
                Reg(ops, R.Gpr(rd, sf));
                Reg(ops, R.Gpr(rn, sf));
                Reg(ops, R.Gpr(rm, sf));
                Imm(ops, $"#0x{imms:x}", imms);
                if (rn == rm) { mnem = "ror"; ops.RemoveAt(2); }
                break;
            }

            case Arm64Format.Adr:
            {
                var imm = SignExtend(((word >> 5) & 0x7FFFF) << 2 | ((word >> 29) & 3), 21);
                target = unchecked(address + (ulong)imm);
                Reg(ops, R.Gpr(rd, true));
                Label(ops, target.Value);
                cat = NativeInstructionCategory.Integer;
                break;
            }

            case Arm64Format.Adrp:
            {
                var imm = SignExtend(((word >> 5) & 0x7FFFF) << 2 | ((word >> 29) & 3), 21) << 12;
                target = unchecked((address & ~0xFFFUL) + (ulong)imm);
                Reg(ops, R.Gpr(rd, true));
                Label(ops, target.Value);
                break;
            }

            case Arm64Format.BranchImm26:
            {
                var off = SignExtend(word & 0x3FFFFFF, 26) << 2;
                target = unchecked(address + (ulong)off);
                Label(ops, target.Value);
                cat = NativeInstructionCategory.Control;
                flow = mnem == "bl" ? NativeFlowKind.Call : NativeFlowKind.Jump;
                break;
            }

            case Arm64Format.BranchCond:
            {
                var off = SignExtend((word >> 5) & 0x7FFFF, 19) << 2;
                target = unchecked(address + (ulong)off);
                mnem = "b." + R.Condition((int)(word & 0xF));
                Label(ops, target.Value);
                cat = NativeInstructionCategory.Control;
                flow = NativeFlowKind.ConditionalBranch;
                break;
            }

            case Arm64Format.CompareBranch:
            {
                var off = SignExtend((word >> 5) & 0x7FFFF, 19) << 2;
                target = unchecked(address + (ulong)off);
                Reg(ops, R.Gpr(rd, sf));
                Label(ops, target.Value);
                cat = NativeInstructionCategory.Control;
                flow = NativeFlowKind.ConditionalBranch;
                break;
            }

            case Arm64Format.TestBranch:
            {
                var bit = (int)(((word >> 31) & 1) << 5 | ((word >> 19) & 0x1F));
                var off = SignExtend((word >> 5) & 0x3FFF, 14) << 2;
                target = unchecked(address + (ulong)off);
                Reg(ops, R.Gpr(rd, bit >= 32));
                Imm(ops, $"#0x{bit:x}", bit);
                Label(ops, target.Value);
                cat = NativeInstructionCategory.Control;
                flow = NativeFlowKind.ConditionalBranch;
                break;
            }

            case Arm64Format.BranchReg:
                cat = NativeInstructionCategory.Control;
                flow = mnem == "blr" ? NativeFlowKind.IndirectCall
                    : mnem == "ret" ? NativeFlowKind.Return : NativeFlowKind.IndirectJump;
                if (!(mnem == "ret" && rn == 30)) Reg(ops, R.Gpr(rn, true));
                break;

            case Arm64Format.Exception:
                Imm(ops, $"#0x{(word >> 5) & 0xFFFF:x}", (word >> 5) & 0xFFFF);
                cat = NativeInstructionCategory.System;
                break;

            case Arm64Format.Udf:
                Imm(ops, $"#0x{word & 0xFFFF:x}", word & 0xFFFF);
                cat = NativeInstructionCategory.System;
                break;

            case Arm64Format.Hint:
            case Arm64Format.Barrier:
                cat = NativeInstructionCategory.System;
                break;

            case Arm64Format.SystemReg:
                cat = NativeInstructionCategory.System;
                Reg(ops, R.Gpr(rd, true));
                break;

            case Arm64Format.ShiftedReg:
                FormatShiftedReg(ref mnem, ops, word, sf, rd, rn, rm);
                break;

            case Arm64Format.ExtendedReg:
                FormatExtendedReg(ref mnem, ops, word, sf, rd, rn, rm);
                break;

            case Arm64Format.DataProc2:
                Reg(ops, R.Gpr(rd, sf));
                Reg(ops, R.Gpr(rn, sf));
                Reg(ops, R.Gpr(rm, sf));
                if (mnem is "lslv" or "lsrv" or "asrv" or "rorv") mnem = mnem[..^1];
                break;

            case Arm64Format.DataProc1:
                Reg(ops, R.Gpr(rd, sf));
                Reg(ops, R.Gpr(rn, sf));
                break;

            case Arm64Format.DataProc3:
                Reg(ops, R.Gpr(rd, sf));
                Reg(ops, R.Gpr(rn, sf));
                Reg(ops, R.Gpr(rm, sf));
                if (ra == 31 && mnem == "madd") mnem = "mul";
                else if (ra == 31 && mnem == "msub") mnem = "mneg";
                else Reg(ops, R.Gpr(ra, sf));
                break;

            case Arm64Format.CondSelect:
            {
                var cond = R.Condition((int)((word >> 12) & 0xF));
                Reg(ops, R.Gpr(rd, sf));
                Reg(ops, R.Gpr(rn, sf));
                Reg(ops, R.Gpr(rm, sf));
                Imm(ops, cond, 0);

                // csinc Rd, xzr, xzr, cond → cset Rd, invert(cond).
                if (mnem == "csinc" && rn == 31 && rm == 31)
                {
                    mnem = "cset";
                    ops.RemoveRange(1, 3);
                    Imm(ops, R.Condition((int)((word >> 12) & 0xF) ^ 1), 0);
                }

                break;
            }

            case Arm64Format.CondCompareReg:
            case Arm64Format.CondCompareImm:
            {
                var nzcv = word & 0xF;
                var cond = R.Condition((int)((word >> 12) & 0xF));
                Reg(ops, R.Gpr(rn, sf));
                if (entry.Format == Arm64Format.CondCompareImm) Imm(ops, $"#0x{rm:x}", rm);
                else Reg(ops, R.Gpr(rm, sf));
                Imm(ops, $"#0x{nzcv:x}", (long)nzcv);
                Imm(ops, cond, 0);
                break;
            }

            case Arm64Format.LdStUImm:
            case Arm64Format.LdStUnscaled:
            case Arm64Format.LdStImmIndexed:
            case Arm64Format.LdStRegOff:
            case Arm64Format.LdStPair:
            case Arm64Format.LdLiteral:
            case Arm64Format.LdStExclusive:
            case Arm64Format.Atomic:
                mnem = FormatLoadStore(entry.Format, ops, word, rd, rn, rm, ra, address, ref target);
                break;

            case Arm64Format.Crc:
                Reg(ops, R.Gpr(rd, false));
                Reg(ops, R.Gpr(rn, false));
                Reg(ops, R.Gpr(rm, mnem.EndsWith('x')));
                break;

            case Arm64Format.ScalarFp3:
            case Arm64Format.ScalarFp2:
            case Arm64Format.FpCompare:
            case Arm64Format.FpCvt:
            case Arm64Format.FpToFromInt:
            case Arm64Format.FpCondSelect:
            case Arm64Format.SimdReg3:
            case Arm64Format.SimdMisc2:
            case Arm64Format.SimdDup:
            case Arm64Format.SimdInsGeneral:
            case Arm64Format.SimdMovFromElement:
            case Arm64Format.SimdModImm:
            case Arm64Format.SimdDot:
            case Arm64Format.CryptoAes:
            case Arm64Format.CryptoSha:
                cat = NativeInstructionCategory.Float;
                FormatSimd(entry.Format, mnem, ops, word, rd, rn, rm);
                break;

            case Arm64Format.SveArithUnpred:
            case Arm64Format.SveArithPred:
            case Arm64Format.SveUnaryPred:
            case Arm64Format.SvePtrue:
            case Arm64Format.SveWhile:
            case Arm64Format.SveLoad:
            case Arm64Format.SveStore:
            case Arm64Format.SveCmpImm:
            case Arm64Format.SveCmpVec:
            case Arm64Format.SveMovprfx:
            case Arm64Format.SveDupImm:
            case Arm64Format.SveInc:
                cat = NativeInstructionCategory.Vector;
                FormatSve(entry.Format, mnem, ops, word, rd, rn, rm);
                break;
        }

        return new Arm64Decoded(mnem, ops, cat, flow, target);
    }

    private static void FormatBitfield(ref string mnem, List<NativeOperand> ops, uint word, bool sf, int rd, int rn)
    {
        var immr = (int)((word >> 16) & 0x3F);
        var imms = (int)((word >> 10) & 0x3F);
        var width = sf ? 64 : 32;
        var u = mnem == "ubfm";
        var s = mnem == "sbfm";

        // sxtw x, w (sbfm, immr=0, imms=31).
        if (s && immr == 0 && imms == 31 && sf)
        {
            mnem = "sxtw";
            Reg(ops, R.Gpr(rd, true)); Reg(ops, R.Gpr(rn, false));
            return;
        }

        // Shift-immediate aliases.
        if (u && imms != width - 1 && imms + 1 == immr)
        {
            mnem = "lsl";
            Reg(ops, R.Gpr(rd, sf)); Reg(ops, R.Gpr(rn, sf));
            Imm(ops, $"#0x{width - 1 - imms:x}", width - 1 - imms);
            return;
        }

        if ((u || s) && imms == width - 1)
        {
            mnem = u ? "lsr" : "asr";
            Reg(ops, R.Gpr(rd, sf)); Reg(ops, R.Gpr(rn, sf));
            Imm(ops, $"#0x{immr:x}", immr);
            return;
        }

        // Bit-extract (imms >= immr) vs bit-insert-into-zero (imms < immr).
        Reg(ops, R.Gpr(rd, sf));
        Reg(ops, R.Gpr(rn, sf));
        if (imms >= immr)
        {
            mnem = u ? "ubfx" : s ? "sbfx" : "bfxil";
            Imm(ops, $"#0x{immr:x}", immr);
            Imm(ops, $"#0x{imms - immr + 1:x}", imms - immr + 1);
        }
        else
        {
            mnem = u ? "ubfiz" : s ? "sbfiz" : "bfi";
            Imm(ops, $"#0x{width - immr:x}", width - immr);
            Imm(ops, $"#0x{imms + 1:x}", imms + 1);
        }
    }

    private static void FormatShiftedReg(ref string mnem, List<NativeOperand> ops, uint word, bool sf, int rd, int rn, int rm)
    {
        var shiftType = (int)((word >> 22) & 3);
        var amount = (int)((word >> 10) & 0x3F);

        // Aliases against the zero register.
        if (mnem == "orr" && rn == 31 && shiftType == 0 && amount == 0)
        {
            mnem = "mov";
            Reg(ops, R.Gpr(rd, sf));
            Reg(ops, R.Gpr(rm, sf));
            return;
        }

        if (mnem is "subs" or "adds" && rd == 31)
        {
            mnem = mnem == "subs" ? "cmp" : "cmn";
            Reg(ops, R.Gpr(rn, sf));
            Reg(ops, R.Gpr(rm, sf));
            AppendShift(ops, shiftType, amount);
            return;
        }

        if (mnem == "ands" && rd == 31)
        {
            mnem = "tst";
            Reg(ops, R.Gpr(rn, sf));
            Reg(ops, R.Gpr(rm, sf));
            AppendShift(ops, shiftType, amount);
            return;
        }

        if (mnem == "sub" && rn == 31 && shiftType == 0 && amount == 0)
        {
            mnem = "neg";
            Reg(ops, R.Gpr(rd, sf));
            Reg(ops, R.Gpr(rm, sf));
            return;
        }

        Reg(ops, R.Gpr(rd, sf));
        Reg(ops, R.Gpr(rn, sf));
        Reg(ops, R.Gpr(rm, sf));
        AppendShift(ops, shiftType, amount);
    }

    private static void FormatExtendedReg(ref string mnem, List<NativeOperand> ops, uint word, bool sf, int rd, int rn, int rm)
    {
        var option = (int)((word >> 13) & 7);
        var imm3 = (int)((word >> 10) & 7);

        if (mnem is "subs" or "adds" && rd == 31)
        {
            mnem = mnem == "subs" ? "cmp" : "cmn";
            Reg(ops, R.GprSp(rn, sf));
            Reg(ops, R.Gpr(rm, (option & 3) == 3));
        }
        else
        {
            Reg(ops, R.GprSp(rd, sf));
            Reg(ops, R.GprSp(rn, sf));
            Reg(ops, R.Gpr(rm, (option & 3) == 3));
        }

        var ext = R.Extend(option);
        Imm(ops, imm3 == 0 ? ext : $"{ext} #{imm3}", imm3);
    }

    private static void AppendShift(List<NativeOperand> ops, int shiftType, int amount)
    {
        if (amount != 0) Imm(ops, $"{R.Shift(shiftType)} #{amount}", amount);
    }

    private static void RewriteAddSubImm(ref string mnem, List<NativeOperand> ops, int rd, int rn, uint imm, bool shift)
    {
        if (mnem == "add" && imm == 0 && !shift && (rd == 31 || rn == 31)) { mnem = "mov"; ops.RemoveAt(2); }
        else if (mnem == "subs" && rd == 31) { mnem = "cmp"; ops.RemoveAt(0); }
        else if (mnem == "adds" && rd == 31) { mnem = "cmn"; ops.RemoveAt(0); }
    }

    private static void Reg(List<NativeOperand> ops, string name) =>
        ops.Add(new NativeOperand(NativeOperandKind.Register, name, Register: name));

    private static void Imm(List<NativeOperand> ops, string text, long value) =>
        ops.Add(new NativeOperand(NativeOperandKind.Immediate, text, Immediate: value));

    private static void Label(List<NativeOperand> ops, ulong target) =>
        ops.Add(new NativeOperand(NativeOperandKind.RelativeTarget, $"0x{target:x}"));

    private static long SignExtend(uint value, int bits)
    {
        var shift = 32 - bits;
        return (int)(value << shift) >> shift;
    }

    /// <summary>
    /// Decodes an A64 logical bitmask immediate (N:immr:imms) to its 32- or 64-bit value, per the
    /// architecture's DecodeBitMasks: a run of ones rotated within an element and replicated.
    /// </summary>
    private static ulong DecodeBitMask(bool sf, uint n, uint imms, uint immr)
    {
        var combined = (n << 6) | (~imms & 0x3F);

        // Highest set bit of the 7-bit combined value gives the element-size log2.
        var hsb = 6;
        while (hsb >= 0 && (combined & (1u << hsb)) == 0) hsb--;
        if (hsb < 1) return 0;

        var size = 1 << hsb;
        var levels = (uint)(size - 1);
        var s = imms & levels;
        var r = immr & levels;

        var welem = s + 1 >= 64 ? ~0UL : (1UL << (int)(s + 1)) - 1;
        var rotated = Ror(welem, (int)r, size);

        // Replicate the element to the datasize.
        var datasize = sf ? 64 : 32;
        ulong result = 0;
        for (var i = 0; i < datasize; i += size)
            result |= rotated << i;

        return sf ? result : result & 0xFFFFFFFF;
    }

    private static ulong Ror(ulong value, int amount, int size)
    {
        if (size >= 64) return amount == 0 ? value : value >> amount | value << (64 - amount);
        var mask = (1UL << size) - 1;
        value &= mask;
        amount %= size;
        return (value >> amount | value << (size - amount)) & mask;
    }
}
