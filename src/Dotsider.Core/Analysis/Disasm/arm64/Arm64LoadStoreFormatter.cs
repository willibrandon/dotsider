using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis.Disasm.arm64;

using R = Arm64Registers;

/// <summary>
/// The load/store operand formatting for <see cref="Arm64OperandFormatter"/>: derives the mnemonic
/// and transfer-register width from the size/V/opc fields, and renders the addressing mode — an
/// unsigned scaled offset, an unscaled signed offset, pre/post-index (<c>[Rn], #imm</c> /
/// <c>[Rn, #imm]!</c>), a register offset, a pair, a PC-relative literal, exclusive/acquire-release,
/// or an LSE atomic.
/// </summary>
internal static partial class Arm64OperandFormatter
{
    private static string FormatLoadStore(
        Arm64Format format, List<NativeOperand> ops, uint word, int rt, int rn, int rm, int rt2,
        ulong address, ref ulong? target)
    {
        var size = (int)(word >> 30) & 3;
        var v = (int)(word >> 26) & 1;
        var opc = (int)(word >> 22) & 3;

        return format switch
        {
            Arm64Format.LdStUImm => FormatUImm(ops, word, size, v, opc, rt, rn),
            Arm64Format.LdStUnscaled => FormatUnscaled(ops, word, size, v, opc, rt, rn),
            Arm64Format.LdStImmIndexed => FormatIndexed(ops, word, size, v, opc, rt, rn),
            Arm64Format.LdStRegOff => FormatRegOff(ops, word, size, v, opc, rt, rn, rm),
            Arm64Format.LdStPair => FormatPair(ops, word, rt, rn, rt2),
            Arm64Format.LdLiteral => FormatLiteral(ops, word, rt, address, ref target),
            Arm64Format.LdStExclusive => FormatExclusive(ops, word, size, rt, rn, rm, rt2),
            Arm64Format.Atomic => FormatAtomic(ops, word, size, rt, rn, rm),
            _ => ".word",
        };
    }

    /// <summary>Derives the (mnemonic, transfer-register name) for a general load/store from size/V/opc.</summary>
    private static (string Mnemonic, string Reg) TransferReg(int size, int v, int opc, int rt)
    {
        if (v == 1)
        {
            // SIMD/FP: opc<0> is load; size with opc<1> gives b/h/s/d/q.
            var load = (opc & 1) == 1;
            var fpSize = size == 0 && (opc & 2) != 0 ? 4 : size;
            return (load ? "ldr" : "str", R.Fp(rt, fpSize));
        }

        return size switch
        {
            0 => opc switch
            {
                0 => ("strb", R.Gpr(rt, false)),
                1 => ("ldrb", R.Gpr(rt, false)),
                2 => ("ldrsb", R.Gpr(rt, true)),
                _ => ("ldrsb", R.Gpr(rt, false)),
            },
            1 => opc switch
            {
                0 => ("strh", R.Gpr(rt, false)),
                1 => ("ldrh", R.Gpr(rt, false)),
                2 => ("ldrsh", R.Gpr(rt, true)),
                _ => ("ldrsh", R.Gpr(rt, false)),
            },
            2 => opc switch
            {
                0 => ("str", R.Gpr(rt, false)),
                1 => ("ldr", R.Gpr(rt, false)),
                _ => ("ldrsw", R.Gpr(rt, true)),
            },
            _ => opc switch
            {
                0 => ("str", R.Gpr(rt, true)),
                1 => ("ldr", R.Gpr(rt, true)),
                _ => ("prfm", "?"),
            },
        };
    }

    private static int AccessScale(int size, int v, int opc) =>
        v == 1 && size == 0 && (opc & 2) != 0 ? 4 : size;

    private static string FormatUImm(List<NativeOperand> ops, uint word, int size, int v, int opc, int rt, int rn)
    {
        var (mnem, reg) = TransferReg(size, v, opc, rt);
        var imm = (long)((word >> 10) & 0xFFF) << AccessScale(size, v, opc);
        Reg(ops, reg);
        Mem(ops, imm == 0 ? $"[{R.GprSp(rn, true)}]" : $"[{R.GprSp(rn, true)}, #0x{imm:x}]", R.GprSp(rn, true), imm);
        return mnem;
    }

    private static string FormatUnscaled(List<NativeOperand> ops, uint word, int size, int v, int opc, int rt, int rn)
    {
        var (mnem, reg) = TransferReg(size, v, opc, rt);
        var imm = SignExtend((word >> 12) & 0x1FF, 9);
        Reg(ops, reg);
        Mem(ops, imm == 0 ? $"[{R.GprSp(rn, true)}]" : $"[{R.GprSp(rn, true)}, #{Hex(imm)}]", R.GprSp(rn, true), imm);
        return mnem[..2] + "u" + mnem[2..]; // ldr→ldur, strb→sturb, ldrsw→ldursw
    }

    private static string FormatIndexed(List<NativeOperand> ops, uint word, int size, int v, int opc, int rt, int rn)
    {
        var (mnem, reg) = TransferReg(size, v, opc, rt);
        var imm = SignExtend((word >> 12) & 0x1FF, 9);
        var pre = ((word >> 10) & 3) == 3;
        var baseName = R.GprSp(rn, true);
        Reg(ops, reg);
        Mem(ops, pre ? $"[{baseName}, #{Hex(imm)}]!" : $"[{baseName}], #{Hex(imm)}", baseName, imm);
        return mnem;
    }

