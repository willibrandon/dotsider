using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the A64 base decoder: data-processing (immediate and register), branches, the bitmask
/// immediate decode, branch-target resolution, and the common architectural aliases
/// (mov/cmp/cmn/tst/neg/mul/lsl/ubfx/cset) — cross-checked against Capstone. Every A64 instruction
/// is four bytes, so the fixed length is asserted throughout.
/// </summary>
public class Arm64DecoderTests
{
    private static NativeInstruction One(uint word)
    {
        var code = BitConverter.GetBytes(word);
        return NativeDisassembler.Disassemble(code, 0x1000, NativeArchitecture.Arm64)[0];
    }

    /// <summary>Decodes representative A64 base instructions to their mnemonics and operands.</summary>
    [Theory(Timeout = 30_000)]
    [InlineData("ret", "", 0xD65F03C0u)]
    [InlineData("nop", "", 0xD503201Fu)]
    [InlineData("add", "x0, x1, x2", 0x8B020020u)]
    [InlineData("add", "x0, x1, #0x4", 0x91001020u)]
    [InlineData("mov", "x0, #0x1", 0xD2800020u)]
    [InlineData("mov", "x0, x1", 0xAA0103E0u)]
    [InlineData("cmp", "x0, #0x0", 0xF100001Fu)]
    [InlineData("cmp", "x0, x1", 0xEB01001Fu)]
    [InlineData("mul", "x0, x1, x2", 0x9B027C20u)]
    [InlineData("lsl", "x0, x1, #0x4", 0xD37CEC20u)]
    [InlineData("ubfx", "w0, w1, #0x1c, #0x1", 0x531C7020u)]
    [InlineData("udiv", "w0, w1, w2", 0x1AC20820u)]
    [InlineData("csel", "x0, x1, x2, eq", 0x9A820020u)]
    [InlineData("cset", "w0, eq", 0x1A9F17E0u)]
    [InlineData("tst", "x0, x1", 0xEA01001Fu)]
    [InlineData("neg", "x0, x1", 0xCB0103E0u)]
    [InlineData("and", "x0, x1, #0xff", 0x92401C20u)]
    [InlineData("udf", "#0x0", 0x00000000u)]
    [InlineData("udf", "#0x1", 0x00000001u)]
    public void Decode_Base_MnemonicAndOperands(string mnemonic, string operands, uint word)
    {
        var insn = One(word);
        Assert.False(insn.IsFallback);
        Assert.Equal(mnemonic, insn.Mnemonic);
        Assert.Equal(operands, insn.OperandText);
        Assert.Equal(4, insn.Length);
    }

    /// <summary>Verifies direct branches compute the absolute target and the flow kind.</summary>
    [Fact(Timeout = 30_000)]
    public void Decode_Branches_ResolveTargetAndFlow()
    {
        var bl = One(0x94000010u);      // bl +0x40
        Assert.Equal(0x1040UL, bl.TargetAddress);
        Assert.Equal(NativeFlowKind.Call, bl.Flow);

        var beq = One(0x54000040u);     // b.eq +8
        Assert.Equal("b.eq", beq.Mnemonic);
        Assert.Equal(0x1008UL, beq.TargetAddress);
        Assert.Equal(NativeFlowKind.ConditionalBranch, beq.Flow);

        var cbz = One(0xB4000040u);     // cbz x0, +8
        Assert.Equal(NativeFlowKind.ConditionalBranch, cbz.Flow);

        var ret = One(0xD65F03C0u);
        Assert.Equal(NativeFlowKind.Return, ret.Flow);
    }

    /// <summary>Verifies adrp computes the page-aligned target off the aligned instruction address.</summary>
    [Fact(Timeout = 30_000)]
    public void Decode_Adrp_ComputesPageTarget()
    {
        var insn = One(0x90000000u); // adrp x0, page(0)
        Assert.Equal("adrp", insn.Mnemonic);
        Assert.Equal(0x1000UL, insn.TargetAddress);
    }

    /// <summary>Verifies an unallocated word decodes as a 4-byte .word that never desyncs.</summary>
    [Fact(Timeout = 30_000)]
    public void Decode_Unallocated_EmitsWord()
    {
        // bits[28:25] = 0b0001 is an unallocated top-level class (no decode group), so it desyncs
        // into a .word. 0x00000000 is deliberately NOT used here — it is udf #0, a defined encoding.
        var insn = One(0x02000000u);
        Assert.True(insn.IsFallback);
        Assert.Equal(".word", insn.Mnemonic);
        Assert.Equal(4, insn.Length);
    }
}
