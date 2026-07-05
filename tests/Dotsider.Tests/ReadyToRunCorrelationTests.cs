using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// The one query surface (<see cref="ReadyToRunCorrelationQuery"/>) the CLI, MCP, and session share,
/// asserted against the real ReadyToRun console: a unique method name, a token, and a native address
/// all resolve to a per-range report; an overloaded name lists candidates instead of guessing; a
/// precompiled method's native disassembly names its indirect call targets through the import map.
/// </summary>
[Collection("SampleAssemblies")]
public class ReadyToRunCorrelationTests(SampleAssemblyFixture samples)
{
    private const string SkipReason = "ReadyToRun crossgen2 publish did not run on this leg.";

    /// <summary>A unique method name resolves to a precompiled report with native code and ranges.</summary>
    [Fact(Timeout = 30_000)]
    public void ByName_Unique_ResolvesPrecompiled()
    {
        Assert.SkipWhen(samples.ReadyToRunConsoleDll is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(samples.ReadyToRunConsoleDll!);

        var result = ReadyToRunCorrelationQuery.Resolve(
            analyzer, "Greeter.get_Name", TestContext.Current.CancellationToken);

        Assert.Equal(ReadyToRunQueryOutcome.Resolved, result.Outcome);
        Assert.Equal(ReadyToRunNativeAvailability.Precompiled, result.Report!.Availability);
        Assert.NotEmpty(result.Report.Ranges);
        Assert.NotNull(result.Report.NativeText);
        Assert.NotNull(result.Report.Il);
    }

    /// <summary>An overloaded name is reported ambiguous with candidates, never first-match.</summary>
    [Fact(Timeout = 30_000)]
    public void ByName_Overloaded_IsAmbiguous()
    {
        Assert.SkipWhen(samples.ReadyToRunConsoleDll is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(samples.ReadyToRunConsoleDll!);

        // Greeter.Greet has two overloads in the precompiled map.
        var result = ReadyToRunCorrelationQuery.Resolve(
            analyzer, "Greet", TestContext.Current.CancellationToken);

        Assert.Equal(ReadyToRunQueryOutcome.Ambiguous, result.Outcome);
        Assert.True(result.Candidates.Count >= 2);
        Assert.Null(result.Report);
    }

    /// <summary>A native address resolves through its containing method's ranges.</summary>
    [Fact(Timeout = 30_000)]
    public void ByAddress_ResolvesContainingMethod()
    {
        Assert.SkipWhen(samples.ReadyToRunConsoleDll is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(samples.ReadyToRunConsoleDll!);

        var method = analyzer.ReadyToRunMethods.First(m => m.CodeRanges.Count > 0);
        var address = method.CodeRanges[0].VirtualAddress;

        var result = ReadyToRunCorrelationQuery.Resolve(
            analyzer, $"0x{address:x}", TestContext.Current.CancellationToken);

        Assert.Equal(ReadyToRunQueryOutcome.Resolved, result.Outcome);
        Assert.Equal(method.Token, result.Report!.Token);
    }

    /// <summary>A precompiled method's native disassembly names its indirect call targets via imports.</summary>
    [Fact(Timeout = 30_000)]
    public void PrecompiledNative_NamesIndirectCallTargets()
    {
        Assert.SkipWhen(samples.ReadyToRunConsoleDll is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(samples.ReadyToRunConsoleDll!);

        // MoveNext calls Console.WriteLine through an import slot; the resolver names it.
        var result = ReadyToRunCorrelationQuery.Resolve(
            analyzer, "MoveNext", TestContext.Current.CancellationToken);

        Assert.Equal(ReadyToRunQueryOutcome.Resolved, result.Outcome);
        Assert.NotNull(result.Report!.NativeText);
        Assert.Contains("Console.WriteLine", result.Report.NativeText);
    }

    /// <summary>A method present in metadata but absent from the precompiled map resolves as IL-only.</summary>
    [Fact(Timeout = 30_000)]
    public void ByName_NotPrecompiled_ResolvesAsIlOnly()
    {
        Assert.SkipWhen(samples.ReadyToRunConsoleDll is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(samples.ReadyToRunConsoleDll!);
        var index = analyzer.ReadyToRunIndex!;

        // Find a metadata method whose name is unique and not in the precompiled map.
        var precompiledTokens = index.Methods.Select(m => m.Token).ToHashSet();
        var byName = analyzer.MethodDefs
            .Where(m => !precompiledTokens.Contains(m.Token))
            .GroupBy(m => m.Name)
            .Where(g => g.Count() == 1)
            .Select(g => g.Single())
            .FirstOrDefault();
        Assert.SkipWhen(byName is null, "every metadata method in this image is precompiled.");

        var result = ReadyToRunCorrelationQuery.Resolve(
            analyzer, byName!.Name, TestContext.Current.CancellationToken);

        Assert.Equal(ReadyToRunQueryOutcome.Resolved, result.Outcome);
        Assert.Equal(ReadyToRunNativeAvailability.NotPrecompiled, result.Report!.Availability);
        Assert.Null(result.Report.NativeText);
    }
}
