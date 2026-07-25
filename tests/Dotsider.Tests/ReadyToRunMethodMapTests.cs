using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using System.Runtime.InteropServices;

namespace Dotsider.Tests;

/// <summary>
/// The method map joining <c>MethodDefEntryPoints</c> and <c>InstanceMethodEntryPoints</c> to their
/// native code ranges, asserted against the real ReadyToRun console. The async state machine's
/// <c>MoveNext</c> proves a body is several disjoint ranges (a hot entry plus a funclet/cold range),
/// not one contiguous slice; the generic samples prove instantiated generics are recovered with a
/// rendered instantiation.
/// </summary>
[TestClass]
public sealed class ReadyToRunMethodMapTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private const string SkipReason = "ReadyToRun crossgen2 publish did not run on this leg.";

    /// <summary>Every precompiled method has ranges and its total size equals the sum of those ranges.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Methods_HaveCodeRanges_AndConsistentSizes()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(Samples.ReadyToRunConsoleDll!);
        var methods = analyzer.ReadyToRunMethods;

        Assert.IsNotEmpty(methods);
        foreach (var method in methods)
        {
            Assert.IsNotEmpty(method.CodeRanges);
            // TotalSize is the sum of every range; no range is dropped from the accounting.
            Assert.AreEqual(method.CodeRanges.Sum(r => r.Size), method.TotalSize);
            TestAssert.All(method.CodeRanges, r => Assert.IsGreaterThan(0, r.Size, "a code range must have a positive size"));
            // Exactly one hot entry per method.
            Assert.ContainsSingle(r => r.Kind == ReadyToRunCodeRangeKind.HotEntry, method.CodeRanges);
        }
    }

    /// <summary>
    /// Verifies a real host-produced ARM64 image exposes a positive final code range through the
    /// public method-map facade.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Arm64_FinalRuntimeFunction_HasPositiveRangeThroughFacade()
    {
        TestSkip.When(
            RuntimeInformation.ProcessArchitecture != Architecture.Arm64,
            "The host ReadyToRun fixture is ARM64 only on an ARM64 test leg.");
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);

        using var analyzer = new AssemblyAnalyzer(Samples.ReadyToRunConsoleDll!);
        var methods = analyzer.ReadyToRunMethods;
        var finalRange = methods
            .SelectMany(static method => method.CodeRanges)
            .MaxBy(static range => range.VirtualAddress);

        Assert.AreEqual(NativeArchitecture.Arm64, analyzer.ReadyToRunInfo!.Architecture);
        Assert.IsNotEmpty(methods);
        Assert.IsNotNull(finalRange);
        Assert.IsGreaterThan(0, finalRange.Size);
    }

    /// <summary>The async state machine's MoveNext spans multiple disjoint ranges (hot plus funclet/cold).</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void AsyncStateMachine_MoveNext_IsMultiRange()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(Samples.ReadyToRunConsoleDll!);

        // The awaiting MoveNext carries a funclet and/or cold range beyond its hot entry.
        var moveNext = analyzer.ReadyToRunMethods
            .Where(m => m.Name == "MoveNext")
            .OrderByDescending(m => m.CodeRanges.Count)
            .FirstOrDefault();

        Assert.IsNotNull(moveNext);
        Assert.IsGreaterThan(1, moveNext!.CodeRanges.Count, $"MoveNext should span multiple ranges, saw {moveNext.CodeRanges.Count}");

        // Disjoint ranges: no two ranges overlap.
        var ordered = moveNext.CodeRanges.OrderBy(r => r.VirtualAddress).ToList();
        for (var i = 1; i < ordered.Count; i++)
            Assert.IsGreaterThanOrEqualTo(ordered[i - 1].VirtualAddress + (ulong)ordered[i - 1].Size, ordered[i].VirtualAddress, "code ranges must be disjoint");
    }

    /// <summary>Instantiated generics are recovered from the instance table with a rendered instantiation.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void GenericInstantiations_AreRecovered_WithRenderedInstantiation()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(Samples.ReadyToRunConsoleDll!);

        var generics = analyzer.ReadyToRunMethods.Where(m => m.IsGenericInstantiation).ToList();
        Assert.IsNotEmpty(generics);
        // The instantiation display carries the concrete args or the canonical shared form.
        Assert.Contains(g => g.InstantiationDisplay is { } d
            && (d.Contains("int") || d.Contains("__Canon")), generics);
        // A generic instantiation resolves its declaring type and method name from metadata, not a
        // bare token — its owning MethodDef token was recovered from the instance signature.
        Assert.Contains(g => g.DeclaringType is not null && g.Name is not null && g.Token != 0, generics);
    }

    /// <summary>The index resolves a method both by its token and by its hot entry address.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Index_FindsMethodByToken_AndByAddress()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(Samples.ReadyToRunConsoleDll!);
        var index = analyzer.ReadyToRunIndex;
        Assert.IsNotNull(index);

        var sample = analyzer.ReadyToRunMethods.First(m => m.Token != 0 && m.CodeRanges.Count > 0);
        var byToken = index!.Find(sample.AssemblyName, sample.Token);
        Assert.IsNotNull(byToken);
        Assert.AreEqual(sample.Token, byToken!.Token);

        // The hot entry's own address resolves back to the same method.
        var hot = sample.CodeRanges.First(r => r.Kind == ReadyToRunCodeRangeKind.HotEntry);
        var byAddress = index.FindByAddress(hot.VirtualAddress);
        Assert.IsNotNull(byAddress);
        Assert.AreEqual(sample.Token, byAddress!.Token);
    }
}
