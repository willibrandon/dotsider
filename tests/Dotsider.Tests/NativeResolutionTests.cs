using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the facade's target resolution: RIP-relative data addresses computed off the next
/// instruction, intra-window branch targets synthesized as loc_ labels, and resolved symbols
/// rendered as Name or Name+0x.. — the naming that drives go-to-definition and the ; annotations.
/// </summary>
public class NativeResolutionTests
{
    /// <summary>Verifies a RIP-relative reference resolves to next-IP + disp as a Data target.</summary>
    [Fact(Timeout = 30_000)]
    public void RipRelative_ComputesAbsoluteDataTarget()
    {
        // lea rax, [rip+0x0] at 0x1000, length 7 → target 0x1007.
        byte[] code = [0x48, 0x8D, 0x05, 0x00, 0x00, 0x00, 0x00];
        var insn = NativeDisassembler.Disassemble(code, 0x1000, NativeArchitecture.X64)[0];

        Assert.Equal(0x1007UL, insn.TargetAddress);
        Assert.Equal(NativeTargetKind.Data, insn.TargetKind);
    }

    /// <summary>Verifies an intra-window branch target becomes a synthesized local label.</summary>
    [Fact(Timeout = 30_000)]
    public void IntraWindowBranch_BecomesLocalLabel()
    {
        // jmp +2 (0xEB 0x00 lands on the next instruction 0x1002), then two nops.
        byte[] code = [0xEB, 0x00, 0x90, 0x90];
        var insns = NativeDisassembler.Disassemble(code, 0x1000, NativeArchitecture.X64);

        Assert.Equal(0x1002UL, insns[0].TargetAddress);
        Assert.Equal(NativeTargetKind.LocalLabel, insns[0].TargetKind);
        Assert.Equal("loc_1002", insns[0].TargetName);
    }

    /// <summary>Verifies the loc_ label line is emitted above its target in the rendered listing.</summary>
    [Fact(Timeout = 30_000)]
    public void DisassembleWithText_EmitsLocalLabelLine()
    {
        byte[] code = [0xEB, 0x00, 0x90, 0x90];
        var (text, _, _) = NativeDisassembler.DisassembleWithText(code, 0x1000, NativeArchitecture.X64);
        Assert.Contains("loc_1002:", text);
    }

    /// <summary>Verifies a call landing inside a resolved symbol renders as Name+0x.. and exactly as Name.</summary>
    [Fact(Timeout = 30_000)]
    public void ResolvedCall_RendersNameAndOffset()
    {
        // call rel32 to 0x2000, then call rel32 to 0x2005.
        byte[] code = [0xE8, 0xFB, 0x0F, 0x00, 0x00, 0xE8, 0xFB, 0x0F, 0x00, 0x00];

        static bool Resolver(ulong va, out NativeSymbolRef sym)
        {
            if (va >= 0x2000 && va < 0x2100)
            {
                sym = new NativeSymbolRef(0x2000, "Foo", NativeSymbolKind.Function, (long)(va - 0x2000));
                return true;
            }

            sym = default;
            return false;
        }

        var insns = NativeDisassembler.Disassemble(code, 0x1000, NativeArchitecture.X64, Resolver);
        Assert.Equal("Foo", insns[0].TargetName);
        Assert.Equal("Foo+0x5", insns[1].TargetName);
        Assert.Equal(NativeTargetKind.Function, insns[0].TargetKind);
    }
}
