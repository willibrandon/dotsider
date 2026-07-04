using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Disasm.x64;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the x86-64 decoder's length engine and legacy surface: exact length across every
/// prefix and addressing form (the no-desync invariant), the legacy integer/control/system
/// instructions decoded to real mnemonics and operands, the enumerated runtime/system set, table
/// completeness, and the one-byte fallback resync. Real vectorized code is validated by the
/// objdump oracle and the AOT fixtures.
/// </summary>
public class XarchDecoderTests
{
    private static NativeInstruction One(ulong address, params byte[] code)
    {
        var insns = NativeDisassembler.Disassemble(code, address, NativeArchitecture.X64);
        return insns[0];
    }

    /// <summary>Verifies the representative legacy surface decodes to exact mnemonics and operands.</summary>
    [Theory(Timeout = 30_000)]
    [InlineData("nop", "", new byte[] { 0x90 })]
    [InlineData("int3", "", new byte[] { 0xCC })]
    [InlineData("ret", "", new byte[] { 0xC3 })]
    [InlineData("push", "rbp", new byte[] { 0x55 })]
    [InlineData("mov", "rbp, rsp", new byte[] { 0x48, 0x89, 0xE5 })]
    [InlineData("sub", "rsp, 0x20", new byte[] { 0x48, 0x83, 0xEC, 0x20 })]
    [InlineData("mov", "rax, qword ptr [rbp-0x8]", new byte[] { 0x48, 0x8B, 0x45, 0xF8 })]
    [InlineData("mov", "eax, dword ptr [rax+rcx*4]", new byte[] { 0x8B, 0x04, 0x88 })]
    [InlineData("mov", "rax, 0x1", new byte[] { 0x48, 0xC7, 0xC0, 0x01, 0x00, 0x00, 0x00 })]
    [InlineData("movabs", "rax, 0x8877665544332211",
        new byte[] { 0x48, 0xB8, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88 })]
    [InlineData("inc", "rax", new byte[] { 0x48, 0xFF, 0xC0 })]
    [InlineData("shl", "eax, 0x4", new byte[] { 0xC1, 0xE0, 0x04 })]
    [InlineData("test", "eax, 0x1", new byte[] { 0xF7, 0xC0, 0x01, 0x00, 0x00, 0x00 })]
    [InlineData("add", "rax, 0xffffffffffffffff", new byte[] { 0x48, 0x83, 0xC0, 0xFF })]
    [InlineData("fld", "dword ptr [rbp-0x8]", new byte[] { 0xD9, 0x45, 0xF8 })]
    [InlineData("lea", "rax, qword ptr [rip+0x0]", new byte[] { 0x48, 0x8D, 0x05, 0x00, 0x00, 0x00, 0x00 })]
    public void Decode_Legacy_MnemonicAndOperands(string mnemonic, string operands, byte[] code)
    {
        var insn = One(0x1000, code);
        Assert.False(insn.IsFallback);
        Assert.Equal(mnemonic, insn.Mnemonic);
        Assert.Equal(operands, insn.OperandText);
        Assert.Equal(code.Length, insn.Length);
    }

    /// <summary>
    /// Verifies exact length across the prefix and addressing matrix — the invariant that keeps the
    /// listing in sync. Each blob is a single instruction whose byte count is its length.
    /// </summary>
    [Theory(Timeout = 30_000)]
    [InlineData(new byte[] { 0x48, 0x01, 0xC0 })]                               // REX.W add
    [InlineData(new byte[] { 0x66, 0x01, 0xC0 })]                               // 66 opsize add ax
    [InlineData(new byte[] { 0x67, 0x8B, 0x00 })]                               // 67 addrsize [eax]
    [InlineData(new byte[] { 0x8B, 0x44, 0x88, 0x10 })]                         // SIB + disp8
    [InlineData(new byte[] { 0x8B, 0x84, 0x88, 0x00, 0x01, 0x00, 0x00 })]       // SIB + disp32
    [InlineData(new byte[] { 0x48, 0x8B, 0x05, 0x00, 0x00, 0x00, 0x00 })]       // RIP-relative
    [InlineData(new byte[] { 0x81, 0xC0, 0x00, 0x01, 0x00, 0x00 })]             // imm32
    [InlineData(new byte[] { 0x66, 0x81, 0xC0, 0x00, 0x01 })]                   // 66 imm16
    [InlineData(new byte[] { 0xEB, 0x10 })]                                     // rel8
    [InlineData(new byte[] { 0xE9, 0x10, 0x00, 0x00, 0x00 })]                   // rel32
    [InlineData(new byte[] { 0x0F, 0x1F, 0x44, 0x00, 0x00 })]                   // multi-byte nop
    public void Decode_LengthMatrix_ExactAndNoFallback(byte[] code)
    {
        var insn = One(0x2000, code);
        Assert.False(insn.IsFallback);
        Assert.Equal(code.Length, insn.Length);
    }

