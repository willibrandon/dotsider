using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Verifies non-host native decoders against real crossgen2 ReadyToRun images.
/// The shared fixture publishes cross-RID sample assemblies when the SDK has the required packs.
/// These tests catch architecture routing and padding mistakes that synthetic byte tests cannot.
/// </summary>
[Collection("SampleAssemblies")]
public class ReadyToRunArchitectureDecoderTests(SampleAssemblyFixture samples)
{
    private const string SkipReason = "ReadyToRun cross-RID publish did not run on this leg.";

    /// <summary>
    /// Resolves a real x86 ReadyToRun method and decodes its native body.
    /// The image is produced by crossgen2, not by hand-authored bytes.
    /// Fallback instructions would mean a valid method range was not decoded.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void X86_RealReadyToRunMethod_DecodesWithoutFallback()
    {
        Assert.SkipWhen(samples.ReadyToRunConsoleX86Dll is null, SkipReason);

        using var analyzer = new AssemblyAnalyzer(samples.ReadyToRunConsoleX86Dll!);
        var report = ResolveGreeterName(analyzer);

        Assert.Equal(NativeArchitecture.X86, analyzer.ReadyToRunInfo!.Architecture);
        Assert.Equal(ReadyToRunNativeAvailability.Precompiled, report.Availability);
        Assert.NotNull(report.NativeInstructions);
        Assert.Contains(report.NativeInstructions!, i => i.Mnemonic == "mov");
        Assert.Contains(report.NativeInstructions!, i => i.Mnemonic == "ret");
        Assert.DoesNotContain(report.NativeInstructions!, i => i.IsFallback);
    }

    /// <summary>
    /// Verifies every file-backed x86 ReadyToRun range decodes length-exact with no fallback.
    /// The range list comes from the real runtime-function table.
    /// This catches emitted patterns outside the one-method smoke path.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void X86_RealReadyToRunRanges_AreLengthExactWithoutFallback()
    {
        Assert.SkipWhen(samples.ReadyToRunConsoleX86Dll is null, SkipReason);

        using var analyzer = new AssemblyAnalyzer(samples.ReadyToRunConsoleX86Dll!);

        AssertRealReadyToRunRangesDecode(analyzer, NativeArchitecture.X86);
    }

    /// <summary>
    /// Resolves a real ARM32 ReadyToRun method and decodes its Thumb body.
    /// The image is produced by crossgen2, and the R2R unwind length excludes later trap padding.
    /// Fallback instructions would mean valid Thumb code in the method body was not modeled.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Arm32_RealReadyToRunMethod_DecodesWithoutFallback()
    {
        Assert.SkipWhen(samples.ReadyToRunConsoleArm32Dll is null, SkipReason);

        using var analyzer = new AssemblyAnalyzer(samples.ReadyToRunConsoleArm32Dll!);
        var report = ResolveGreeterName(analyzer);

        Assert.Equal(NativeArchitecture.Arm32, analyzer.ReadyToRunInfo!.Architecture);
        Assert.Equal(ReadyToRunNativeAvailability.Precompiled, report.Availability);
        Assert.NotNull(report.NativeInstructions);
        Assert.Contains(report.NativeInstructions!, i => i.Mnemonic == "push");
        Assert.Contains(report.NativeInstructions!, i => i.Mnemonic == "ldr");
        Assert.Contains(report.NativeInstructions!, i => i.Mnemonic == "pop");
        Assert.DoesNotContain(report.NativeInstructions!, i => i.IsFallback);
    }

    /// <summary>
    /// Verifies every file-backed ARM32 ReadyToRun range decodes length-exact with no fallback.
    /// The range list comes from the real runtime-function table.
    /// This catches mixed-width Thumb patterns outside the one-method smoke path.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Arm32_RealReadyToRunRanges_AreLengthExactWithoutFallback()
    {
        Assert.SkipWhen(samples.ReadyToRunConsoleArm32Dll is null, SkipReason);

        using var analyzer = new AssemblyAnalyzer(samples.ReadyToRunConsoleArm32Dll!);

        AssertRealReadyToRunRangesDecode(analyzer, NativeArchitecture.Arm32);
    }

