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

        // The sample's own managed methods decode with no desync and zero fallback.
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

        // The intrinsic families are compiled into the binary whether or not the platform's symbol
        // table names them — Release AOT strips symbols on Linux/macOS, so the guarded vector methods
        // are present as unnamed function symbols. Scan every function (not just the named managed
        // ones) for a cleanly-decoded vector-register operand, so this asserts the decoder handles
        // real vectorized AOT output on every leg rather than depending on cross-platform naming.
        var sawVector = false;
        var scanned = 0;
        foreach (var code in ExecutableFunctions(analyzer, symbols!))
        {
            var insns = NativeDisassembler.Disassemble(code, 0, arch);
            if (insns.Any(i => i.IsFallback)) continue; // skip desynced (embedded jump-table) functions
            scanned++;
            if (insns.Any(i => i.Category is NativeInstructionCategory.Vector or NativeInstructionCategory.Float
                && i.Operands.Any(o => o.Register is { } r
                    && (r.StartsWith("xmm") || r.StartsWith("ymm") || r.StartsWith("zmm") || r.StartsWith('v') || r.StartsWith('z')))))
            {
                sawVector = true;
                break;
            }
        }

        Assert.True(sawVector, $"no vector-register operand across {scanned} cleanly-decoded functions ({arch})");
    }

    private static string Hex(NativeInstruction? insn) =>
        insn is null ? "—" : string.Join(" ", insn.Bytes.Select(b => b.ToString("x2")));

    /// <summary>Every executable function's bytes (named or not), for scans that must not depend on symbol naming.</summary>
    private static IEnumerable<byte[]> ExecutableFunctions(AssemblyAnalyzer analyzer, NativeSymbolInfo symbols)
    {
        var raw = analyzer.RawBytes;
        foreach (var s in symbols.Symbols)
        {
            if (s.Kind is not (NativeSymbolKind.Function or NativeSymbolKind.Boundary)
                || s.FileOffset is not { } fo || s.Size <= 0 || fo + s.Size > raw.Length)
                continue;
            yield return raw.Span.Slice((int)fo, (int)s.Size).ToArray();
        }
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
