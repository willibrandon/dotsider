using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the x86-64 AVX-512 (EVEX) decode: ZMM vectors, the d/q width suffix by EVEX.W, opmask
/// {k}/{z} decoration, the EVEX-only ops (ternary logic, VNNI, conflict/lzcnt, scale, mask
/// compares), and the VEX-encoded opmask moves — cross-checked against objdump.
/// </summary>
public class XarchAvx512Tests
{
    private static NativeInstruction One(params byte[] code) =>
        NativeDisassembler.Disassemble(code, 0x1000, NativeArchitecture.X64)[0];

    /// <summary>Decodes representative EVEX-encoded AVX-512 ops to their mnemonics, operands, and length.</summary>
    [Theory(Timeout = 30_000)]
    [InlineData("vpaddd", "zmm0, zmm1, zmm2", new byte[] { 0x62, 0xF1, 0x75, 0x48, 0xFE, 0xC2 })]
    [InlineData("vpaddd", "zmm0{k1}, zmm1, zmm2", new byte[] { 0x62, 0xF1, 0x75, 0x49, 0xFE, 0xC2 })]
    [InlineData("vpternlogd", "zmm0, zmm1, zmm2, 0xff", new byte[] { 0x62, 0xF3, 0x75, 0x48, 0x25, 0xC2, 0xFF })]
    [InlineData("vpdpbusd", "zmm0, zmm1, zmm2", new byte[] { 0x62, 0xF2, 0x75, 0x48, 0x50, 0xC2 })]
    [InlineData("vpcmpd", "k1, zmm1, zmm2, 0x0", new byte[] { 0x62, 0xF3, 0x75, 0x48, 0x1F, 0xCA, 0x00 })]
    [InlineData("vscalefps", "zmm0, zmm1, zmm2", new byte[] { 0x62, 0xF2, 0x75, 0x48, 0x2C, 0xC2 })]
    [InlineData("vplzcntq", "zmm0, zmm1", new byte[] { 0x62, 0xF2, 0xFD, 0x48, 0x44, 0xC1 })]
    [InlineData("vpxord", "zmm0, zmm1, zmm2", new byte[] { 0x62, 0xF1, 0x75, 0x48, 0xEF, 0xC2 })]
    [InlineData("kmovw", "k1, k2", new byte[] { 0xC5, 0xF8, 0x90, 0xCA })]
    public void Decode_Avx512_MnemonicAndOperands(string mnemonic, string operands, byte[] code)
    {
        var insn = One(code);
        Assert.False(insn.IsFallback);
        Assert.Equal(mnemonic, insn.Mnemonic);
        Assert.Equal(operands, insn.OperandText);
        Assert.Equal(code.Length, insn.Length);
    }

    /// <summary>Verifies EVEX.512 selects zmm registers and zeroing renders {z}.</summary>
    [Fact(Timeout = 30_000)]
    public void Decode_Evex_ZmmAndZeroing()
    {
        // vpaddd zmm0{k1}{z}, zmm1, zmm2 — z bit set in P2.
        var insn = One(0x62, 0xF1, 0x75, 0xC9, 0xFE, 0xC2);
        Assert.Equal("zmm0{k1}{z}, zmm1, zmm2", insn.OperandText);
    }
}
