using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the A64 SVE / SVE2 decode: unpredicated and predicated arithmetic over scalable Z
/// registers, predicate generation (ptrue/whilelt), contiguous predicated loads/stores, the
/// predicate-writing compares, movprfx, and the mod-immediate move — cross-checked against Capstone.
/// </summary>
public class Arm64SveTests
{
    private static NativeInstruction One(uint word)
    {
        var code = BitConverter.GetBytes(word);
        return NativeDisassembler.Disassemble(code, 0x1000, NativeArchitecture.Arm64)[0];
    }

    /// <summary>Decodes representative SVE instructions with Z/predicate registers and element suffixes.</summary>
    [Theory(Timeout = 30_000)]
    [InlineData("add", "z0.s, z0.s, z0.s", 0x04A00000u)]
    [InlineData("sub", "z0.s, z0.s, z0.s", 0x04A00400u)]
    [InlineData("add", "z0.b, p0/m, z0.b, z0.b", 0x04000000u)]
    [InlineData("mul", "z0.b, p0/m, z0.b, z0.b", 0x04100000u)]
    [InlineData("fadd", "z0.s, p0/m, z0.s, z0.s", 0x65808000u)]
    [InlineData("frintn", "z0.s, p0/m, z0.s", 0x6580A000u)]
    [InlineData("ptrue", "p0.s, pow2", 0x2598E000u)]
    [InlineData("whilelt", "p0.b, x0, x0", 0x25201400u)]
    [InlineData("ld1w", "{z0.s}, p0/z, [x0, x0, lsl #2]", 0xA5404000u)]
    [InlineData("st1w", "{z0.s}, p0, [x0, x0, lsl #2]", 0xE5404000u)]
    [InlineData("ld1d", "{z0.d}, p0/z, [x0, x0, lsl #3]", 0xA5E04000u)]
    [InlineData("cmphs", "p0.d, p0/z, z0.d, z0.d", 0x24C00000u)]
    [InlineData("cmpge", "p0.b, p4/z, z0.b, #1", 0x25011000u)]
    [InlineData("movprfx", "z0, z0", 0x0420BC00u)]
    [InlineData("mov", "z0.b, #1", 0x2538C020u)]
    public void Decode_Sve_MnemonicAndOperands(string mnemonic, string operands, uint word)
    {
        var insn = One(word);
        Assert.False(insn.IsFallback);
        Assert.Equal(mnemonic, insn.Mnemonic);
        Assert.Equal(operands, insn.OperandText);
        Assert.Equal(4, insn.Length);
    }

    /// <summary>Verifies SVE instructions classify as Vector.</summary>
    [Fact(Timeout = 30_000)]
    public void Decode_Sve_ClassifiesVector()
    {
        Assert.Equal(NativeInstructionCategory.Vector, One(0x04A00000u).Category);
    }
}
