using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the A64 load/store decode: register offsets (unsigned/unscaled/pre/post/register),
/// pairs (incl. the prologue/epilogue stp/ldp pre/post-index), SIMD/FP transfers by width,
/// PC-relative literals, exclusive and acquire/release accesses, and LSE atomics — cross-checked
/// against Capstone.
/// </summary>
public class Arm64LoadStoreTests
{
    private static NativeInstruction One(uint word)
    {
        var code = BitConverter.GetBytes(word);
        return NativeDisassembler.Disassemble(code, 0x1000, NativeArchitecture.Arm64)[0];
    }

    /// <summary>Decodes representative A64 load/store forms to their mnemonics and addressing modes.</summary>
    [Theory(Timeout = 30_000)]
    [InlineData("ldr", "x0, [x1]", 0xF9400020u)]
    [InlineData("str", "x0, [x1]", 0xF9000020u)]
    [InlineData("ldrb", "w0, [x1]", 0x39400020u)]
    [InlineData("ldr", "w0, [x1]", 0xB9400020u)]
    [InlineData("ldr", "x0, [x30], #0x10", 0xF84107C0u)]
    [InlineData("str", "x0, [sp, #0x8]!", 0xF8008FE0u)]
    [InlineData("stp", "x29, x30, [sp, #-0x10]!", 0xA9BF7BFDu)]
    [InlineData("ldp", "x29, x30, [sp], #0x10", 0xA8C17BFDu)]
    [InlineData("ldr", "x0, [x1, x0]", 0xF8606820u)]
    [InlineData("ldr", "d0, [x1]", 0xFD400020u)]
    [InlineData("ldr", "q0, [x1]", 0x3DC00020u)]
    [InlineData("ldr", "s0, [x1]", 0xBD400020u)]
    [InlineData("ldxr", "x0, [x1]", 0xC85F7C20u)]
    [InlineData("stlr", "x0, [x1]", 0xC89FFC20u)]
    [InlineData("ldar", "x0, [x1]", 0xC8DFFC20u)]
    [InlineData("ldaxr", "w0, [x1]", 0x885FFC20u)]
    [InlineData("ldadd", "w0, w1, [x2]", 0xB8200041u)]
    public void Decode_LoadStore_MnemonicAndOperands(string mnemonic, string operands, uint word)
    {
        var insn = One(word);
        Assert.False(insn.IsFallback);
        Assert.Equal(mnemonic, insn.Mnemonic);
        Assert.Equal(operands, insn.OperandText);
        Assert.Equal(4, insn.Length);
    }

    /// <summary>Verifies a PC-relative literal load resolves its absolute target.</summary>
    [Fact(Timeout = 30_000)]
    public void Decode_LiteralLoad_ResolvesTarget()
    {
        var insn = One(0x58000040u); // ldr x0, +8
        Assert.Equal("ldr", insn.Mnemonic);
        Assert.Equal(0x1008UL, insn.TargetAddress);
    }
}
