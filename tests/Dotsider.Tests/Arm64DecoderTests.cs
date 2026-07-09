using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the A64 base decoder: data-processing (immediate and register), branches, the bitmask
/// immediate decode, branch-target resolution, and the common architectural aliases
/// (mov/cmp/cmn/tst/neg/mul/lsl/ubfx/cset) — cross-checked against Capstone. Every A64 instruction
/// is four bytes, so the fixed length is asserted throughout.
/// </summary>
[TestClass]
public class Arm64DecoderTests
{
    private static NativeInstruction One(uint word)
    {
        var code = BitConverter.GetBytes(word);
        return NativeDisassembler.Disassemble(code, 0x1000, NativeArchitecture.Arm64)[0];
    }

    private static NativeInstruction OneAt(uint word, ulong address)
    {
        var code = BitConverter.GetBytes(word);
        return NativeDisassembler.Disassemble(code, address, NativeArchitecture.Arm64)[0];
    }

    /// <summary>Decodes representative A64 base instructions to their mnemonics and operands.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow("ret", "", 0xD65F03C0u)]
    [DataRow("nop", "", 0xD503201Fu)]
    [DataRow("add", "x0, x1, x2", 0x8B020020u)]
    [DataRow("add", "x0, x1, #0x4", 0x91001020u)]
    [DataRow("mov", "x0, #0x1", 0xD2800020u)]
    [DataRow("mov", "x0, x1", 0xAA0103E0u)]
    [DataRow("cmp", "x0, #0x0", 0xF100001Fu)]
    [DataRow("cmp", "x0, x1", 0xEB01001Fu)]
    [DataRow("mul", "x0, x1, x2", 0x9B027C20u)]
    [DataRow("lsl", "x0, x1, #0x4", 0xD37CEC20u)]
    [DataRow("ubfx", "w0, w1, #0x1c, #0x1", 0x531C7020u)]
    [DataRow("udiv", "w0, w1, w2", 0x1AC20820u)]
    [DataRow("csel", "x0, x1, x2, eq", 0x9A820020u)]
    [DataRow("cset", "w0, eq", 0x1A9F17E0u)]
    [DataRow("tst", "x0, x1", 0xEA01001Fu)]
    [DataRow("neg", "x0, x1", 0xCB0103E0u)]
    [DataRow("and", "x0, x1, #0xff", 0x92401C20u)]
    [DataRow("udf", "#0x0", 0x00000000u)]
    [DataRow("udf", "#0x1", 0x00000001u)]
    public void Decode_Base_MnemonicAndOperands(string mnemonic, string operands, uint word)
    {
        var insn = One(word);
        Assert.IsFalse(insn.IsFallback);
        Assert.AreEqual(mnemonic, insn.Mnemonic);
        Assert.AreEqual(operands, insn.OperandText);
        Assert.AreEqual(4, insn.Length);
    }

    /// <summary>Verifies direct branches compute the absolute target and the flow kind.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Decode_Branches_ResolveTargetAndFlow()
    {
        var bl = One(0x94000010u);      // bl +0x40
        Assert.AreEqual(0x1040UL, bl.TargetAddress);
        Assert.AreEqual(NativeFlowKind.Call, bl.Flow);

        var beq = One(0x54000040u);     // b.eq +8
        Assert.AreEqual("b.eq", beq.Mnemonic);
        Assert.AreEqual(0x1008UL, beq.TargetAddress);
        Assert.AreEqual(NativeFlowKind.ConditionalBranch, beq.Flow);

        var cbz = One(0xB4000040u);     // cbz x0, +8
        Assert.AreEqual(NativeFlowKind.ConditionalBranch, cbz.Flow);

        var ret = One(0xD65F03C0u);
        Assert.AreEqual(NativeFlowKind.Return, ret.Flow);
    }

    /// <summary>Verifies adrp computes the page-aligned target off the aligned instruction address.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Decode_Adrp_ComputesPageTarget()
    {
        var insn = One(0x90000000u); // adrp x0, page(0)
        Assert.AreEqual("adrp", insn.Mnemonic);
        Assert.AreEqual(0x1000UL, insn.TargetAddress);
    }

    /// <summary>Verifies ADR/ADRP concatenate immhi:immlo in architectural order.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Decode_AdrAndAdrp_UsesImmhiImmloOrder()
    {
        var adr = One(0x10000440u); // adr x0, +0x88
        Assert.AreEqual("adr", adr.Mnemonic);
        Assert.AreEqual(0x1088UL, adr.TargetAddress);

        // Real osx-arm64 R2R import-slot pattern: llvm-objdump decodes this as
        // "adrp x11, 0x180032000" at 0x180010e28.
        var adrp = OneAt(0xD000010Bu, 0x180010E28UL);
        Assert.AreEqual("adrp", adrp.Mnemonic);
        Assert.AreEqual(0x180032000UL, adrp.TargetAddress);
    }

    /// <summary>Verifies an arm64 import-slot call sequence is named structurally.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Decode_Arm64ImportSlotCall_ResolvesTargetName()
    {
        byte[] code =
        [
            0x0B, 0x01, 0x00, 0xD0, // adrp x11, 0x180032000
            0x6B, 0x21, 0x1E, 0x91, // add  x11, x11, #0x788
            0x70, 0x01, 0x40, 0xF9, // ldr  x16, [x11]
            0x00, 0x02, 0x3F, 0xD6, // blr  x16
        ];

        static bool Resolve(ulong va, out NativeSymbolRef symbol)
        {
            if (va == 0x180032788UL)
            {
                symbol = new NativeSymbolRef(va, "Console.WriteLine", NativeSymbolKind.Stub, 0);
                return true;
            }

            symbol = default;
            return false;
        }

        var instructions = NativeDisassembler.Disassemble(
            code, 0x180010E28UL, NativeArchitecture.Arm64, Resolve);

        var branch = instructions[^1];
        Assert.AreEqual("blr", branch.Mnemonic);
        Assert.AreEqual(NativeTargetKind.Import, branch.TargetKind);
        Assert.AreEqual("Console.WriteLine", branch.TargetName);
    }

    /// <summary>Verifies an unallocated word decodes as a 4-byte .word that never desyncs.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Decode_Unallocated_EmitsWord()
    {
        // bits[28:25] = 0b0001 is an unallocated top-level class (no decode group), so it desyncs
        // into a .word. 0x00000000 is deliberately NOT used here — it is udf #0, a defined encoding.
        var insn = One(0x02000000u);
        Assert.IsTrue(insn.IsFallback);
        Assert.AreEqual(".word", insn.Mnemonic);
        Assert.AreEqual(4, insn.Length);
    }
}
