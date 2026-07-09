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
[TestClass]
public class XarchDecoderTests
{
    private static NativeInstruction One(ulong address, params byte[] code)
    {
        var insns = NativeDisassembler.Disassemble(code, address, NativeArchitecture.X64);
        return insns[0];
    }

    /// <summary>Verifies the representative legacy surface decodes to exact mnemonics and operands.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow("nop", "", new byte[] { 0x90 })]
    [DataRow("int3", "", new byte[] { 0xCC })]
    [DataRow("ret", "", new byte[] { 0xC3 })]
    [DataRow("push", "rbp", new byte[] { 0x55 })]
    [DataRow("mov", "rbp, rsp", new byte[] { 0x48, 0x89, 0xE5 })]
    [DataRow("sub", "rsp, 0x20", new byte[] { 0x48, 0x83, 0xEC, 0x20 })]
    [DataRow("mov", "rax, qword ptr [rbp-0x8]", new byte[] { 0x48, 0x8B, 0x45, 0xF8 })]
    [DataRow("mov", "eax, dword ptr [rax+rcx*4]", new byte[] { 0x8B, 0x04, 0x88 })]
    [DataRow("mov", "rax, 0x1", new byte[] { 0x48, 0xC7, 0xC0, 0x01, 0x00, 0x00, 0x00 })]
    [DataRow("movabs", "rax, 0x8877665544332211",
        new byte[] { 0x48, 0xB8, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88 })]
    [DataRow("inc", "rax", new byte[] { 0x48, 0xFF, 0xC0 })]
    [DataRow("shl", "eax, 0x4", new byte[] { 0xC1, 0xE0, 0x04 })]
    [DataRow("test", "eax, 0x1", new byte[] { 0xF7, 0xC0, 0x01, 0x00, 0x00, 0x00 })]
    [DataRow("add", "rax, 0xffffffffffffffff", new byte[] { 0x48, 0x83, 0xC0, 0xFF })]
    [DataRow("fld", "dword ptr [rbp-0x8]", new byte[] { 0xD9, 0x45, 0xF8 })]
    [DataRow("lea", "rax, qword ptr [rip+0x0]", new byte[] { 0x48, 0x8D, 0x05, 0x00, 0x00, 0x00, 0x00 })]
    public void Decode_Legacy_MnemonicAndOperands(string mnemonic, string operands, byte[] code)
    {
        var insn = One(0x1000, code);
        Assert.IsFalse(insn.IsFallback);
        Assert.AreEqual(mnemonic, insn.Mnemonic);
        Assert.AreEqual(operands, insn.OperandText);
        Assert.AreEqual(code.Length, insn.Length);
    }

    /// <summary>
    /// Verifies exact length across the prefix and addressing matrix — the invariant that keeps the
    /// listing in sync. Each blob is a single instruction whose byte count is its length.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(new byte[] { 0x48, 0x01, 0xC0 })]                               // REX.W add
    [DataRow(new byte[] { 0x66, 0x01, 0xC0 })]                               // 66 opsize add ax
    [DataRow(new byte[] { 0x67, 0x8B, 0x00 })]                               // 67 addrsize [eax]
    [DataRow(new byte[] { 0x8B, 0x44, 0x88, 0x10 })]                         // SIB + disp8
    [DataRow(new byte[] { 0x8B, 0x84, 0x88, 0x00, 0x01, 0x00, 0x00 })]       // SIB + disp32
    [DataRow(new byte[] { 0x48, 0x8B, 0x05, 0x00, 0x00, 0x00, 0x00 })]       // RIP-relative
    [DataRow(new byte[] { 0x81, 0xC0, 0x00, 0x01, 0x00, 0x00 })]             // imm32
    [DataRow(new byte[] { 0x66, 0x81, 0xC0, 0x00, 0x01 })]                   // 66 imm16
    [DataRow(new byte[] { 0xEB, 0x10 })]                                     // rel8
    [DataRow(new byte[] { 0xE9, 0x10, 0x00, 0x00, 0x00 })]                   // rel32
    [DataRow(new byte[] { 0x0F, 0x1F, 0x44, 0x00, 0x00 })]                   // multi-byte nop
    public void Decode_LengthMatrix_ExactAndNoFallback(byte[] code)
    {
        var insn = One(0x2000, code);
        Assert.IsFalse(insn.IsFallback);
        Assert.AreEqual(code.Length, insn.Length);
    }

