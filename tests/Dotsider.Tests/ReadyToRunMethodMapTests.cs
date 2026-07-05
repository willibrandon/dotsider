using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// The method map joining <c>MethodDefEntryPoints</c> and <c>InstanceMethodEntryPoints</c> to their
/// native code ranges, asserted against the real ReadyToRun console. The async state machine's
/// <c>MoveNext</c> proves a body is several disjoint ranges (a hot entry plus a funclet/cold range),
/// not one contiguous slice; the generic samples prove instantiated generics are recovered with a
/// rendered instantiation.
/// </summary>
[Collection("SampleAssemblies")]
public class ReadyToRunMethodMapTests(SampleAssemblyFixture samples)
{
    private const string SkipReason = "ReadyToRun crossgen2 publish did not run on this leg.";

    /// <summary>Every precompiled method has ranges and its total size equals the sum of those ranges.</summary>
    [Fact(Timeout = 30_000)]
    public void Methods_HaveCodeRanges_AndConsistentSizes()
    {
        Assert.SkipWhen(samples.ReadyToRunConsoleDll is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(samples.ReadyToRunConsoleDll!);
        var methods = analyzer.ReadyToRunMethods;

        Assert.NotEmpty(methods);
        foreach (var method in methods)
        {
            Assert.NotEmpty(method.CodeRanges);
            // TotalSize is the sum of every range; no range is dropped from the accounting.
            Assert.Equal(method.CodeRanges.Sum(r => r.Size), method.TotalSize);
            Assert.All(method.CodeRanges, r => Assert.True(r.Size > 0, "a code range must have a positive size"));
            // Exactly one hot entry per method.
            Assert.Equal(1, method.CodeRanges.Count(r => r.Kind == ReadyToRunCodeRangeKind.HotEntry));
        }
    }

    /// <summary>The async state machine's MoveNext spans multiple disjoint ranges (hot plus funclet/cold).</summary>
    [Fact(Timeout = 30_000)]
    public void AsyncStateMachine_MoveNext_IsMultiRange()
    {
        Assert.SkipWhen(samples.ReadyToRunConsoleDll is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(samples.ReadyToRunConsoleDll!);

        // The awaiting MoveNext carries a funclet and/or cold range beyond its hot entry.
        var moveNext = analyzer.ReadyToRunMethods
            .Where(m => m.Name == "MoveNext")
            .OrderByDescending(m => m.CodeRanges.Count)
            .FirstOrDefault();

        Assert.NotNull(moveNext);
        Assert.True(moveNext!.CodeRanges.Count > 1,
            $"MoveNext should span multiple ranges, saw {moveNext.CodeRanges.Count}");

        // Disjoint ranges: no two ranges overlap.
        var ordered = moveNext.CodeRanges.OrderBy(r => r.VirtualAddress).ToList();
        for (var i = 1; i < ordered.Count; i++)
            Assert.True(
                ordered[i].VirtualAddress >= ordered[i - 1].VirtualAddress + (ulong)ordered[i - 1].Size,
                "code ranges must be disjoint");
    }

    /// <summary>Instantiated generics are recovered from the instance table with a rendered instantiation.</summary>
    [Fact(Timeout = 30_000)]
    public void GenericInstantiations_AreRecovered_WithRenderedInstantiation()
    {
        Assert.SkipWhen(samples.ReadyToRunConsoleDll is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(samples.ReadyToRunConsoleDll!);

        var generics = analyzer.ReadyToRunMethods.Where(m => m.IsGenericInstantiation).ToList();
        Assert.NotEmpty(generics);
        // The instantiation display carries the concrete args or the canonical shared form.
        Assert.Contains(generics, g => g.InstantiationDisplay is { } d
            && (d.Contains("int") || d.Contains("__Canon")));
        // A generic instantiation resolves its declaring type and method name from metadata, not a
        // bare token — its owning MethodDef token was recovered from the instance signature.
        Assert.Contains(generics, g => g.DeclaringType is not null && g.Name is not null && g.Token != 0);
    }

    /// <summary>The index resolves a method both by its token and by its hot entry address.</summary>
    [Fact(Timeout = 30_000)]
    public void Index_FindsMethodByToken_AndByAddress()
    {
        Assert.SkipWhen(samples.ReadyToRunConsoleDll is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(samples.ReadyToRunConsoleDll!);
        var index = analyzer.ReadyToRunIndex;
        Assert.NotNull(index);

        var sample = analyzer.ReadyToRunMethods.First(m => m.Token != 0 && m.CodeRanges.Count > 0);
        var byToken = index!.Find(sample.AssemblyName, sample.Token);
        Assert.NotNull(byToken);
        Assert.Equal(sample.Token, byToken!.Token);

        // The hot entry's own address resolves back to the same method.
        var hot = sample.CodeRanges.First(r => r.Kind == ReadyToRunCodeRangeKind.HotEntry);
        var byAddress = index.FindByAddress(hot.VirtualAddress);
        Assert.NotNull(byAddress);
        Assert.Equal(sample.Token, byAddress!.Token);
    }
}
