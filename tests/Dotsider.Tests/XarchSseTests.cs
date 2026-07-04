using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the x86-64 SSE–SSE4.2 decode: packed and scalar arithmetic, moves with the correct
/// memory size hints (scalar dword/qword vs packed xmmword), conversions, packed-integer ops,
/// the 0F 38 / 0F 3A maps, and the shift-by-immediate groups — cross-checked against objdump.
/// </summary>
public class XarchSseTests
{
    private static NativeInstruction One(params byte[] code) =>
        NativeDisassembler.Disassemble(code, 0x1000, NativeArchitecture.X64)[0];

    /// <summary>Decodes representative SSE opcodes to their exact mnemonics, operands, and length.</summary>
    [Theory(Timeout = 30_000)]
    [InlineData("movaps", "xmm0, xmm1", new byte[] { 0x0F, 0x28, 0xC1 })]
    [InlineData("movapd", "xmm0, xmm1", new byte[] { 0x66, 0x0F, 0x28, 0xC1 })]
    [InlineData("movss", "xmm0, dword ptr [rax]", new byte[] { 0xF3, 0x0F, 0x10, 0x00 })]
    [InlineData("movsd", "xmm0, qword ptr [rax]", new byte[] { 0xF2, 0x0F, 0x10, 0x00 })]
    [InlineData("addps", "xmm0, xmm1", new byte[] { 0x0F, 0x58, 0xC1 })]
    [InlineData("addsd", "xmm0, xmm1", new byte[] { 0xF2, 0x0F, 0x58, 0xC1 })]
    [InlineData("mulss", "xmm0, xmm1", new byte[] { 0xF3, 0x0F, 0x59, 0xC1 })]
    [InlineData("pxor", "xmm0, xmm1", new byte[] { 0x66, 0x0F, 0xEF, 0xC1 })]
    [InlineData("paddd", "xmm0, xmm1", new byte[] { 0x66, 0x0F, 0xFE, 0xC1 })]
    [InlineData("movdqu", "xmm0, xmmword ptr [rax]", new byte[] { 0xF3, 0x0F, 0x6F, 0x00 })]
    [InlineData("movd", "xmm0, eax", new byte[] { 0x66, 0x0F, 0x6E, 0xC0 })]
    [InlineData("movq", "xmm0, rax", new byte[] { 0x66, 0x48, 0x0F, 0x6E, 0xC0 })]
    [InlineData("cvtsi2sd", "xmm0, eax", new byte[] { 0xF2, 0x0F, 0x2A, 0xC0 })]
    [InlineData("cvttss2si", "eax, xmm1", new byte[] { 0xF3, 0x0F, 0x2C, 0xC1 })]
    [InlineData("pshufd", "xmm0, xmm1, 0x1b", new byte[] { 0x66, 0x0F, 0x70, 0xC1, 0x1B })]
    [InlineData("pslldq", "xmm1, 0x4", new byte[] { 0x66, 0x0F, 0x73, 0xF9, 0x04 })]
    [InlineData("pshufb", "xmm0, xmm1", new byte[] { 0x66, 0x0F, 0x38, 0x00, 0xC1 })]
    [InlineData("pcmpeqq", "xmm0, xmm1", new byte[] { 0x66, 0x0F, 0x38, 0x29, 0xC1 })]
    [InlineData("pmulld", "xmm0, xmm1", new byte[] { 0x66, 0x0F, 0x38, 0x40, 0xC1 })]
    [InlineData("roundsd", "xmm0, xmm1, 0x8", new byte[] { 0x66, 0x0F, 0x3A, 0x0B, 0xC1, 0x08 })]
    [InlineData("crc32", "eax, ecx", new byte[] { 0xF2, 0x0F, 0x38, 0xF1, 0xC1 })]
    [InlineData("xorps", "xmm0, xmm1", new byte[] { 0x0F, 0x57, 0xC1 })]
    [InlineData("cmpps", "xmm0, xmm1, 0x0", new byte[] { 0x0F, 0xC2, 0xC1, 0x00 })]
    public void Decode_Sse_MnemonicAndOperands(string mnemonic, string operands, byte[] code)
    {
        var insn = One(code);
        Assert.False(insn.IsFallback);
        Assert.Equal(mnemonic, insn.Mnemonic);
        Assert.Equal(operands, insn.OperandText);
        Assert.Equal(code.Length, insn.Length);
    }

    /// <summary>Verifies packed ops classify as Vector and scalar single/double as Float.</summary>
    [Fact(Timeout = 30_000)]
    public void Decode_Sse_ClassifiesVectorAndFloat()
    {
        Assert.Equal(NativeInstructionCategory.Vector, One(0x66, 0x0F, 0xEF, 0xC1).Category); // pxor
        Assert.Equal(NativeInstructionCategory.Float, One(0xF2, 0x0F, 0x58, 0xC1).Category);  // addsd
    }
}
