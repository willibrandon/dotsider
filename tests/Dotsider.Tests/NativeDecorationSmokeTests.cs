using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;
using Dotsider.Views;
using Hex1b.Documents;

namespace Dotsider.Tests;

/// <summary>
/// Smoke test that the native syntax and navigation decoration providers never throw or emit an
/// out-of-range span over real disassembled functions — the per-frame render path in native mode.
/// </summary>
[TestClass]
public class NativeDecorationSmokeTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>Runs both providers over every managed function's real listing and asserts no throw / in-range spans.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Providers_OverRealFunctions_NeverThrowOrExceedLine()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null || !File.Exists(Samples.NativeAotConsoleExe),
            "NativeAOT publish did not run on this leg.");

        using var analyzer = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);
        var symbols = analyzer.NativeSymbols!;
        var syntax = new NativeSyntaxDecorationProvider();
        var nav = new NativeNavigationDecorationProvider();

        var checkedFns = 0;
        foreach (var s in symbols.Symbols.Where(s => s.Kind == NativeSymbolKind.Function && s.FileOffset is not null && s.Size > 0).Take(400))
        {
            var result = NativeDisassembler.DisassembleSymbol(analyzer, s);
            if (result is null) continue;
            var doc = new Hex1bDocument(result.Value.Text);
            syntax.Instructions = result.Value.Instructions;
            nav.Instructions = result.Value.Instructions;

            var spans = syntax.GetDecorations(1, doc.LineCount, doc);
            spans = [.. spans, .. nav.GetDecorations(1, doc.LineCount, doc)];
            foreach (var span in spans)
            {
                var lineLen = doc.GetLineText(span.Start.Line).Length;
                Assert.IsLessThanOrEqualTo(lineLen + 1, span.End.Column, $"{s.ManagedName ?? s.Name} line {span.Start.Line}: end col {span.End.Column} > line length {lineLen}");
            }

            checkedFns++;
        }

        Assert.IsGreaterThan(0, checkedFns);
    }
}