    /// <summary>
    /// Resolves a real RISC-V64 ReadyToRun method and decodes its native body when SDK packs exist.
    /// Public SDK feeds do not always ship this RID, so the fixture path is optional.
    /// When present, the image is crossgen2 output rather than a hand-authored byte fixture.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RiscV64_RealReadyToRunMethod_DecodesWithoutFallback()
    {
        Assert.SkipWhen(samples.ReadyToRunConsoleRiscV64Dll is null, SkipReason);

        using var analyzer = new AssemblyAnalyzer(samples.ReadyToRunConsoleRiscV64Dll!);
        var report = ResolveGreeterName(analyzer);

        Assert.Equal(NativeArchitecture.RiscV64, analyzer.ReadyToRunInfo!.Architecture);
        Assert.Equal(ReadyToRunNativeAvailability.Precompiled, report.Availability);
        Assert.NotNull(report.NativeInstructions);
        Assert.NotEmpty(report.NativeInstructions!);
        Assert.DoesNotContain(report.NativeInstructions!, i => i.IsFallback);
    }

    /// <summary>
    /// Verifies every file-backed RISC-V64 ReadyToRun range decodes length-exact with no fallback.
    /// The range list comes from a real runtime-function table when the SDK can publish the RID.
    /// The committed oracle fixtures cover this decoder on SDKs that lack the RID packs.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RiscV64_RealReadyToRunRanges_AreLengthExactWithoutFallback()
    {
        Assert.SkipWhen(samples.ReadyToRunConsoleRiscV64Dll is null, SkipReason);

        using var analyzer = new AssemblyAnalyzer(samples.ReadyToRunConsoleRiscV64Dll!);

        AssertRealReadyToRunRangesDecode(analyzer, NativeArchitecture.RiscV64);
    }

    /// <summary>
    /// Resolves a real LoongArch64 ReadyToRun method and decodes its native body when SDK packs exist.
    /// Public SDK feeds do not always ship this RID, so the fixture path is optional.
    /// When present, the image is crossgen2 output rather than a hand-authored byte fixture.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void LoongArch64_RealReadyToRunMethod_DecodesWithoutFallback()
    {
        Assert.SkipWhen(samples.ReadyToRunConsoleLoongArch64Dll is null, SkipReason);

        using var analyzer = new AssemblyAnalyzer(samples.ReadyToRunConsoleLoongArch64Dll!);
        var report = ResolveGreeterName(analyzer);

        Assert.Equal(NativeArchitecture.LoongArch64, analyzer.ReadyToRunInfo!.Architecture);
        Assert.Equal(ReadyToRunNativeAvailability.Precompiled, report.Availability);
        Assert.NotNull(report.NativeInstructions);
        Assert.NotEmpty(report.NativeInstructions!);
        Assert.DoesNotContain(report.NativeInstructions!, i => i.IsFallback);
    }

    /// <summary>
    /// Verifies every file-backed LoongArch64 ReadyToRun range decodes length-exact with no fallback.
    /// The range list comes from a real runtime-function table when the SDK can publish the RID.
    /// The committed oracle fixtures cover this decoder on SDKs that lack the RID packs.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void LoongArch64_RealReadyToRunRanges_AreLengthExactWithoutFallback()
    {
        Assert.SkipWhen(samples.ReadyToRunConsoleLoongArch64Dll is null, SkipReason);

        using var analyzer = new AssemblyAnalyzer(samples.ReadyToRunConsoleLoongArch64Dll!);

        AssertRealReadyToRunRangesDecode(analyzer, NativeArchitecture.LoongArch64);
    }

    private static ReadyToRunMethodReport ResolveGreeterName(AssemblyAnalyzer analyzer)
    {
        var result = ReadyToRunCorrelationQuery.Resolve(
            analyzer, "Greeter.get_Name", TestContext.Current.CancellationToken);

        Assert.Equal(ReadyToRunQueryOutcome.Resolved, result.Outcome);
        Assert.NotNull(result.Report);
        return result.Report!;
    }

    private static void AssertRealReadyToRunRangesDecode(AssemblyAnalyzer analyzer, NativeArchitecture expected)
    {
        Assert.Equal(expected, analyzer.ReadyToRunInfo!.Architecture);

        var failures = new List<string>();
        foreach (var method in analyzer.ReadyToRunMethods)
        {
            foreach (var range in method.CodeRanges)
            {
                if (range.FileOffset is not { } fileOffset || range.Size <= 0)
                    continue;

                var code = analyzer.RawBytes.Span.Slice(fileOffset, (int)range.Size);
                var instructions = NativeDisassembler.Disassemble(code, range.VirtualAddress, expected);
                var length = instructions.Sum(i => i.Length);
                var fallback = instructions.FirstOrDefault(i => i.IsFallback);

                if (length != range.Size || fallback is not null)
                {
                    failures.Add(
                        $"{method.DeclaringType}.{method.Name} {range.Kind} "
                        + $"0x{range.VirtualAddress:x}+{range.Size}: "
                        + $"length={length}, fallback={fallback?.Mnemonic} {fallback?.OperandText}");
                }
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures.Take(20)));
    }
}
