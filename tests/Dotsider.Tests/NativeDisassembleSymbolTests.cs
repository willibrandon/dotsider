using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="NativeDisassembler.DisassembleSymbol"/>: the analyzer-driven convenience
/// slices a recovered symbol's bytes, decodes them for the real architecture, resolves targets, and
/// renders a header with the symbol name — proven over a managed function of the real AOT sample.
/// </summary>
[Collection("SampleAssemblies")]
public class NativeDisassembleSymbolTests(SampleAssemblyFixture samples)
{
    /// <summary>Verifies a recovered managed function disassembles to a named, non-empty listing.</summary>
    [Fact(Timeout = 120_000)]
    public void DisassembleSymbol_ManagedFunction_RendersNamedListing()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null || !File.Exists(samples.NativeAotConsoleExe),
            "NativeAOT publish did not run on this leg.");

        using var analyzer = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);
        var symbols = analyzer.NativeSymbols;
        Assert.NotNull(symbols);

        var fn = symbols!.Symbols.FirstOrDefault(s =>
            s.Kind == NativeSymbolKind.Function && s.ManagedName is not null && s.FileOffset is not null && s.Size > 0);
        Assert.NotNull(fn);

        var result = NativeDisassembler.DisassembleSymbol(analyzer, fn!);
        Assert.NotNull(result);
        var (text, instructions, headerLineCount) = result!.Value;

        Assert.Contains(fn!.ManagedName!, text);
        Assert.NotEmpty(instructions);
        Assert.True(headerLineCount >= 1);
        Assert.All(instructions, i => Assert.False(i.IsFallback));
        Assert.Equal(fn.Size, instructions.Sum(i => i.Length));
    }

    /// <summary>Verifies a managed (non-native) binary yields no native symbols to disassemble.</summary>
    [Fact(Timeout = 30_000)]
    public void DisassembleSymbol_ManagedBinary_HasNoNativeSymbols()
    {
        using var analyzer = new AssemblyAnalyzer(samples.HelloWorldDll);
        Assert.True(analyzer.NativeSymbols is null || analyzer.NativeSymbols.Symbols.Count == 0);
    }
}
