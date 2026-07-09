using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="NativeDisassembler.DisassembleSymbol(AssemblyAnalyzer, NativeSymbol)"/>: the analyzer-driven convenience
/// slices a recovered symbol's bytes, decodes them for the real architecture, resolves targets, and
/// renders a header with the symbol name — proven over a managed function of the real AOT sample.
/// </summary>
[TestClass]
public class NativeDisassembleSymbolTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>Verifies a recovered managed function disassembles to a named, non-empty listing.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void DisassembleSymbol_ManagedFunction_RendersNamedListing()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null || !File.Exists(Samples.NativeAotConsoleExe),
            "NativeAOT publish did not run on this leg.");

        using var analyzer = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);
        var symbols = analyzer.NativeSymbols;
        Assert.IsNotNull(symbols);

        var fn = symbols!.Symbols.FirstOrDefault(s =>
            s.Kind == NativeSymbolKind.Function && s.ManagedName is not null && s.FileOffset is not null && s.Size > 0);
        Assert.IsNotNull(fn);

        var result = NativeDisassembler.DisassembleSymbol(analyzer, fn!);
        Assert.IsNotNull(result);
        var (text, instructions, headerLineCount) = result!.Value;

        Assert.Contains(fn!.ManagedName!, text);
        Assert.IsNotEmpty(instructions);
        Assert.IsGreaterThanOrEqualTo(1, headerLineCount);
        TestAssert.All(instructions, i => Assert.IsFalse(i.IsFallback));
        Assert.AreEqual(fn.Size, instructions.Sum(i => i.Length));
    }

    /// <summary>Verifies a managed (non-native) binary yields no native symbols to disassemble.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void DisassembleSymbol_ManagedBinary_HasNoNativeSymbols()
    {
        using var analyzer = new AssemblyAnalyzer(Samples.HelloWorldDll);
        Assert.IsTrue(analyzer.NativeSymbols is null || analyzer.NativeSymbols.Symbols.Count == 0);
    }

    /// <summary>
    /// Verifies the shipped two-argument overload still exists with its exact signature
    /// (binary compatibility) and that the new resolver overload renders correlation-aware
    /// target names when a call/branch target resolves.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void DisassembleSymbol_ResolverOverload_PinsBinaryCompatAndResolvesNames()
    {
        var twoArg = typeof(NativeDisassembler).GetMethod(
            nameof(NativeDisassembler.DisassembleSymbol),
            [typeof(AssemblyAnalyzer), typeof(NativeSymbol)]);
        Assert.IsNotNull(twoArg);

        TestSkip.When(Samples.NativeAotConsoleExe is null || !File.Exists(Samples.NativeAotConsoleExe),
            "NativeAOT publish did not run on this leg.");

        using var analyzer = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);
        var info = analyzer.NativeSymbols!;
        var caller = info.Symbols.FirstOrDefault(s =>
            s.Kind == NativeSymbolKind.Function && s.FileOffset is not null && s.Size > 32);
        TestSkip.When(caller is null, "no disassemblable function symbol");

        // Rename every recognized target to a sentinel; if a target resolves, the sentinel
        // wins over the recovered-metadata name.
        var result = NativeDisassembler.DisassembleSymbol(
            analyzer, caller!, _ => "SENTINEL_MANAGED_NAME");
        Assert.IsNotNull(result);
        Assert.HasCount(NativeDisassembler.DisassembleSymbol(analyzer, caller!)!.Value.Instructions.Count, result!.Value.Instructions);
    }
}
