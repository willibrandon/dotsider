using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the x86-64 AES-NI, PCLMULQDQ, and GFNI decode, including the VEX-wide VAES /
/// VPCLMULQDQ forms that gain their vvvv source — cross-checked against objdump (whose fused
/// pclmul immediate names normalize to the generic form).
/// </summary>
public class XarchCryptoTests
{
    private static NativeInstruction One(params byte[] code) =>
        NativeDisassembler.Disassemble(code, 0x1000, NativeArchitecture.X64)[0];

    /// <summary>Decodes the crypto/GF opcodes to their mnemonics, operands, and length.</summary>
    [Theory(Timeout = 30_000)]
    [InlineData("aesenc", "xmm0, xmm1", new byte[] { 0x66, 0x0F, 0x38, 0xDC, 0xC1 })]
    [InlineData("aesenclast", "xmm0, xmm1", new byte[] { 0x66, 0x0F, 0x38, 0xDD, 0xC1 })]
    [InlineData("aesdec", "xmm0, xmm1", new byte[] { 0x66, 0x0F, 0x38, 0xDE, 0xC1 })]
    [InlineData("aesimc", "xmm0, xmm1", new byte[] { 0x66, 0x0F, 0x38, 0xDB, 0xC1 })]
    [InlineData("aeskeygenassist", "xmm0, xmm1, 0x1", new byte[] { 0x66, 0x0F, 0x3A, 0xDF, 0xC1, 0x01 })]
    [InlineData("pclmulqdq", "xmm0, xmm1, 0x11", new byte[] { 0x66, 0x0F, 0x3A, 0x44, 0xC1, 0x11 })]
    [InlineData("vaesenc", "xmm0, xmm1, xmm2", new byte[] { 0xC4, 0xE2, 0x71, 0xDC, 0xC2 })]
    [InlineData("vpclmulqdq", "xmm0, xmm1, xmm2, 0x10", new byte[] { 0xC4, 0xE3, 0x71, 0x44, 0xC2, 0x10 })]
    [InlineData("gf2p8mulb", "xmm0, xmm1", new byte[] { 0x66, 0x0F, 0x38, 0xCF, 0xC1 })]
    [InlineData("gf2p8affineqb", "xmm0, xmm1, 0x5", new byte[] { 0x66, 0x0F, 0x3A, 0xCE, 0xC1, 0x05 })]
    public void Decode_Crypto_MnemonicAndOperands(string mnemonic, string operands, byte[] code)
    {
        var insn = One(code);
        Assert.False(insn.IsFallback);
        Assert.Equal(mnemonic, insn.Mnemonic);
        Assert.Equal(operands, insn.OperandText);
        Assert.Equal(code.Length, insn.Length);
    }
}