    /// <summary>Verifies direct branches/calls compute the absolute target and the flow kind.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Decode_RelativeBranches_ResolveTargetAndFlow()
    {
        var call = One(0x1000, 0xE8, 0x00, 0x00, 0x00, 0x00);
        Assert.AreEqual("call", call.Mnemonic);
        Assert.AreEqual(0x1005UL, call.TargetAddress);
        Assert.AreEqual(NativeFlowKind.Call, call.Flow);

        var je = One(0x1000, 0x0F, 0x84, 0x10, 0x00, 0x00, 0x00);
        Assert.AreEqual(0x1016UL, je.TargetAddress);
        Assert.AreEqual(NativeFlowKind.ConditionalBranch, je.Flow);

        var jmpIndirect = One(0x1000, 0xFF, 0xE0); // jmp rax
        Assert.AreEqual("jmp", jmpIndirect.Mnemonic);
        Assert.AreEqual(NativeFlowKind.IndirectJump, jmpIndirect.Flow);
    }

    /// <summary>Verifies the enumerated runtime/system instructions decode to real mnemonics.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow("nop", new byte[] { 0x90 })]
    [DataRow("pause", new byte[] { 0xF3, 0x90 })]
    [DataRow("int3", new byte[] { 0xCC })]
    [DataRow("ud2", new byte[] { 0x0F, 0x0B })]
    [DataRow("cpuid", new byte[] { 0x0F, 0xA2 })]
    [DataRow("rdtsc", new byte[] { 0x0F, 0x31 })]
    [DataRow("endbr64", new byte[] { 0xF3, 0x0F, 0x1E, 0xFA })]
    [DataRow("endbr32", new byte[] { 0xF3, 0x0F, 0x1E, 0xFB })]
    [DataRow("syscall", new byte[] { 0x0F, 0x05 })]
    [DataRow("nop", new byte[] { 0x0F, 0x1F, 0x00 })] // nop dword ptr [rax]
    public void Decode_RuntimeSystem_RealMnemonics(string mnemonic, byte[] code)
    {
        var insn = One(0x1000, code);
        Assert.AreEqual(mnemonic, insn.Mnemonic);
        Assert.IsFalse(insn.IsFallback);
        Assert.AreEqual(NativeInstructionCategory.System, insn.Category);
        Assert.AreEqual(code.Length, insn.Length);
    }

    /// <summary>
    /// Verifies every defined one-byte opcode decodes to a non-fallback instruction when fed a
    /// canonical operand stream — the table-completeness invariant behind "exact length by
    /// construction". A gap would surface here rather than as a silent <c>.byte</c> on real code.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Decode_EveryDefinedOneByteOpcode_IsNotFallback()
    {
        Span<byte> buffer = stackalloc byte[16];
        for (var opcode = 0; opcode < 256; opcode++)
        {
            if (!XarchTables.HasEntry(XarchTables.MapOneByte, XarchTables.PpNone, opcode)) continue;

            buffer.Clear();
            buffer[0] = (byte)opcode;
            var insn = NativeDisassembler.Disassemble(buffer.ToArray(), 0x1000, NativeArchitecture.X64)[0];
            Assert.IsFalse(insn.IsFallback, $"opcode 0x{opcode:x2} fell back to .byte");
        }
    }

    /// <summary>
    /// Verifies the decoder never throws and always advances on arbitrary bytes — no desync, no
    /// infinite loop — so a corrupt or uncovered region degrades gracefully.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Disassemble_ArbitraryBytes_NeverThrowsAndAlwaysAdvances()
    {
        var code = new byte[4096];
        for (var i = 0; i < code.Length; i++) code[i] = (byte)((i * 131 + 17) & 0xFF);

        var insns = NativeDisassembler.Disassemble(code, 0x400000, NativeArchitecture.X64);

        TestAssert.All(insns, i => Assert.IsGreaterThanOrEqualTo(1, i.Length));
        // Every byte is consumed except at most one truncated instruction straddling the end.
        var consumed = insns.Sum(i => i.Length);
        Assert.IsLessThanOrEqualTo(code.Length, consumed);
        Assert.IsLessThan(16, code.Length - consumed, $"desync: {code.Length - consumed} bytes unconsumed");
    }

    /// <summary>Verifies an undefined opcode falls back to one byte and resynchronizes.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Decode_UndefinedOpcode_OneByteFallbackResyncs()
    {
        // 0x06 (push es) is #UD in 64-bit; the following 0x90 must decode as nop.
        var insns = NativeDisassembler.Disassemble([0x06, 0x90], 0x1000, NativeArchitecture.X64);
        Assert.HasCount(2, insns);
        Assert.IsTrue(insns[0].IsFallback);
        Assert.AreEqual(1, insns[0].Length);
        Assert.AreEqual("nop", insns[1].Mnemonic);
    }
}
