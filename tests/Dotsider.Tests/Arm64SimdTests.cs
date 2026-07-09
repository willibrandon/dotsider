using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the A64 scalar-FP and Advanced-SIMD decode: scalar FP arithmetic/compare/convert, the
/// vector three-same and misc classes with their Vd.T arrangements, dup, dot-product, the AES
/// crypto rounds, and CRC32 — cross-checked against Capstone.
/// </summary>
[TestClass]
public class Arm64SimdTests
{
    private static NativeInstruction One(uint word)
    {
        var code = BitConverter.GetBytes(word);
        return NativeDisassembler.Disassemble(code, 0x1000, NativeArchitecture.Arm64)[0];
    }

    /// <summary>Decodes representative scalar-FP, SIMD, crypto, and CRC instructions.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow("fadd", "s0, s1, s2", 0x1E222820u)]
    [DataRow("fmul", "d0, d1, d2", 0x1E620820u)]
    [DataRow("fcmp", "s0, s1", 0x1E212000u)]
    [DataRow("fmov", "s0, s1", 0x1E204020u)]
    [DataRow("scvtf", "s0, w0", 0x1E220000u)]
    [DataRow("fcvtzs", "w0, s0", 0x1E380000u)]
    [DataRow("add", "v0.4s, v1.4s, v2.4s", 0x4EA28420u)]
    [DataRow("fmul", "v0.4s, v1.4s, v2.4s", 0x6E22DC20u)]
    [DataRow("fadd", "v0.4s, v1.4s, v2.4s", 0x4E22D420u)]
    [DataRow("and", "v0.16b, v1.16b, v2.16b", 0x4E221C20u)]
    [DataRow("neg", "v0.4s, v1.4s", 0x6EA0B820u)]
    [DataRow("dup", "v0.4s, w1", 0x4E040C20u)]
    [DataRow("mov", "v16.s[0], w0", 0x4E041C10u)]
    [DataRow("smov", "x0, v16.s[0]", 0x4E042E00u)]
    [DataRow("umov", "w0, v16.b[0]", 0x0E013E00u)]
    [DataRow("aese", "v0.16b, v1.16b", 0x4E284820u)]
    [DataRow("crc32b", "w0, w1, w2", 0x1AC24020u)]
    [DataRow("crc32x", "w0, w1, x2", 0x9AC24C20u)]
    [DataRow("sdot", "v0.4s, v1.16b, v2.16b", 0x4E829420u)]
    public void Decode_SimdFp_MnemonicAndOperands(string mnemonic, string operands, uint word)
    {
        var insn = One(word);
        Assert.IsFalse(insn.IsFallback);
        Assert.AreEqual(mnemonic, insn.Mnemonic);
        Assert.AreEqual(operands, insn.OperandText);
        Assert.AreEqual(4, insn.Length);
    }
}
