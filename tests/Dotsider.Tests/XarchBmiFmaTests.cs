using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the x86-64 FMA and BMI1/BMI2/ADX decode: FMA's pd/sd vs ps/ss suffix by VEX.W, and
/// the VEX-encoded BMI ops that keep their plain mnemonics and take a GPR vvvv source — all
/// cross-checked against objdump.
/// </summary>
public class XarchBmiFmaTests
{
    private static NativeInstruction One(params byte[] code) =>
        NativeDisassembler.Disassemble(code, 0x1000, NativeArchitecture.X64)[0];

    /// <summary>Decodes representative FMA and BMI/ADX ops to their mnemonics, operands, and length.</summary>
    [Theory(Timeout = 30_000)]
    [InlineData("vfmadd231ps", "xmm0, xmm1, xmm2", new byte[] { 0xC4, 0xE2, 0x71, 0xB8, 0xC2 })]
    [InlineData("vfmadd231pd", "xmm0, xmm1, xmm2", new byte[] { 0xC4, 0xE2, 0xF1, 0xB8, 0xC2 })]
    [InlineData("vfmadd213sd", "xmm0, xmm1, xmm2", new byte[] { 0xC4, 0xE2, 0xF1, 0xA9, 0xC2 })]
    [InlineData("vfnmadd231ps", "xmm0, xmm1, xmm2", new byte[] { 0xC4, 0xE2, 0x71, 0xBC, 0xC2 })]
    [InlineData("andn", "eax, ebx, ecx", new byte[] { 0xC4, 0xE2, 0x60, 0xF2, 0xC1 })]
    [InlineData("blsr", "eax, ecx", new byte[] { 0xC4, 0xE2, 0x78, 0xF3, 0xC9 })]
    [InlineData("blsi", "eax, ecx", new byte[] { 0xC4, 0xE2, 0x78, 0xF3, 0xD9 })]
    [InlineData("mulx", "eax, ebx, ecx", new byte[] { 0xC4, 0xE2, 0x63, 0xF6, 0xC1 })]
    [InlineData("rorx", "eax, ecx, 0x5", new byte[] { 0xC4, 0xE3, 0x7B, 0xF0, 0xC1, 0x05 })]
    [InlineData("bextr", "eax, ecx, edx", new byte[] { 0xC4, 0xE2, 0x68, 0xF7, 0xC1 })]
    [InlineData("popcnt", "eax, ecx", new byte[] { 0xF3, 0x0F, 0xB8, 0xC1 })]
    [InlineData("adcx", "eax, ecx", new byte[] { 0x66, 0x0F, 0x38, 0xF6, 0xC1 })]
    [InlineData("adox", "eax, ecx", new byte[] { 0xF3, 0x0F, 0x38, 0xF6, 0xC1 })]
    public void Decode_BmiFma_MnemonicAndOperands(string mnemonic, string operands, byte[] code)
    {
        var insn = One(code);
        Assert.False(insn.IsFallback);
        Assert.Equal(mnemonic, insn.Mnemonic);
        Assert.Equal(operands, insn.OperandText);
        Assert.Equal(code.Length, insn.Length);
    }
}
