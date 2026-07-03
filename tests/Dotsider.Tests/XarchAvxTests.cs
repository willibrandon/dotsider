using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the x86-64 AVX / AVX2 (VEX) decode: the shared SSE opcodes gain their vvvv source and
/// v-prefix, ymm registers appear on VEX.256, and the VEX-only ops (broadcasts, permutes,
/// insert/extract-128, variable shifts, F16C) decode — cross-checked against objdump.
/// </summary>
public class XarchAvxTests
{
    private static NativeInstruction One(params byte[] code) =>
        NativeDisassembler.Disassemble(code, 0x1000, NativeArchitecture.X64)[0];

    /// <summary>Decodes representative VEX-encoded AVX/AVX2 ops to their mnemonics, operands, and length.</summary>
    [Theory(Timeout = 30_000)]
    [InlineData("vaddps", "ymm0, ymm1, ymm2", new byte[] { 0xC5, 0xF4, 0x58, 0xC2 })]
    [InlineData("vaddsd", "xmm0, xmm1, xmm2", new byte[] { 0xC5, 0xF3, 0x58, 0xC2 })]
    [InlineData("vpxor", "xmm0, xmm1, xmm2", new byte[] { 0xC5, 0xF1, 0xEF, 0xC2 })]
    [InlineData("vmovaps", "ymm0, ymm1", new byte[] { 0xC5, 0xFC, 0x28, 0xC1 })]
    [InlineData("vmovdqa", "ymm0, ymmword ptr [rcx]", new byte[] { 0xC5, 0xFD, 0x6F, 0x01 })]
    [InlineData("vbroadcastss", "ymm0, xmm1", new byte[] { 0xC4, 0xE2, 0x7D, 0x18, 0xC1 })]
    [InlineData("vinsertf128", "ymm0, ymm0, xmm2, 0x1", new byte[] { 0xC4, 0xE3, 0x7D, 0x18, 0xC2, 0x01 })]
    [InlineData("vzeroupper", "", new byte[] { 0xC5, 0xF8, 0x77 })]
    [InlineData("vzeroall", "", new byte[] { 0xC5, 0xFC, 0x77 })]
    [InlineData("vpmulld", "ymm0, ymm1, ymm2", new byte[] { 0xC4, 0xE2, 0x75, 0x40, 0xC2 })]
    [InlineData("vpsllvd", "xmm0, xmm1, xmm2", new byte[] { 0xC4, 0xE2, 0x71, 0x47, 0xC2 })]
    [InlineData("vcvtph2ps", "ymm0, xmm1", new byte[] { 0xC4, 0xE2, 0x7D, 0x13, 0xC1 })]
    public void Decode_Avx_MnemonicAndOperands(string mnemonic, string operands, byte[] code)
    {
        var insn = One(code);
        Assert.False(insn.IsFallback);
        Assert.Equal(mnemonic, insn.Mnemonic);
        Assert.Equal(operands, insn.OperandText);
        Assert.Equal(code.Length, insn.Length);
    }

    /// <summary>Verifies a VEX.256 op renders ymm registers and classifies as Vector.</summary>
    [Fact(Timeout = 30_000)]
    public void Decode_Avx256_UsesYmmAndVectorCategory()
    {
        var insn = One(0xC5, 0xF4, 0x58, 0xC2); // vaddps ymm0, ymm1, ymm2
        Assert.Contains("ymm", insn.OperandText);
        Assert.Equal(NativeInstructionCategory.Vector, insn.Category);
    }
}
