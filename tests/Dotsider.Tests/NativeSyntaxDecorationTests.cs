using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;
using Dotsider.Views;
using Hex1b.Documents;

namespace Dotsider.Tests;

/// <summary>
/// Tests that the native syntax highlighter decorates the disassembly listing from the decoded
/// instructions' <see cref="NativeLineLayout"/> spans (address, mnemonic, operands, target) rather
/// than by re-parsing the rendered text, and stays inert until fed instructions.
/// </summary>
public class NativeSyntaxDecorationTests
{
    [Fact(Timeout = 30_000)]
    public void GetDecorations_NativeListing_ColorsMnemonicAndAddress()
    {
        // mov rbp, rsp ; sub rsp, 0x20 ; call rel32
        byte[] code = [0x48, 0x89, 0xE5, 0x48, 0x83, 0xEC, 0x20, 0xE8, 0x00, 0x00, 0x00, 0x00];
        var (text, instructions, _) = NativeDisassembler.DisassembleWithText(code, 0x1000, NativeArchitecture.X64);

        var doc = new Hex1bDocument(text);
        var provider = new NativeSyntaxDecorationProvider { Instructions = instructions };

        var spans = provider.GetDecorations(1, doc.LineCount, doc);

        Assert.NotEmpty(spans);
        // Every instruction line contributes at least an address and a mnemonic span.
        Assert.True(spans.Count >= instructions.Count(i => i.DisplayLine is not null) * 2);
    }

    [Fact(Timeout = 30_000)]
    public void GetDecorations_NoInstructions_IsInert()
    {
        var doc = new Hex1bDocument("0x1000: 90  nop");
        var provider = new NativeSyntaxDecorationProvider();
        Assert.Empty(provider.GetDecorations(1, doc.LineCount, doc));
    }
}
