using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the A64 scalar-FP and Advanced-SIMD decode: scalar FP arithmetic/compare/convert, the
/// vector three-same and misc classes with their Vd.T arrangements, dup, dot-product, the AES
/// crypto rounds, and CRC32 — cross-checked against Capstone.
/// </summary>
public class Arm64SimdTests
{
    private static NativeInstruction One(uint word)
    {
        var code = BitConverter.GetBytes(word);
        return NativeDisassembler.Disassemble(code, 0x1000, NativeArchitecture.Arm64)[0];
    }

    /// <summary>Decodes representative scalar-FP, SIMD, crypto, and CRC instructions.</summary>
    [Theory(Timeout = 30_000)]
    [InlineData("fadd", "s0, s1, s2", 0x1E222820u)]
    [InlineData("fmul", "d0, d1, d2", 0x1E620820u)]
    [InlineData("fcmp", "s0, s1", 0x1E212000u)]
    [InlineData("fmov", "s0, s1", 0x1E204020u)]
    [InlineData("scvtf", "s0, w0", 0x1E220000u)]
    [InlineData("fcvtzs", "w0, s0", 0x1E380000u)]
    [InlineData("add", "v0.4s, v1.4s, v2.4s", 0x4EA28420u)]
    [InlineData("fmul", "v0.4s, v1.4s, v2.4s", 0x6E22DC20u)]
    [InlineData("fadd", "v0.4s, v1.4s, v2.4s", 0x4E22D420u)]
    [InlineData("and", "v0.16b, v1.16b, v2.16b", 0x4E221C20u)]
    [InlineData("neg", "v0.4s, v1.4s", 0x6EA0B820u)]
    [InlineData("dup", "v0.4s, w1", 0x4E040C20u)]
    [InlineData("mov", "v16.s[0], w0", 0x4E041C10u)]
    [InlineData("smov", "x0, v16.s[0]", 0x4E042E00u)]
    [InlineData("umov", "w0, v16.b[0]", 0x0E013E00u)]
    [InlineData("aese", "v0.16b, v1.16b", 0x4E284820u)]
    [InlineData("crc32b", "w0, w1, w2", 0x1AC24020u)]
    [InlineData("crc32x", "w0, w1, x2", 0x9AC24C20u)]
    [InlineData("sdot", "v0.4s, v1.16b, v2.16b", 0x4E829420u)]
    public void Decode_SimdFp_MnemonicAndOperands(string mnemonic, string operands, uint word)
    {
        var insn = One(word);
        Assert.False(insn.IsFallback);
        Assert.Equal(mnemonic, insn.Mnemonic);
        Assert.Equal(operands, insn.OperandText);
        Assert.Equal(4, insn.Length);
    }
}