    /// <summary>Verifies direct branches/calls compute the absolute target and the flow kind.</summary>
    [Fact(Timeout = 30_000)]
    public void Decode_RelativeBranches_ResolveTargetAndFlow()
    {
        var call = One(0x1000, 0xE8, 0x00, 0x00, 0x00, 0x00);
        Assert.Equal("call", call.Mnemonic);
        Assert.Equal(0x1005UL, call.TargetAddress);
        Assert.Equal(NativeFlowKind.Call, call.Flow);

        var je = One(0x1000, 0x0F, 0x84, 0x10, 0x00, 0x00, 0x00);
        Assert.Equal(0x1016UL, je.TargetAddress);
        Assert.Equal(NativeFlowKind.ConditionalBranch, je.Flow);

        var jmpIndirect = One(0x1000, 0xFF, 0xE0); // jmp rax
        Assert.Equal("jmp", jmpIndirect.Mnemonic);
        Assert.Equal(NativeFlowKind.IndirectJump, jmpIndirect.Flow);
    }

    /// <summary>Verifies the enumerated runtime/system instructions decode to real mnemonics.</summary>
    [Theory(Timeout = 30_000)]
    [InlineData("nop", new byte[] { 0x90 })]
    [InlineData("pause", new byte[] { 0xF3, 0x90 })]
    [InlineData("int3", new byte[] { 0xCC })]
    [InlineData("ud2", new byte[] { 0x0F, 0x0B })]
    [InlineData("cpuid", new byte[] { 0x0F, 0xA2 })]
    [InlineData("rdtsc", new byte[] { 0x0F, 0x31 })]
    [InlineData("endbr64", new byte[] { 0xF3, 0x0F, 0x1E, 0xFA })]
    [InlineData("endbr32", new byte[] { 0xF3, 0x0F, 0x1E, 0xFB })]
    [InlineData("syscall", new byte[] { 0x0F, 0x05 })]
    [InlineData("nop", new byte[] { 0x0F, 0x1F, 0x00 })] // nop dword ptr [rax]
    public void Decode_RuntimeSystem_RealMnemonics(string mnemonic, byte[] code)
    {
        var insn = One(0x1000, code);
        Assert.Equal(mnemonic, insn.Mnemonic);
        Assert.False(insn.IsFallback);
        Assert.Equal(NativeInstructionCategory.System, insn.Category);
        Assert.Equal(code.Length, insn.Length);
    }

    /// <summary>
    /// Verifies every defined one-byte opcode decodes to a non-fallback instruction when fed a
    /// canonical operand stream — the table-completeness invariant behind "exact length by
    /// construction". A gap would surface here rather than as a silent <c>.byte</c> on real code.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Decode_EveryDefinedOneByteOpcode_IsNotFallback()
    {
        Span<byte> buffer = stackalloc byte[16];
        for (var opcode = 0; opcode < 256; opcode++)
        {
            if (!XarchTables.HasEntry(XarchTables.MapOneByte, XarchTables.PpNone, opcode)) continue;

            buffer.Clear();
            buffer[0] = (byte)opcode;
            var insn = NativeDisassembler.Disassemble(buffer.ToArray(), 0x1000, NativeArchitecture.X64)[0];
            Assert.False(insn.IsFallback, $"opcode 0x{opcode:x2} fell back to .byte");
        }
    }

    /// <summary>
    /// Verifies the decoder never throws and always advances on arbitrary bytes — no desync, no
    /// infinite loop — so a corrupt or uncovered region degrades gracefully.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Disassemble_ArbitraryBytes_NeverThrowsAndAlwaysAdvances()
    {
        var code = new byte[4096];
        for (var i = 0; i < code.Length; i++) code[i] = (byte)((i * 131 + 17) & 0xFF);

        var insns = NativeDisassembler.Disassemble(code, 0x400000, NativeArchitecture.X64);

        Assert.All(insns, i => Assert.True(i.Length >= 1));
        // Every byte is consumed except at most one truncated instruction straddling the end.
        var consumed = insns.Sum(i => i.Length);
        Assert.True(consumed <= code.Length);
        Assert.True(code.Length - consumed < 16, $"desync: {code.Length - consumed} bytes unconsumed");
    }

    /// <summary>Verifies an undefined opcode falls back to one byte and resynchronizes.</summary>
    [Fact(Timeout = 30_000)]
    public void Decode_UndefinedOpcode_OneByteFallbackResyncs()
    {
        // 0x06 (push es) is #UD in 64-bit; the following 0x90 must decode as nop.
        var insns = NativeDisassembler.Disassemble([0x06, 0x90], 0x1000, NativeArchitecture.X64);
        Assert.Equal(2, insns.Count);
        Assert.True(insns[0].IsFallback);
        Assert.Equal(1, insns[0].Length);
        Assert.Equal("nop", insns[1].Mnemonic);
    }
}
