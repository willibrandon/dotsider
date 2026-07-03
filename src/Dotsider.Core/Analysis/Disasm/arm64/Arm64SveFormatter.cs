using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis.Disasm.arm64;

using R = Arm64Registers;

/// <summary>
/// The SVE / SVE2 operand formatting for <see cref="Arm64OperandFormatter"/>: scalable Z registers
/// with their element-size suffix, governing predicates with the zeroing/merging qualifier, the
/// predicate-writing compares, the contiguous predicated addressing mode, and the element counters.
/// </summary>
internal static partial class Arm64OperandFormatter
{
    private static void FormatSve(Arm64Format format, string mnem, List<NativeOperand> ops, uint word, int zd, int zn, int zm)
    {
        var elt = SveElt((int)(word >> 22) & 3);
        var pg = (int)(word >> 10) & 7;

        switch (format)
        {
            case Arm64Format.SveArithUnpred:
                Z(ops, zd, elt); Z(ops, zn, elt); Z(ops, zm, elt);
                break;

            case Arm64Format.SveArithPred:
                Z(ops, zd, elt); Pred(ops, pg, "m"); Z(ops, zd, elt); Z(ops, zn, elt);
                break;

            case Arm64Format.SveUnaryPred:
                Z(ops, zd, elt); Pred(ops, pg, "m"); Z(ops, zn, elt);
                break;

            case Arm64Format.SvePtrue:
                P(ops, zd & 0xF, elt);
                Imm(ops, SvePattern((int)(word >> 5) & 0x1F), 0);
                break;

            case Arm64Format.SveWhile:
            {
                var is64 = ((word >> 12) & 1) == 1;
                P(ops, zd & 0xF, elt);
                Reg(ops, R.Gpr(zn, is64));
                Reg(ops, R.Gpr(zm, is64));
                break;
            }

            case Arm64Format.SveLoad:
            case Arm64Format.SveStore:
            {
                var scale = mnem[^1] switch { 'b' => 0, 'h' => 1, 'w' => 2, _ => 3 };
                var eltLs = mnem[^1] switch { 'b' => "b", 'h' => "h", 'w' => "s", _ => "d" };
                ZList(ops, zd, eltLs);
                Pred(ops, pg, format == Arm64Format.SveLoad ? "z" : null);
                var lsl = scale == 0 ? "" : $", lsl #{scale}";
                Mem(ops, $"[{R.GprSp(zn, true)}, {R.Gpr(zm, true)}{lsl}]", R.GprSp(zn, true), 0);
                break;
            }

            case Arm64Format.SveCmpVec:
                P(ops, zd & 0xF, elt); Pred(ops, pg, "z"); Z(ops, zn, elt); Z(ops, zm, elt);
                break;

            case Arm64Format.SveCmpImm:
                P(ops, zd & 0xF, elt); Pred(ops, pg, "z"); Z(ops, zn, elt);
                Imm(ops, $"#{(int)(word >> 16) & 0x1F}", (int)(word >> 16) & 0x1F);
                break;

            case Arm64Format.SveMovprfx:
                Z(ops, zd, null); Z(ops, zn, null);
                break;

            case Arm64Format.SveDupImm:
            {
                var imm = SignExtend((word >> 5) & 0xFF, 8);
                Z(ops, zd, elt);
                Imm(ops, $"#{imm}", imm);
                break;
            }

            case Arm64Format.SveInc:
                Reg(ops, R.Gpr(zd, true));
                break;
        }
    }

    private static void Z(List<NativeOperand> ops, int index, string? elt)
    {
        var name = elt is null ? $"z{index}" : $"z{index}.{elt}";
        ops.Add(new NativeOperand(NativeOperandKind.Register, name, Register: name));
    }

    private static void ZList(List<NativeOperand> ops, int index, string elt)
    {
        var name = $"{{z{index}.{elt}}}";
        ops.Add(new NativeOperand(NativeOperandKind.Register, name, Register: $"z{index}.{elt}"));
    }

    private static void P(List<NativeOperand> ops, int index, string elt)
    {
        var name = $"p{index}.{elt}";
        ops.Add(new NativeOperand(NativeOperandKind.Register, name, Register: name));
    }

    private static void Pred(List<NativeOperand> ops, int index, string? qualifier)
    {
        var name = qualifier is null ? $"p{index}" : $"p{index}/{qualifier}";
        ops.Add(new NativeOperand(NativeOperandKind.Register, name, Register: name));
    }

    private static string SveElt(int size) => size switch { 0 => "b", 1 => "h", 2 => "s", _ => "d" };

    private static string SvePattern(int pattern) => pattern switch
    {
        0 => "pow2",
        >= 1 and <= 8 => "vl" + pattern,
        0x1D => "mul4",
        0x1E => "mul3",
        0x1F => "all",
        _ => $"#0x{pattern:x}",
    };
}
