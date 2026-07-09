using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the A64 SVE / SVE2 decode: unpredicated and predicated arithmetic over scalable Z
/// registers, predicate generation (ptrue/whilelt), contiguous predicated loads/stores, the
/// predicate-writing compares, movprfx, and the mod-immediate move — cross-checked against Capstone.
/// </summary>
[TestClass]
public class Arm64SveTests
{
    private static NativeInstruction One(uint word)
    {
        var code = BitConverter.GetBytes(word);
        return NativeDisassembler.Disassemble(code, 0x1000, NativeArchitecture.Arm64)[0];
    }

    /// <summary>Decodes representative SVE instructions with Z/predicate registers and element suffixes.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow("add", "z0.s, z0.s, z0.s", 0x04A00000u)]
    [DataRow("sub", "z0.s, z0.s, z0.s", 0x04A00400u)]
    [DataRow("add", "z0.b, p0/m, z0.b, z0.b", 0x04000000u)]
    [DataRow("mul", "z0.b, p0/m, z0.b, z0.b", 0x04100000u)]
    [DataRow("fadd", "z0.s, p0/m, z0.s, z0.s", 0x65808000u)]
    [DataRow("frintn", "z0.s, p0/m, z0.s", 0x6580A000u)]
    [DataRow("ptrue", "p0.s, pow2", 0x2598E000u)]
    [DataRow("whilelt", "p0.b, x0, x0", 0x25201400u)]
    [DataRow("ld1w", "{z0.s}, p0/z, [x0, x0, lsl #2]", 0xA5404000u)]
    [DataRow("st1w", "{z0.s}, p0, [x0, x0, lsl #2]", 0xE5404000u)]
    [DataRow("ld1d", "{z0.d}, p0/z, [x0, x0, lsl #3]", 0xA5E04000u)]
    [DataRow("cmphs", "p0.d, p0/z, z0.d, z0.d", 0x24C00000u)]
    [DataRow("cmpge", "p0.b, p4/z, z0.b, #1", 0x25011000u)]
    [DataRow("movprfx", "z0, z0", 0x0420BC00u)]
    [DataRow("mov", "z0.b, #1", 0x2538C020u)]
    public void Decode_Sve_MnemonicAndOperands(string mnemonic, string operands, uint word)
    {
        var insn = One(word);
        Assert.IsFalse(insn.IsFallback);
        Assert.AreEqual(mnemonic, insn.Mnemonic);
        Assert.AreEqual(operands, insn.OperandText);
        Assert.AreEqual(4, insn.Length);
    }

    /// <summary>Verifies SVE instructions classify as Vector.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Decode_Sve_ClassifiesVector()
    {
        Assert.AreEqual(NativeInstructionCategory.Vector, One(0x04A00000u).Category);
    }
}
