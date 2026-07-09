using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the <see cref="NativeDisassembler"/> facade skeleton: the exact-width fallback that
/// keeps the listing in sync, and the text renderer that stamps <see cref="NativeInstruction.DisplayLine"/>
/// and <see cref="NativeLineLayout"/> spans the decoration providers rely on. The real decoders are
/// exercised in the per-family suites; here the wiring is proven.
/// </summary>
[TestClass]
public class NativeDisassemblerTests
{
    /// <summary>Verifies undefined x64 opcodes fall back to one <c>.byte</c> each and never desync.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Disassemble_X64Undefined_EmitsExactWidthBytes()
    {
        // 0x06 (push es) and 0x0E (push cs) are both #UD in 64-bit → one-byte fallbacks.
        byte[] code = [0x06, 0x0E];
        var insns = NativeDisassembler.Disassemble(code, 0x1000, NativeArchitecture.X64);

        Assert.HasCount(2, insns);
        TestAssert.All(insns, i => Assert.IsTrue(i.IsFallback));
        TestAssert.All(insns, i => Assert.AreEqual(".byte", i.Mnemonic));
        TestAssert.All(insns, i => Assert.AreEqual(1, i.Length));
        Assert.AreEqual(0x1000UL, insns[0].Address);
        Assert.AreEqual(0x1001UL, insns[1].Address);
        Assert.AreEqual(NativeInstructionCategory.Unknown, insns[0].Category);
    }

    /// <summary>Verifies unallocated arm64 words fall back to one 32-bit <c>.word</c> each.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Disassemble_Arm64Unknown_EmitsWords()
    {
        // bits[28:25]=0001 is an unallocated major class (no decode group). 0x00000000 is NOT used —
        // it is udf #0, a defined encoding; 0x02000002 / 0x02000003 are little-endian words here.
        byte[] code = [0x02, 0x00, 0x00, 0x02, 0x03, 0x00, 0x00, 0x02];
        var insns = NativeDisassembler.Disassemble(code, 0x4000, NativeArchitecture.Arm64);

        Assert.HasCount(2, insns);
        TestAssert.All(insns, i => Assert.AreEqual(".word", i.Mnemonic));
        TestAssert.All(insns, i => Assert.AreEqual(4, i.Length));
        Assert.AreEqual("0x02000002", insns[0].OperandText);
        Assert.AreEqual(0x4004UL, insns[1].Address);
    }

    /// <summary>Verifies a truncated arm64 tail falls to a byte rather than reading past the end.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Disassemble_Arm64TruncatedTail_FallsToByte()
    {
        // 0x02000002 is an unallocated word (bits[28:25]=0001); the trailing 0xAA is a truncated tail.
        byte[] code = [0x02, 0x00, 0x00, 0x02, 0xAA];
        var insns = NativeDisassembler.Disassemble(code, 0, NativeArchitecture.Arm64);

        Assert.HasCount(2, insns);
        Assert.AreEqual(".word", insns[0].Mnemonic);
        Assert.AreEqual(".byte", insns[1].Mnemonic);
        Assert.AreEqual(1, insns[1].Length);
    }

    /// <summary>Verifies empty input yields no instructions.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Disassemble_Empty_ReturnsEmpty()
    {
        Assert.IsEmpty(NativeDisassembler.Disassemble([], 0, NativeArchitecture.X64));
    }

    /// <summary>
    /// Verifies the text render stamps a 1-based <see cref="NativeInstruction.DisplayLine"/> and a
    /// <see cref="NativeLineLayout"/> whose spans slice the rendered line back to the exact mnemonic
    /// and operand text — the invariant the decoration providers depend on.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void DisassembleWithText_StampsDisplayLineAndAccurateLayoutSpans()
    {
        byte[] code = [0x90, 0xCC];
        var (text, insns, headerCount) = NativeDisassembler.DisassembleWithText(
            code, 0x2000, NativeArchitecture.X64, header: "// Function: X\n// Size: 2");

        Assert.AreEqual(3, headerCount); // two header lines + a blank
        var lines = text.Split('\n');
        Assert.AreEqual("// Function: X", lines[0]);
        Assert.AreEqual("", lines[2]);

        TestAssert.All(insns, i => Assert.IsNotNull(i.DisplayLine));
        foreach (var insn in insns)
        {
            var line = lines[insn.DisplayLine!.Value - 1];
            var layout = insn.Layout!.Value;
            Assert.AreEqual(insn.Mnemonic, line.Substring(layout.MnemonicStart, layout.MnemonicLength));
            if (layout.OperandsStart >= 0)
                Assert.AreEqual(insn.OperandText, line.Substring(layout.OperandsStart, layout.OperandsLength));
            Assert.StartsWith($"0x{insn.Address:x}:", line);
        }
    }

    /// <summary>Verifies the composer emits the exact bytes for the primitive and encoding helpers.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void CodeBlob_ComposesExactBytes()
    {
        var blob = new CodeBlob().Rex(w: true).U8(0x01).ModRM(3, 0, 1);
        Assert.AreSequenceEqual(new byte[] { 0x48, 0x01, 0xC1 }, blob.ToArray());

        // 3-byte VEX for map 0F38, W0, no vvvv, L=0, pp=66 → C4 E2 79 <op>
        var vex = new CodeBlob().Vex3(r: false, x: false, b: false, map: 2, w: false, vvvv: 0, l: 0, pp: 1).U8(0xDC);
        var bytes = vex.ToArray();
        Assert.AreEqual(0xC4, bytes[0]);
        Assert.AreEqual(0xE2, bytes[1]); // ~R ~X ~B mmmmm = 111 00010
        Assert.AreEqual(0x79, bytes[2]); // W ~vvvv L pp = 0 1111 0 01

        Assert.AreSequenceEqual(new byte[] { 0x00, 0x01, 0x02, 0x03 }, new CodeBlob().Word(0x03020100).ToArray());
    }
}