    private static string FormatRegOff(List<NativeOperand> ops, uint word, int size, int v, int opc, int rt, int rn, int rm)
    {
        var (mnem, reg) = TransferReg(size, v, opc, rt);
        var option = (int)(word >> 13) & 7;
        var s = ((word >> 12) & 1) == 1;
        var amount = s ? AccessScale(size, v, opc) : 0;
        var indexReg = R.Gpr(rm, (option & 3) == 3);
        var ext = R.Extend(option);
        var extText = option == 3 && !s ? "" : $", {(option == 3 ? "lsl" : ext)}{(amount != 0 ? $" #{amount}" : "")}";
        Reg(ops, reg);
        Mem(ops, $"[{R.GprSp(rn, true)}, {indexReg}{extText}]", R.GprSp(rn, true), 0);
        return mnem;
    }

    private static string FormatPair(List<NativeOperand> ops, uint word, int rt, int rn, int rt2)
    {
        var opc = (int)(word >> 30) & 3;      // 00=32-bit, 10=64-bit, 01=32-bit signed (ldpsw)
        var v = (int)(word >> 26) & 1;
        var load = ((word >> 22) & 1) == 1;
        var kind = (word >> 23) & 3;          // 01=post, 10=offset(signed), 11=pre
        var is64 = opc == 2;
        var scale = v == 1 ? 2 + opc : is64 ? 3 : 2;
        var imm = SignExtend((word >> 15) & 0x7F, 7) << scale;

        var rtReg = v == 1 ? R.Fp(rt, scale) : R.Gpr(rt, is64);
        var rt2Reg = v == 1 ? R.Fp(rt2, scale) : R.Gpr(rt2, is64);
        var mnem = v == 0 && opc == 1 ? (load ? "ldpsw" : "stp") : load ? "ldp" : "stp";
        var baseName = R.GprSp(rn, true);

        Reg(ops, rtReg);
        Reg(ops, rt2Reg);
        var mem = kind switch
        {
            1 => $"[{baseName}], #{Hex(imm)}",              // post-index
            3 => $"[{baseName}, #{Hex(imm)}]!",             // pre-index
            _ => imm == 0 ? $"[{baseName}]" : $"[{baseName}, #{Hex(imm)}]",
        };
        Mem(ops, mem, baseName, imm);
        return mnem;
    }

    private static string FormatLiteral(List<NativeOperand> ops, uint word, int rt, ulong address, ref ulong? target)
    {
        var opc = (int)(word >> 30) & 3;
        var off = SignExtend((word >> 5) & 0x7FFFF, 19) << 2;
        target = unchecked(address + (ulong)off);
        var reg = opc switch { 0 => R.Gpr(rt, false), 1 => R.Gpr(rt, true), 2 => R.Gpr(rt, true), _ => R.Fp(rt, 2) };
        Reg(ops, reg);
        Label(ops, target.Value);
        return "ldr";
    }

    private static string FormatExclusive(List<NativeOperand> ops, uint word, int size, int rt, int rn, int rs, int rt2)
    {
        var load = ((word >> 22) & 1) == 1;
        var o0 = ((word >> 15) & 1) == 1;      // acquire/release ordering
        var pair = ((word >> 21) & 1) == 1;
        var is64 = size == 3;
        var baseName = R.GprSp(rn, true);

        // ldar/stlr have no status register (bit23=1 via the o2 form); the exclusive forms do.
        var lo3 = size switch { 0 => "b", 1 => "h", _ => "" };
        if (((word >> 23) & 1) == 1)
        {
            var m = load ? "ldar" : "stlr";
            Reg(ops, R.Gpr(rt, is64));
            Mem(ops, $"[{baseName}]", baseName, 0);
            return m + lo3;
        }

        var mnem = (load ? "ld" : "st") + (o0 ? "a" : "") + "xr" + lo3;
        if (!load) Reg(ops, R.Gpr(rs, false));
        Reg(ops, R.Gpr(rt, is64));
        if (pair) Reg(ops, R.Gpr(rt2, is64));
        Mem(ops, $"[{baseName}]", baseName, 0);
        return mnem;
    }

    private static string FormatAtomic(List<NativeOperand> ops, uint word, int size, int rt, int rn, int rs)
    {
        var op = (int)(word >> 12) & 7;
        var is64 = size == 3;
        var a = ((word >> 23) & 1) == 1;
        var rl = ((word >> 22) & 1) == 1;
        var order = (a ? "a" : "") + (rl ? "l" : "");
        var baseName = R.GprSp(rn, true);
        var name = op switch
        {
            0 => "ldadd", 1 => "ldclr", 2 => "ldeor", 3 => "ldset",
            4 => "ldsmax", 5 => "ldsmin", 6 => "ldumax", 7 => "ldumin", _ => "ldadd",
        };
        var suffix = size switch { 0 => "b", 1 => "h", _ => "" };

        Reg(ops, R.Gpr(rs, is64));
        Reg(ops, R.Gpr(rt, is64));
        Mem(ops, $"[{baseName}]", baseName, 0);
        return name + order + suffix;
    }

    private static void Mem(List<NativeOperand> ops, string text, string baseReg, long disp) =>
        ops.Add(new NativeOperand(NativeOperandKind.Memory, text, MemoryBase: baseReg, MemoryDisplacement: disp));

    private static string Hex(long value) => value < 0 ? $"-0x{-value:x}" : $"0x{value:x}";
}
