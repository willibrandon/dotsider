namespace Dotsider.Core.Analysis.Disasm.arm64;

using static Arm64Format;

/// <summary>
/// Registers a representative set of SVE / SVE2 opcodes (bits[28:25]=0010): unpredicated and
/// predicated integer/FP arithmetic over scalable Z registers, predicated unary ops, predicate
/// generation (<c>ptrue</c>/<c>while*</c>), contiguous predicated loads/stores, vector and
/// immediate compares that write a predicate, <c>movprfx</c>, the mod-immediate move, and the
/// element counters. The formatter derives the element-size suffix (<c>.b/.h/.s/.d</c>), the
/// governing predicate, and the zeroing/merging qualifier.
/// </summary>
internal static partial class Arm64Tables
{
    static partial void RegisterSve()
    {
        // Unpredicated arithmetic (Zd, Zn, Zm).
        Add(Sve, 0xFF20FC00, 0x04200000, "add", SveArithUnpred);
        Add(Sve, 0xFF20FC00, 0x04200400, "sub", SveArithUnpred);
        Add(Sve, 0xFF20FC00, 0x65000000, "fadd", SveArithUnpred);
        Add(Sve, 0xFF20FC00, 0x65000800, "fmul", SveArithUnpred);
        Add(Sve, 0xFF20FC00, 0x65000400, "fsub", SveArithUnpred);

        // Predicated destructive arithmetic (Zdn, Pg/m, Zdn, Zm).
        Add(Sve, 0xFF3FE000, 0x04000000, "add", SveArithPred);
        Add(Sve, 0xFF3FE000, 0x04010000, "sub", SveArithPred);
        Add(Sve, 0xFF3FE000, 0x04100000, "mul", SveArithPred);
        Add(Sve, 0xFF3FE000, 0x04080000, "smax", SveArithPred);
        Add(Sve, 0xFF3FE000, 0x040A0000, "smin", SveArithPred);
        Add(Sve, 0xFF3FE000, 0x65008000, "fadd", SveArithPred);
        Add(Sve, 0xFF3FE000, 0x65018000, "fsub", SveArithPred);
        Add(Sve, 0xFF3FE000, 0x65028000, "fmul", SveArithPred);

        // Predicated unary (Zd, Pg/m, Zn).
        Add(Sve, 0xFF3FE000, 0x6500A000, "frintn", SveUnaryPred);
        Add(Sve, 0xFF3FE000, 0x650DA000, "fsqrt", SveUnaryPred);
        Add(Sve, 0xFF3FE000, 0x0417A000, "abs", SveUnaryPred);
        Add(Sve, 0xFF3FE000, 0x0416A000, "neg", SveUnaryPred);

        // Predicate generation.
        Add(Sve, 0xFF3FFC10, 0x2518E000, "ptrue", SvePtrue);
        Add(Sve, 0xFF20FC10, 0x25201400, "whilelt", SveWhile);
        Add(Sve, 0xFF20FC10, 0x25200C00, "whilelo", SveWhile);

        // Contiguous predicated load/store (element size fixed by the dtype field).
        Add(Sve, 0xFFE0E000, 0xA5404000, "ld1w", SveLoad);
        Add(Sve, 0xFFE0E000, 0xA5E04000, "ld1d", SveLoad);
        Add(Sve, 0xFFE0E000, 0xE5404000, "st1w", SveStore);
        Add(Sve, 0xFFE0E000, 0xE5E04000, "st1d", SveStore);

        // Compares (write a predicate).
        Add(Sve, 0xFF20E010, 0x24000000, "cmphs", SveCmpVec);
        Add(Sve, 0xFF20E010, 0x24000010, "cmphi", SveCmpVec);
        Add(Sve, 0xFF20E010, 0x24100000, "cmpeq", SveCmpVec);
        Add(Sve, 0xFF20E010, 0x24100010, "cmpne", SveCmpVec);
        Add(Sve, 0xFF20E000, 0x25000000, "cmpge", SveCmpImm);

        // Move-prefix, mod-immediate move, and element counters.
        Add(Sve, 0xFFE0FC00, 0x0420BC00, "movprfx", SveMovprfx);
        Add(Sve, 0xFF3FC000, 0x2538C000, "mov", SveDupImm);
        Add(Sve, 0xFFF0FC00, 0x0430E000, "incb", SveInc);
        Add(Sve, 0xFFF0FC00, 0x0470E000, "inch", SveInc);
        Add(Sve, 0xFFF0FC00, 0x04B0E000, "incw", SveInc);
        Add(Sve, 0xFFF0FC00, 0x04F0E000, "incd", SveInc);
    }
}
