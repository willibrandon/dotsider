using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// The one query surface (<see cref="ReadyToRunCorrelationQuery"/>) the CLI, MCP, and session share,
/// asserted against the real ReadyToRun console: a unique method name, a token, and a native address
/// all resolve to a per-range report; an overloaded name lists candidates instead of guessing; a
/// precompiled method's native disassembly names its indirect call targets through the import map.
/// </summary>
[TestClass]
public class ReadyToRunCorrelationTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private const string SkipReason = "ReadyToRun crossgen2 publish did not run on this leg.";

    /// <summary>A unique method name resolves to a precompiled report with native code and ranges.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ByName_Unique_ResolvesPrecompiled()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(Samples.ReadyToRunConsoleDll!);

        var result = ReadyToRunCorrelationQuery.Resolve(
            analyzer, "Greeter.get_Name", CancellationToken.None);

        Assert.AreEqual(ReadyToRunQueryOutcome.Resolved, result.Outcome);
        Assert.AreEqual(ReadyToRunNativeAvailability.Precompiled, result.Report!.Availability);
        Assert.IsNotEmpty(result.Report.Ranges);
        Assert.IsNotNull(result.Report.NativeText);
        Assert.IsNotNull(result.Report.Il);
    }

    /// <summary>An overloaded name is reported ambiguous with candidates, never first-match.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ByName_Overloaded_IsAmbiguous()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(Samples.ReadyToRunConsoleDll!);

        // Greeter.Greet has two overloads in the precompiled map.
        var result = ReadyToRunCorrelationQuery.Resolve(
            analyzer, "Greet", CancellationToken.None);

        Assert.AreEqual(ReadyToRunQueryOutcome.Ambiguous, result.Outcome);
        Assert.IsGreaterThanOrEqualTo(2, result.Candidates.Count);
        Assert.IsNull(result.Report);
    }

    /// <summary>A native address resolves through its containing method's ranges.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ByAddress_ResolvesContainingMethod()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(Samples.ReadyToRunConsoleDll!);

        var method = analyzer.ReadyToRunMethods.First(m => m.CodeRanges.Count > 0);
        var address = method.CodeRanges[0].VirtualAddress;

        var result = ReadyToRunCorrelationQuery.Resolve(
            analyzer, $"0x{address:x}", CancellationToken.None);

        Assert.AreEqual(ReadyToRunQueryOutcome.Resolved, result.Outcome);
        Assert.AreEqual(method.Token, result.Report!.Token);
    }

    /// <summary>A precompiled method's native disassembly names its indirect call targets via imports.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void PrecompiledNative_NamesIndirectCallTargets()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(Samples.ReadyToRunConsoleDll!);

        // MoveNext calls Console.WriteLine through an import slot; the resolver names it.
        var result = ReadyToRunCorrelationQuery.Resolve(
            analyzer, "MoveNext", CancellationToken.None);

        Assert.AreEqual(ReadyToRunQueryOutcome.Resolved, result.Outcome);
        Assert.IsNotNull(result.Report!.NativeText);
        Assert.Contains("Console.WriteLine", result.Report.NativeText);
    }

    /// <summary>A method present in metadata but absent from the precompiled map resolves as IL-only.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ByName_NotPrecompiled_ResolvesAsIlOnly()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(Samples.ReadyToRunConsoleDll!);
        var index = analyzer.ReadyToRunIndex!;

        // Find a metadata method whose name is unique and not in the precompiled map.
        var precompiledTokens = index.Methods.Select(m => m.Token).ToHashSet();
        var byName = analyzer.MethodDefs
            .Where(m => !precompiledTokens.Contains(m.Token))
            .GroupBy(m => m.Name)
            .Where(g => g.Count() == 1)
            .Select(g => g.Single())
            .FirstOrDefault();
        TestSkip.When(byName is null, "every metadata method in this image is precompiled.");

        var result = ReadyToRunCorrelationQuery.Resolve(
            analyzer, byName!.Name, CancellationToken.None);

        Assert.AreEqual(ReadyToRunQueryOutcome.Resolved, result.Outcome);
        Assert.AreEqual(ReadyToRunNativeAvailability.NotPrecompiled, result.Report!.Availability);
        Assert.IsNull(result.Report.NativeText);
    }
}
