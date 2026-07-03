using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis.Disasm.arm64;

using R = Arm64Registers;

/// <summary>
/// The scalar-FP and Advanced-SIMD operand formatting for <see cref="Arm64OperandFormatter"/>:
/// scalar FP registers by precision type, vector <c>Vd.T</c> arrangements derived from the size/Q
/// fields, the integer/FP conversions with their direction, dup/mod-immediate, dot-product, and the
/// AES/SHA crypto forms.
/// </summary>
internal static partial class Arm64OperandFormatter
{
    private static void FormatSimd(Arm64Format format, string mnem, List<NativeOperand> ops, uint word, int rd, int rn, int rm)
    {
        var type = (int)(word >> 22) & 3;         // scalar FP ptype
        var q = ((word >> 30) & 1) == 1;
        var fp = mnem[0] == 'f';

        switch (format)
        {
            case Arm64Format.ScalarFp3:
            {
                var sc = FpSize(type);
                Reg(ops, R.Fp(rd, sc)); Reg(ops, R.Fp(rn, sc)); Reg(ops, R.Fp(rm, sc));
                break;
            }

            case Arm64Format.ScalarFp2:
            {
                var sc = FpSize(type);
                Reg(ops, R.Fp(rd, sc)); Reg(ops, R.Fp(rn, sc));
                break;
            }

            case Arm64Format.FpCompare:
            {
                var sc = FpSize(type);
                Reg(ops, R.Fp(rn, sc));
                if (((word >> 3) & 1) == 1) Imm(ops, "#0.0", 0);
                else Reg(ops, R.Fp(rm, sc));
                break;
            }

            case Arm64Format.FpCvt:
            {
                var dst = FpSize((int)(word >> 15) & 3);
                Reg(ops, R.Fp(rd, dst)); Reg(ops, R.Fp(rn, FpSize(type)));
                break;
            }

            case Arm64Format.FpToFromInt:
            {
                var sf = ((word >> 31) & 1) == 1;
                var sc = FpSize(type);
                if (mnem is "scvtf" or "ucvtf") { Reg(ops, R.Fp(rd, sc)); Reg(ops, R.Gpr(rn, sf)); }
                else if (mnem is "fcvtzs" or "fcvtzu") { Reg(ops, R.Gpr(rd, sf)); Reg(ops, R.Fp(rn, sc)); }
                else if (((word >> 16) & 7) == 6) { Reg(ops, R.Gpr(rd, sf)); Reg(ops, R.Fp(rn, sc)); } // fmov to GPR
                else { Reg(ops, R.Fp(rd, sc)); Reg(ops, R.Gpr(rn, sf)); }                              // fmov from GPR
                break;
            }

            case Arm64Format.FpCondSelect:
            {
                var sc = FpSize(type);
                Reg(ops, R.Fp(rd, sc)); Reg(ops, R.Fp(rn, sc)); Reg(ops, R.Fp(rm, sc));
                Imm(ops, R.Condition((int)((word >> 12) & 0xF)), 0);
                break;
            }

            case Arm64Format.SimdReg3:
            {
                var arr = fp ? FpArrangement(word, q) : Arrangement((int)(word >> 22) & 3, q);
                Vec(ops, rd, arr); Vec(ops, rn, arr); Vec(ops, rm, arr);
                break;
            }

            case Arm64Format.SimdMisc2:
            {
                var arr = fp ? FpArrangement(word, q) : Arrangement((int)(word >> 22) & 3, q);
                Vec(ops, rd, arr); Vec(ops, rn, arr);
                break;
            }

            case Arm64Format.SimdDup:
            {
                var imm5 = (int)(word >> 16) & 0x1F;
                var (arr, is64) = DupArrangement(imm5, q);
                Vec(ops, rd, arr); Reg(ops, R.Gpr(rn, is64));
                break;
            }

            case Arm64Format.SimdModImm:
            {
                var imm = (word >> 5) & 0x1F | ((word >> 16) & 7) << 5;
                Vec(ops, rd, q ? "4s" : "2s");
                Imm(ops, $"#0x{imm:x}", (long)imm);
                break;
            }

            case Arm64Format.SimdDot:
            {
                Vec(ops, rd, q ? "4s" : "2s");
                Vec(ops, rn, q ? "16b" : "8b");
                Vec(ops, rm, q ? "16b" : "8b");
                break;
            }

            case Arm64Format.CryptoAes:
                Vec(ops, rd, "16b"); Vec(ops, rn, "16b");
                break;

            case Arm64Format.CryptoSha:
                if (mnem == "sha256su0") { Vec(ops, rd, "4s"); Vec(ops, rn, "4s"); }
                else { Reg(ops, R.Fp(rd, 4)); Reg(ops, R.Fp(rn, 4)); Vec(ops, rm, "4s"); }
                break;
        }
    }

    private static void Vec(List<NativeOperand> ops, int index, string arrangement)
    {
        var name = $"v{index}.{arrangement}";
        ops.Add(new NativeOperand(NativeOperandKind.Register, name, Register: name));
    }

    private static int FpSize(int ptype) => ptype switch { 0 => 2, 1 => 3, _ => 1 }; // S, D, H

    private static string Arrangement(int size, bool q) => size switch
    {
        0 => q ? "16b" : "8b",
        1 => q ? "8h" : "4h",
        2 => q ? "4s" : "2s",
        _ => q ? "2d" : "1d",
    };

    private static string FpArrangement(uint word, bool q)
    {
        var sz = ((word >> 22) & 1) == 1;
        return sz ? q ? "2d" : "1d" : q ? "4s" : "2s";
    }

    private static (string Arrangement, bool Is64) DupArrangement(int imm5, bool q)
    {
        if ((imm5 & 1) != 0) return (q ? "16b" : "8b", false);
        if ((imm5 & 2) != 0) return (q ? "8h" : "4h", false);
        if ((imm5 & 4) != 0) return (q ? "4s" : "2s", false);
        return (q ? "2d" : "1d", true);
    }
}
