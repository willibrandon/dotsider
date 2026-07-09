using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// End-to-end disassembler fixtures over real Native AOT output: the user's own managed methods
/// must decode with no desync (summed instruction lengths equal the symbol size) and zero
/// <c>.byte</c>/<c>.word</c> fallback — the "zero fallback on real code" bar. The HardwareIntrinsics
/// sample additionally proves the vectorized/intrinsic surface decodes (a vector-register operand
/// appears). Runtime helpers that embed jump tables are excluded, since a linear sweep cannot avoid
/// their inline data. These gate with <see cref="TestSkip.When"/> when the AOT publish did not run
/// (no toolchain on the leg).
/// </summary>
[TestClass]
public class NativeDisasmAotFixtureTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private static NativeArchitecture ArchOf(AssemblyAnalyzer a) => a.Architecture.ToUpperInvariant() switch
    {
        "X64" => NativeArchitecture.X64,
        "ARM64" => NativeArchitecture.Arm64,
        _ => NativeArchitecture.Unknown,
    };

    /// <summary>Verifies the sample's own managed methods decode with no desync and zero fallback.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeAotConsole_ManagedFunctions_DecodeCleanly()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null || !File.Exists(Samples.NativeAotConsoleExe),
            "NativeAOT publish did not run on this leg.");

        using var analyzer = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);
        var arch = ArchOf(analyzer);
        Assert.AreNotEqual(NativeArchitecture.Unknown, arch);
        var symbols = analyzer.NativeSymbols;
        Assert.IsNotNull(symbols);

        var checkedFns = 0;
        foreach (var (code, name) in ManagedFunctions(analyzer, symbols!))
        {
            var insns = NativeDisassembler.Disassemble(code, 0, arch);
            Assert.AreEqual(code.Length, insns.Sum(i => i.Length));
            var fallback = insns.FirstOrDefault(i => i.IsFallback);
            Assert.IsNull(fallback, $"{name} @+0x{fallback?.Address:x}: unexpected fallback {fallback?.Mnemonic} {fallback?.OperandText} (bytes {Hex(fallback)})");
            checkedFns++;
        }

        Assert.IsGreaterThan(0, checkedFns, "no managed functions were available to check");
    }

    /// <summary>Verifies the intrinsic sample decodes cleanly and its vectorized code produces vector operands.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void HardwareIntrinsics_ManagedFunctions_DecodeAndVectorize()
    {
        TestSkip.When(Samples.HardwareIntrinsicsExe is null || !File.Exists(Samples.HardwareIntrinsicsExe),
            "HardwareIntrinsics publish did not run on this leg.");

        using var analyzer = new AssemblyAnalyzer(Samples.HardwareIntrinsicsExe!);
        var arch = ArchOf(analyzer);
        var symbols = analyzer.NativeSymbols;
        Assert.IsNotNull(symbols);

        // Scope to the sample's own intrinsic methods (the X64.* families on x64, the Arm.* families
        // on arm64), identified by their ILC-mangled symbol name. These are pure vector/scalar code:
        // unlike framework helpers a linear sweep cannot avoid the inline data literals of, they must
        // decode with no desync and zero fallback. The ILC demangler does not join app-method names,
        // so match on the raw symbol name rather than ManagedName, and don't depend on which families
        // a leg's ISA emits — whichever architecture's families are present are the ones checked.
        var raw = analyzer.RawBytes;
        var intrinsics = symbols!.Symbols
            .Where(s => s.Kind == NativeSymbolKind.Function && s.FileOffset is { } fo && s.Size > 0
                && fo + s.Size <= raw.Length
                && (s.Name.Contains("HardwareIntrinsics_X64") || s.Name.Contains("HardwareIntrinsics_Arm")))
            .ToList();
        Assert.IsNotEmpty(intrinsics); // the intrinsic families must be present as function symbols

        var sawVector = false;
        var funcDetails = new List<string>();
        foreach (var s in intrinsics)
        {
            var code = raw.Span.Slice((int)s.FileOffset!.Value, (int)s.Size).ToArray();
            var insns = NativeDisassembler.Disassemble(code, 0, arch);
            Assert.AreEqual(code.Length, insns.Sum(i => i.Length));
            var fallback = insns.FirstOrDefault(i => i.IsFallback);
            Assert.IsNull(fallback, $"{s.Name} @+0x{fallback?.Address:x}: unexpected fallback {fallback?.Mnemonic} {fallback?.OperandText} (bytes {Hex(fallback)})");

            sawVector |= insns.Any(i => i.Category is NativeInstructionCategory.Vector or NativeInstructionCategory.Float
                && i.Operands.Any(o => o.Register is { } r
                    && (r.StartsWith("xmm") || r.StartsWith("ymm") || r.StartsWith("zmm") || r.StartsWith('v') || r.StartsWith('z'))));
            if (funcDetails.Count < 24)
                funcDetails.Add($"{s.Name}:{{{string.Join(",", insns.Select(i => i.Mnemonic).Distinct())}}}");
        }

        Assert.IsTrue(sawVector, $"no vector-register operand across the intrinsic methods; funcs=[{string.Join(" | ", funcDetails)}]");
    }

    private static string Hex(NativeInstruction? insn) =>
        insn is null ? "—" : string.Join(" ", insn.Bytes.Select(b => b.ToString("x2")));

    /// <summary>The reader populates the real architecture and a source map, so the disassembler can name the slice and annotate file:line.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeAotConsole_Architecture_And_SourceMap_Populated()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null || !File.Exists(Samples.NativeAotConsoleExe),
            "NativeAOT publish did not run on this leg.");

        using var analyzer = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);
        var info = analyzer.NativeSymbols;
        Assert.IsNotNull(info);

        // The architecture reads from the image header, so it is always the real slice arch.
        Assert.AreNotEqual(NativeArchitecture.Unknown, info!.Architecture);

        // Where the sidecar carries line data, the aggregated map must resolve it; a leg whose symbols
        // are stripped of line data has no map, which is correct rather than a failure.
        var fn = info.Symbols.FirstOrDefault(s =>
            s.Kind == NativeSymbolKind.Function && s.SourceFile is not null && s.Line is > 0);
        if (fn is not null)
        {
            Assert.IsNotNull(info.SourceMap);
            Assert.IsTrue(info.SourceMap!.TryGetLine(fn.VirtualAddress, out _, out var line) && line > 0);
        }
    }

    /// <summary>A truncated instruction tail renders as .byte so summed lengths still equal the window — nothing is dropped.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Disassemble_TruncatedTail_SumsToWindow()
    {
        // A 3-byte x64 window where the last instruction (a 4-byte lea) is truncated at the boundary.
        byte[] code = [0x90, 0x8D, 0x05]; // nop, then a truncated lea eax,[rip+...]
        var insns = NativeDisassembler.Disassemble(code, 0x1000, NativeArchitecture.X64);
        Assert.AreEqual(code.Length, insns.Sum(i => i.Length));
        Assert.Contains(i => i.IsFallback, insns);
    }

    private static IEnumerable<(byte[] Code, string Name)> ManagedFunctions(AssemblyAnalyzer analyzer, NativeSymbolInfo symbols)
    {
        var raw = analyzer.RawBytes;
        foreach (var s in symbols.Symbols)
        {
            if (s.Kind != NativeSymbolKind.Function || s.ManagedName is null || s.FileOffset is not { } fo || s.Size <= 0)
                continue;
            if (fo + s.Size > raw.Length) continue;
            yield return (raw.Span.Slice((int)fo, (int)s.Size).ToArray(), s.ManagedName);
        }
    }
}
