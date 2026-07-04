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
/// their inline data. These gate with <see cref="Assert.SkipWhen"/> when the AOT publish did not run
/// (no toolchain on the leg).
/// </summary>
[Collection("SampleAssemblies")]
public class NativeDisasmAotFixtureTests(SampleAssemblyFixture samples)
{
    private static NativeArchitecture ArchOf(AssemblyAnalyzer a) => a.Architecture.ToUpperInvariant() switch
    {
        "X64" => NativeArchitecture.X64,
        "ARM64" => NativeArchitecture.Arm64,
        _ => NativeArchitecture.Unknown,
    };

    /// <summary>Verifies the sample's own managed methods decode with no desync and zero fallback.</summary>
    [Fact(Timeout = 120_000)]
    public void NativeAotConsole_ManagedFunctions_DecodeCleanly()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null || !File.Exists(samples.NativeAotConsoleExe),
            "NativeAOT publish did not run on this leg.");

        using var analyzer = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);
        var arch = ArchOf(analyzer);
        Assert.NotEqual(NativeArchitecture.Unknown, arch);
        var symbols = analyzer.NativeSymbols;
        Assert.NotNull(symbols);

        var checkedFns = 0;
        foreach (var (code, name) in ManagedFunctions(analyzer, symbols!))
        {
            var insns = NativeDisassembler.Disassemble(code, 0, arch);
            Assert.Equal(code.Length, insns.Sum(i => i.Length));
            var fallback = insns.FirstOrDefault(i => i.IsFallback);
            Assert.True(fallback is null, $"{name} @+0x{fallback?.Address:x}: unexpected fallback {fallback?.Mnemonic} {fallback?.OperandText} (bytes {Hex(fallback)})");
            checkedFns++;
        }

        Assert.True(checkedFns > 0, "no managed functions were available to check");
    }

    /// <summary>Verifies the intrinsic sample decodes cleanly and its vectorized code produces vector operands.</summary>
    [Fact(Timeout = 120_000)]
    public void HardwareIntrinsics_ManagedFunctions_DecodeAndVectorize()
    {
        Assert.SkipWhen(samples.HardwareIntrinsicsExe is null || !File.Exists(samples.HardwareIntrinsicsExe),
            "HardwareIntrinsics publish did not run on this leg.");

        using var analyzer = new AssemblyAnalyzer(samples.HardwareIntrinsicsExe!);
        var arch = ArchOf(analyzer);
        var symbols = analyzer.NativeSymbols;
        Assert.NotNull(symbols);

        var sawVector = false;
        var checkedFns = 0;
        var totalInsns = 0;
        var funcDetails = new List<string>();
        foreach (var (code, name) in ManagedFunctions(analyzer, symbols!))
        {
            var insns = NativeDisassembler.Disassemble(code, 0, arch);
            Assert.Equal(code.Length, insns.Sum(i => i.Length));
            var fallback = insns.FirstOrDefault(i => i.IsFallback);
            Assert.True(fallback is null, $"{name} @+0x{fallback?.Address:x}: unexpected fallback {fallback?.Mnemonic} {fallback?.OperandText} (bytes {Hex(fallback)})");

            checkedFns++;
            totalInsns += insns.Count;
            sawVector |= insns.Any(i => i.Category is NativeInstructionCategory.Vector or NativeInstructionCategory.Float
                && i.Operands.Any(o => o.Register is { } r
                    && (r.StartsWith("xmm") || r.StartsWith("ymm") || r.StartsWith("zmm") || r.StartsWith('v') || r.StartsWith('z'))));

            // Per-function distinct mnemonics — so a failure shows whether the intrinsic method is
            // present at all, and whether it decoded to SIMD (a categorization bug) or to scalar/no
            // vector code (a build/codegen issue).
            if (funcDetails.Count < 24)
                funcDetails.Add($"{name}:{{{string.Join(",", insns.Select(i => i.Mnemonic).Distinct())}}}");
        }

        Assert.True(sawVector,
            $"no vector-register operand across {checkedFns} functions / {totalInsns} instructions; "
            + $"funcs=[{string.Join(" | ", funcDetails)}]");
    }

    private static string Hex(NativeInstruction? insn) =>
        insn is null ? "—" : string.Join(" ", insn.Bytes.Select(b => b.ToString("x2")));

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
