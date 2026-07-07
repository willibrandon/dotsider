using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Decoder dispatch tests for non-x64 native architectures.
/// These byte-level tests exercise the public disassembly API directly.
/// Real ReadyToRun fixture tests cover SDK-emitted architecture bodies.
/// </summary>
public class NativeArchitectureDecoderTests
{
    /// <summary>
    /// Verifies x86 bytes decode as 32-bit x86, not x64 REX prefixes.
    /// </summary>
    [Fact]
    public void X86_Decodes32BitOpcodesWithoutRexFallback()
    {
        var insns = NativeDisassembler.Disassemble([0x40, 0xC3], 0x1000, NativeArchitecture.X86);

        Assert.Equal(2, insns.Count);
        Assert.Equal("inc", insns[0].Mnemonic);
        Assert.Equal("eax", insns[0].Operands[0].Register);
        Assert.Equal(1, insns[0].Length);
        Assert.Equal("ret", insns[1].Mnemonic);
        Assert.Equal(NativeFlowKind.Return, insns[1].Flow);
    }

    /// <summary>
    /// Verifies Thumb-16 instructions and returns decode structurally.
    /// </summary>
    [Fact]
    public void Arm32_DecodesThumb16AndReturn()
    {
        var insns = NativeDisassembler.Disassemble([0x01, 0x20, 0x70, 0x47], 0x2000, NativeArchitecture.Arm32);

        Assert.Equal(2, insns.Count);
        Assert.Equal("movs", insns[0].Mnemonic);
        Assert.Equal("r0", insns[0].Operands[0].Register);
        Assert.Equal(2, insns[0].Length);
        Assert.Equal("bx", insns[1].Mnemonic);
        Assert.Equal(NativeFlowKind.Return, insns[1].Flow);
    }

    /// <summary>
    /// Verifies a Thumb-2 instruction is kept as one four-byte instruction.
    /// </summary>
    [Fact]
    public void Arm32_DecodesThumb32AsOneInstruction()
    {
        var insns = NativeDisassembler.Disassemble([0x00, 0xF0, 0x00, 0xF8], 0x3000, NativeArchitecture.Arm32);

        var insn = Assert.Single(insns);
        Assert.Equal("bl", insn.Mnemonic);
        Assert.Equal(4, insn.Length);
        Assert.Equal(NativeFlowKind.Call, insn.Flow);
    }

    /// <summary>
    /// Verifies Thumb undefined traps decode as real padding, not fallback.
    /// </summary>
    [Fact]
    public void Arm32_DecodesThumbUdfTrapPadding()
    {
        var insn = Assert.Single(NativeDisassembler.Disassemble([0x01, 0xDE], 0x3100, NativeArchitecture.Arm32));

        Assert.Equal("udf", insn.Mnemonic);
        Assert.Equal("#0x1", insn.OperandText);
        Assert.False(insn.IsFallback);
    }

    /// <summary>
    /// Verifies RV64 base instructions and canonical returns decode structurally.
    /// </summary>
    [Fact]
    public void RiscV64_DecodesBaseAndReturn()
    {
        var insns = NativeDisassembler.Disassemble(
            [0x13, 0x05, 0x10, 0x00, 0x67, 0x80, 0x00, 0x00],
            0x4000,
            NativeArchitecture.RiscV64);

        Assert.Equal(2, insns.Count);
        Assert.Equal("addi", insns[0].Mnemonic);
        Assert.Equal("a0", insns[0].Operands[0].Register);
        Assert.Equal(4, insns[0].Length);
        Assert.Equal("jalr", insns[1].Mnemonic);
        Assert.Equal(NativeFlowKind.Return, insns[1].Flow);
    }

    /// <summary>
    /// Verifies compressed RISC-V instructions retain their two-byte length.
    /// </summary>
    [Fact]
    public void RiscV64_DecodesCompressedNop()
    {
        var insn = Assert.Single(NativeDisassembler.Disassemble([0x01, 0x00], 0x5000, NativeArchitecture.RiscV64));

        Assert.Equal("c.nop", insn.Mnemonic);
        Assert.Equal(2, insn.Length);
    }

    /// <summary>
    /// Verifies LoongArch64 runtime-table rows decode structurally.
    /// </summary>
    [Fact]
    public void LoongArch64_DecodesRuntimeTableRows()
    {
        var insns = NativeDisassembler.Disassemble(
            [0x00, 0x00, 0x40, 0x03, 0x84, 0x00, 0xC0, 0x02],
            0x6000,
            NativeArchitecture.LoongArch64);

        Assert.Equal(2, insns.Count);
        Assert.Equal("nop", insns[0].Mnemonic);
        Assert.Equal(4, insns[0].Length);
        Assert.Equal("addi.d", insns[1].Mnemonic);
        Assert.Equal("r4", insns[1].Operands[0].Register);
    }

    /// <summary>
    /// Verifies Wasm LEB immediates and calls decode structurally.
    /// </summary>
    [Fact]
    public void Wasm32_DecodesLebAndCall()
    {
        var insns = NativeDisassembler.Disassemble([0x41, 0x2A, 0x10, 0x03, 0x0B], 0, NativeArchitecture.Wasm32);

        Assert.Equal(3, insns.Count);
        Assert.Equal("i32.const", insns[0].Mnemonic);
        Assert.Equal(42, insns[0].Operands[0].Immediate);
        Assert.Equal(2, insns[0].Length);
        Assert.Equal("call", insns[1].Mnemonic);
        Assert.Equal(NativeFlowKind.Call, insns[1].Flow);
        Assert.Equal("end", insns[2].Mnemonic);
    }
}
