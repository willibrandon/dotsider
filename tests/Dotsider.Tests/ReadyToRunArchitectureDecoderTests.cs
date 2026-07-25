using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Verifies non-host native decoders against real crossgen2 ReadyToRun images.
/// The shared fixture publishes cross-RID sample assemblies when the SDK has the required packs.
/// These tests catch architecture routing and padding mistakes that synthetic byte tests cannot.
/// </summary>
[TestClass]
public class ReadyToRunArchitectureDecoderTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private const string SkipReason = "ReadyToRun cross-RID publish did not run on this leg.";

    /// <summary>
    /// Resolves a real x86 ReadyToRun method and decodes its native body.
    /// The image is produced by crossgen2, not by hand-authored bytes.
    /// Fallback instructions would mean a valid method range was not decoded.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void X86_RealReadyToRunMethod_DecodesWithoutFallback()
    {
        TestSkip.When(Samples.ReadyToRunConsoleX86Dll is null, SkipReason);

        using var analyzer = new AssemblyAnalyzer(Samples.ReadyToRunConsoleX86Dll!);
        var report = ResolveGreeterName(analyzer);

        Assert.AreEqual(NativeArchitecture.X86, analyzer.ReadyToRunInfo!.Architecture);
        Assert.AreEqual(ReadyToRunNativeAvailability.Precompiled, report.Availability);
        Assert.IsNotNull(report.NativeInstructions);
        Assert.Contains(i => i.Mnemonic == "mov", report.NativeInstructions!);
        Assert.Contains(i => i.Mnemonic == "ret", report.NativeInstructions!);
        Assert.DoesNotContain(i => i.IsFallback, report.NativeInstructions!);
    }

    /// <summary>
    /// Verifies every file-backed x86 ReadyToRun range decodes length-exact with no fallback.
    /// The range list comes from the real runtime-function table.
    /// This catches emitted patterns outside the one-method smoke path.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void X86_RealReadyToRunRanges_AreLengthExactWithoutFallback()
    {
        TestSkip.When(Samples.ReadyToRunConsoleX86Dll is null, SkipReason);

        using var analyzer = new AssemblyAnalyzer(Samples.ReadyToRunConsoleX86Dll!);

        AssertRealReadyToRunRangesDecode(analyzer, NativeArchitecture.X86);
    }

    /// <summary>
    /// Resolves a real ARM32 ReadyToRun method and decodes its Thumb body.
    /// The image is produced by crossgen2, and the R2R unwind length excludes later trap padding.
    /// Fallback instructions would mean valid Thumb code in the method body was not modeled.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Arm32_RealReadyToRunMethod_DecodesWithoutFallback()
    {
        TestSkip.When(Samples.ReadyToRunConsoleArm32Dll is null, SkipReason);

        using var analyzer = new AssemblyAnalyzer(Samples.ReadyToRunConsoleArm32Dll!);
        var report = ResolveGreeterName(analyzer);

        Assert.AreEqual(NativeArchitecture.Arm32, analyzer.ReadyToRunInfo!.Architecture);
        Assert.AreEqual(ReadyToRunNativeAvailability.Precompiled, report.Availability);
        Assert.IsNotNull(report.NativeInstructions);
        Assert.Contains(i => i.Mnemonic == "push", report.NativeInstructions!);
        Assert.Contains(i => i.Mnemonic == "ldr", report.NativeInstructions!);
        Assert.Contains(i => i.Mnemonic == "pop", report.NativeInstructions!);
        Assert.DoesNotContain(i => i.IsFallback, report.NativeInstructions!);
    }

    /// <summary>
    /// Verifies every file-backed ARM32 ReadyToRun range decodes length-exact with no fallback.
    /// The range list comes from the real runtime-function table.
    /// This catches mixed-width Thumb patterns outside the one-method smoke path.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Arm32_RealReadyToRunRanges_AreLengthExactWithoutFallback()
    {
        TestSkip.When(Samples.ReadyToRunConsoleArm32Dll is null, SkipReason);

        using var analyzer = new AssemblyAnalyzer(Samples.ReadyToRunConsoleArm32Dll!);

        AssertRealReadyToRunRangesDecode(analyzer, NativeArchitecture.Arm32);
    }

    /// <summary>
    /// Resolves a real RISC-V64 ReadyToRun method and decodes its native body when SDK packs exist.
    /// Public SDK feeds do not always ship this RID, so the fixture path is optional.
    /// When present, the image is crossgen2 output rather than a hand-authored byte fixture.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RiscV64_RealReadyToRunMethod_DecodesWithoutFallback()
    {
        TestSkip.When(Samples.ReadyToRunConsoleRiscV64Dll is null, SkipReason);

        using var analyzer = new AssemblyAnalyzer(Samples.ReadyToRunConsoleRiscV64Dll!);
        var report = ResolveGreeterName(analyzer);

        Assert.AreEqual(NativeArchitecture.RiscV64, analyzer.ReadyToRunInfo!.Architecture);
        Assert.AreEqual(ReadyToRunNativeAvailability.Precompiled, report.Availability);
        Assert.IsNotNull(report.NativeInstructions);
        Assert.IsNotEmpty(report.NativeInstructions!);
        Assert.DoesNotContain(i => i.IsFallback, report.NativeInstructions!);
    }

    /// <summary>
    /// Verifies every file-backed RISC-V64 ReadyToRun range decodes length-exact with no fallback.
    /// The range list comes from a real runtime-function table when the SDK can publish the RID.
    /// The committed oracle fixtures cover this decoder on SDKs that lack the RID packs.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RiscV64_RealReadyToRunRanges_AreLengthExactWithoutFallback()
    {
        TestSkip.When(Samples.ReadyToRunConsoleRiscV64Dll is null, SkipReason);

        using var analyzer = new AssemblyAnalyzer(Samples.ReadyToRunConsoleRiscV64Dll!);

        AssertRealReadyToRunRangesDecode(analyzer, NativeArchitecture.RiscV64);
    }

    /// <summary>
    /// Resolves a real LoongArch64 ReadyToRun method and decodes its native body when SDK packs exist.
    /// Public SDK feeds do not always ship this RID, so the fixture path is optional.
    /// When present, the image is crossgen2 output rather than a hand-authored byte fixture.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void LoongArch64_RealReadyToRunMethod_DecodesWithoutFallback()
    {
        TestSkip.When(Samples.ReadyToRunConsoleLoongArch64Dll is null, SkipReason);

        using var analyzer = new AssemblyAnalyzer(Samples.ReadyToRunConsoleLoongArch64Dll!);
        var report = ResolveGreeterName(analyzer);

        Assert.AreEqual(NativeArchitecture.LoongArch64, analyzer.ReadyToRunInfo!.Architecture);
        Assert.AreEqual(ReadyToRunNativeAvailability.Precompiled, report.Availability);
        Assert.IsNotNull(report.NativeInstructions);
        Assert.IsNotEmpty(report.NativeInstructions!);
        Assert.DoesNotContain(i => i.IsFallback, report.NativeInstructions!);
    }

    /// <summary>
    /// Verifies every file-backed LoongArch64 ReadyToRun range decodes length-exact with no fallback.
    /// The range list comes from a real runtime-function table when the SDK can publish the RID.
    /// The committed oracle fixtures cover this decoder on SDKs that lack the RID packs.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void LoongArch64_RealReadyToRunRanges_AreLengthExactWithoutFallback()
    {
        TestSkip.When(Samples.ReadyToRunConsoleLoongArch64Dll is null, SkipReason);

        using var analyzer = new AssemblyAnalyzer(Samples.ReadyToRunConsoleLoongArch64Dll!);

        AssertRealReadyToRunRangesDecode(analyzer, NativeArchitecture.LoongArch64);
    }

    private static ReadyToRunMethodReport ResolveGreeterName(AssemblyAnalyzer analyzer)
    {
        var result = ReadyToRunCorrelationQuery.Resolve(
            analyzer, "Greeter.get_Name", CancellationToken.None);

        Assert.AreEqual(ReadyToRunQueryOutcome.Resolved, result.Outcome);
        Assert.IsNotNull(result.Report);
        return result.Report!;
    }

    private static void AssertRealReadyToRunRangesDecode(AssemblyAnalyzer analyzer, NativeArchitecture expected)
    {
        Assert.AreEqual(expected, analyzer.ReadyToRunInfo!.Architecture);
        Assert.IsNotEmpty(analyzer.ReadyToRunMethods);

        var failures = new List<string>();
        var rangeCount = 0;
        foreach (var method in analyzer.ReadyToRunMethods)
        {
            foreach (var range in method.CodeRanges)
            {
                rangeCount++;
                Assert.IsGreaterThan(
                    0,
                    range.Size,
                    $"{method.DeclaringType}.{method.Name} {range.Kind} must have a positive size.");

                if (range.FileOffset is not { } fileOffset)
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

        Assert.IsGreaterThan(0, rangeCount);
        Assert.IsEmpty(failures, string.Join(Environment.NewLine, failures.Take(20)));
    }
}
