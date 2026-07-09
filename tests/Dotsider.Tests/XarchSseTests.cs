using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the x86-64 SSE–SSE4.2 decode: packed and scalar arithmetic, moves with the correct
/// memory size hints (scalar dword/qword vs packed xmmword), conversions, packed-integer ops,
/// the 0F 38 / 0F 3A maps, and the shift-by-immediate groups — cross-checked against objdump.
/// </summary>
[TestClass]
public class XarchSseTests
{
    private static NativeInstruction One(params byte[] code) =>
        NativeDisassembler.Disassemble(code, 0x1000, NativeArchitecture.X64)[0];

    /// <summary>Decodes representative SSE opcodes to their exact mnemonics, operands, and length.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow("movaps", "xmm0, xmm1", new byte[] { 0x0F, 0x28, 0xC1 })]
    [DataRow("movapd", "xmm0, xmm1", new byte[] { 0x66, 0x0F, 0x28, 0xC1 })]
    [DataRow("movss", "xmm0, dword ptr [rax]", new byte[] { 0xF3, 0x0F, 0x10, 0x00 })]
    [DataRow("movsd", "xmm0, qword ptr [rax]", new byte[] { 0xF2, 0x0F, 0x10, 0x00 })]
    [DataRow("addps", "xmm0, xmm1", new byte[] { 0x0F, 0x58, 0xC1 })]
    [DataRow("addsd", "xmm0, xmm1", new byte[] { 0xF2, 0x0F, 0x58, 0xC1 })]
    [DataRow("mulss", "xmm0, xmm1", new byte[] { 0xF3, 0x0F, 0x59, 0xC1 })]
    [DataRow("pxor", "xmm0, xmm1", new byte[] { 0x66, 0x0F, 0xEF, 0xC1 })]
    [DataRow("paddd", "xmm0, xmm1", new byte[] { 0x66, 0x0F, 0xFE, 0xC1 })]
    [DataRow("movdqu", "xmm0, xmmword ptr [rax]", new byte[] { 0xF3, 0x0F, 0x6F, 0x00 })]
    [DataRow("movd", "xmm0, eax", new byte[] { 0x66, 0x0F, 0x6E, 0xC0 })]
    [DataRow("movq", "xmm0, rax", new byte[] { 0x66, 0x48, 0x0F, 0x6E, 0xC0 })]
    [DataRow("cvtsi2sd", "xmm0, eax", new byte[] { 0xF2, 0x0F, 0x2A, 0xC0 })]
    [DataRow("cvttss2si", "eax, xmm1", new byte[] { 0xF3, 0x0F, 0x2C, 0xC1 })]
    [DataRow("pshufd", "xmm0, xmm1, 0x1b", new byte[] { 0x66, 0x0F, 0x70, 0xC1, 0x1B })]
    [DataRow("pslldq", "xmm1, 0x4", new byte[] { 0x66, 0x0F, 0x73, 0xF9, 0x04 })]
    [DataRow("pshufb", "xmm0, xmm1", new byte[] { 0x66, 0x0F, 0x38, 0x00, 0xC1 })]
    [DataRow("pcmpeqq", "xmm0, xmm1", new byte[] { 0x66, 0x0F, 0x38, 0x29, 0xC1 })]
    [DataRow("pmulld", "xmm0, xmm1", new byte[] { 0x66, 0x0F, 0x38, 0x40, 0xC1 })]
    [DataRow("roundsd", "xmm0, xmm1, 0x8", new byte[] { 0x66, 0x0F, 0x3A, 0x0B, 0xC1, 0x08 })]
    [DataRow("crc32", "eax, ecx", new byte[] { 0xF2, 0x0F, 0x38, 0xF1, 0xC1 })]
    [DataRow("xorps", "xmm0, xmm1", new byte[] { 0x0F, 0x57, 0xC1 })]
    [DataRow("cmpps", "xmm0, xmm1, 0x0", new byte[] { 0x0F, 0xC2, 0xC1, 0x00 })]
    public void Decode_Sse_MnemonicAndOperands(string mnemonic, string operands, byte[] code)
    {
        var insn = One(code);
        Assert.IsFalse(insn.IsFallback);
        Assert.AreEqual(mnemonic, insn.Mnemonic);
        Assert.AreEqual(operands, insn.OperandText);
        Assert.AreEqual(code.Length, insn.Length);
    }

    /// <summary>Verifies packed ops classify as Vector and scalar single/double as Float.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Decode_Sse_ClassifiesVectorAndFloat()
    {
        Assert.AreEqual(NativeInstructionCategory.Vector, One(0x66, 0x0F, 0xEF, 0xC1).Category); // pxor
        Assert.AreEqual(NativeInstructionCategory.Float, One(0xF2, 0x0F, 0x58, 0xC1).Category);  // addsd
    }
}
