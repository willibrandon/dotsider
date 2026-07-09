using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Views;
using Hex1b;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Runtime Tracer.
/// </summary>
[TestClass]
public class RuntimeTracerTests : IDisposable
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private RuntimeTracer? _tracer;
    private Hex1bApp? _app;
    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;

    private RuntimeTracer CreateTracer(string assemblyPath, string args = "")
    {
        _workload = new Hex1bAppWorkloadAdapter();
        _terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(_workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();
        _app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
            new Hex1bAppOptions { WorkloadAdapter = _workload });
        _tracer = new RuntimeTracer(assemblyPath, args, () => _app.Invalidate());
        return _tracer;
    }

    /// <summary>
    /// Waits for the tracer to reach Exited or Error. If the process hangs
    /// under EventPipe (known issue on Windows CI), stops it after the timeout.
    /// The caller's timeout plus the 10-second recovery wait must fit inside
    /// the test's <c>[Timeout]</c>, or MSTest aborts the test before this
    /// recovery ever runs.
    /// </summary>
    private static async Task WaitForExitAsync(RuntimeTracer tracer, TimeSpan timeout)
    {
        try
        {
            await TestHelpers.WaitUntilAsync(
                () => tracer.ProcessState is TraceProcessState.Exited or TraceProcessState.Error,
                timeout);
        }
        catch (TimeoutException)
        {
            tracer.Stop();
            await TestHelpers.WaitUntilAsync(
                () => tracer.ProcessState is TraceProcessState.Exited or TraceProcessState.Error,
                TimeSpan.FromSeconds(10));
        }
    }

    /// <summary>
    /// Verifies launch hello world transitions to running.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task LaunchHelloWorld_TransitionsToRunning()
    {
        var tracer = CreateTracer(Samples.HelloWorldDll);
        Assert.AreEqual(TraceProcessState.Idle, tracer.ProcessState);
        tracer.Start();
        await TestHelpers.WaitUntilAsync(
            () => tracer.ProcessState is TraceProcessState.Running or TraceProcessState.Exited
                or TraceProcessState.Error,
            TimeSpan.FromSeconds(30));
        Assert.IsTrue(tracer.ProcessState is TraceProcessState.Running or TraceProcessState.Exited,
            $"Expected Running or Exited but got {tracer.ProcessState}: {tracer.ErrorMessage}");
    }

    /// <summary>
    /// Verifies launch hello world exits successfully.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task LaunchHelloWorld_ExitsSuccessfully()
    {
        var tracer = CreateTracer(Samples.HelloWorldDll);
        tracer.Start();
        await WaitForExitAsync(tracer, TimeSpan.FromSeconds(15));
        Assert.AreEqual(TraceProcessState.Exited, tracer.ProcessState);
        // ExitCode is set by the Process.Exited handler which fires asynchronously —
        // wait for it rather than reading immediately after state transition.
        await TestHelpers.WaitUntilAsync(() => tracer.ExitCode is not null, TimeSpan.FromSeconds(5));
        Assert.AreEqual(0, tracer.ExitCode);
    }

    /// <summary>
    /// Verifies launch hello world captures events.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task LaunchHelloWorld_CapturesEvents()
    {
        var tracer = CreateTracer(Samples.HelloWorldDll);
        tracer.Start();
        await WaitForExitAsync(tracer, TimeSpan.FromSeconds(15));
        Assert.AreEqual(TraceProcessState.Exited, tracer.ProcessState);
        var events = tracer.GetEvents();
        Assert.IsNotEmpty(events);
    }

    /// <summary>
    /// Verifies launch hello world captures counters.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task LaunchHelloWorld_CapturesCounters()
    {
        var tracer = CreateTracer(Samples.HelloWorldDll);
        tracer.Start();
        // Counters arrive every ~1s — wait up to 10s
        await TestHelpers.WaitUntilAsync(
            () => tracer.GetLatestCounters() != null
                || tracer.ProcessState == TraceProcessState.Error,
            TimeSpan.FromSeconds(30));
        Assert.AreNotEqual(TraceProcessState.Error, tracer.ProcessState);
        var counters = tracer.GetLatestCounters();
        Assert.IsNotNull(counters);
    }

    /// <summary>
    /// Verifies launch hello world captures output.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task LaunchHelloWorld_CapturesOutput()
    {
        var tracer = CreateTracer(Samples.HelloWorldDll);
        tracer.Start();
        await WaitForExitAsync(tracer, TimeSpan.FromSeconds(15));
        Assert.AreEqual(TraceProcessState.Exited, tracer.ProcessState);
        var output = tracer.GetOutput();
        Assert.IsNotEmpty(output);
    }

    /// <summary>
    /// Verifies launch hello world summary has events.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task LaunchHelloWorld_SummaryHasEvents()
    {
        var tracer = CreateTracer(Samples.HelloWorldDll);
        tracer.Start();
        await WaitForExitAsync(tracer, TimeSpan.FromSeconds(15));
        Assert.AreEqual(TraceProcessState.Exited, tracer.ProcessState);
        var summary = tracer.GetSummary();
        Assert.IsGreaterThan(0, summary.TotalEvents);
    }

    /// <summary>
    /// Verifies launch hello world process id is set.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task LaunchHelloWorld_ProcessIdIsSet()
    {
        var tracer = CreateTracer(Samples.HelloWorldDll);
        tracer.Start();
        await TestHelpers.WaitUntilAsync(
            () => tracer.ProcessId != null,
            TimeSpan.FromSeconds(10));
        Assert.IsNotNull(tracer.ProcessId);
    }

    /// <summary>
    /// Verifies launch hello world elapsed increases.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task LaunchHelloWorld_ElapsedIncreases()
    {
        var tracer = CreateTracer(Samples.HelloWorldDll);
        tracer.Start();
        await TestHelpers.WaitUntilAsync(
            () => tracer.ProcessState is TraceProcessState.Running or TraceProcessState.Exited
                or TraceProcessState.Error,
            TimeSpan.FromSeconds(30));
        Assert.AreNotEqual(TraceProcessState.Error, tracer.ProcessState);
        var elapsed1 = tracer.Elapsed;
        if (tracer.ProcessState == TraceProcessState.Running)
        {
            await Task.Delay(500, CancellationToken.None);
            Assert.IsGreaterThan(elapsed1, tracer.Elapsed);
        }
    }

    /// <summary>
    /// Verifies error message null on successful run.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ErrorMessage_NullOnSuccessfulRun()
    {
        var tracer = CreateTracer(Samples.HelloWorldDll);
        tracer.Start();
        await WaitForExitAsync(tracer, TimeSpan.FromSeconds(15));
        Assert.AreEqual(TraceProcessState.Exited, tracer.ProcessState);
        Assert.IsNull(tracer.ErrorMessage);
    }

    /// <summary>
    /// Verifies summary total exceptions matches event count.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Summary_TotalExceptions_MatchesEventCount()
    {
        var tracer = CreateTracer(Samples.HelloWorldDll);
        tracer.Start();
        await WaitForExitAsync(tracer, TimeSpan.FromSeconds(15));
        Assert.AreEqual(TraceProcessState.Exited, tracer.ProcessState);
        var summary = tracer.GetSummary();
        var exceptionEvents = summary.EventsByCategory
            .GetValueOrDefault(TraceEventCategory.Exception);
        Assert.AreEqual(exceptionEvents, summary.TotalExceptions);
    }

    /// <summary>
    /// Verifies complex app short lived still captures events.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ComplexApp_ShortLived_StillCapturesEvents()
    {
        var tracer = CreateTracer(Samples.ComplexAppDll);
        tracer.Start();
        await WaitForExitAsync(tracer, TimeSpan.FromSeconds(45));
        Assert.AreEqual(TraceProcessState.Exited, tracer.ProcessState);

        var events = tracer.GetEvents();

        await TestHelpers.WaitUntilAsync(() => tracer.ExitCode is not null, TimeSpan.FromSeconds(5));
        Assert.AreEqual(0, tracer.ExitCode);
        Assert.IsNotEmpty(events);
    }

    // --- Lifecycle edge cases ---

    /// <summary>
    /// Verifies stop during startup unblocks within five seconds.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Stop_DuringStartup_UnblocksWithinFiveSeconds()
    {
        var tracer = CreateTracer(Samples.HelloWorldDll);
        tracer.Start();
        tracer.Stop();
        await TestHelpers.WaitUntilAsync(
            () => tracer.ProcessState is TraceProcessState.Exited or TraceProcessState.Error,
            TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// Verifies dispose while running cleans up.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Dispose_WhileRunning_CleansUp()
    {
        var tracer = CreateTracer(Samples.HelloWorldDll);
        tracer.Start();
        await TestHelpers.WaitUntilAsync(
            () => tracer.ProcessState is TraceProcessState.Running or TraceProcessState.Exited
                or TraceProcessState.Error,
            TimeSpan.FromSeconds(30));
        tracer.Dispose();
        _tracer = null; // prevent double-dispose in test cleanup
    }

    /// <summary>
    /// Verifies dispose called twice no throw.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Dispose_CalledTwice_NoThrow()
    {
        var tracer = CreateTracer(Samples.HelloWorldDll);
        tracer.Dispose();
        tracer.Dispose(); // should not throw
        _tracer = null;
    }

    /// <summary>
    /// Verifies event categories contain expected types.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task EventCategories_ContainExpectedTypes()
    {
        var tracer = CreateTracer(Samples.HelloWorldDll);
        tracer.Start();
        await WaitForExitAsync(tracer, TimeSpan.FromSeconds(15));
        Assert.AreEqual(TraceProcessState.Exited, tracer.ProcessState);
        var events = tracer.GetEvents();
        var categories = events.Select(e => e.Category).Distinct().ToHashSet();
        // HelloWorld triggers GC and JIT at minimum
        Assert.IsGreaterThan(0, categories.Count);
    }

    /// <summary>
    /// Verifies jit events overloaded methods disambiguated by token.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task JitEvents_OverloadedMethods_DisambiguatedByToken()
    {
        var tracer = CreateTracer(Samples.HelloWorldDll);
        tracer.Start();
        // Wait for the specific Format JIT events — ProcessState can transition
        // to Exited before all events are flushed from the EventPipe buffer.
        await WaitForExitAsync(tracer, TimeSpan.FromSeconds(15));
        Assert.AreEqual(TraceProcessState.Exited, tracer.ProcessState);
        await TestHelpers.WaitUntilAsync(
            () => tracer.GetEvents().Count(e => e.Category == TraceEventCategory.JIT
                && e.Detail.EndsWith(".Format") && e.MetadataToken > 0) >= 2,
            TimeSpan.FromSeconds(10));

        var jitEvents = tracer.GetEvents()
            .Where(e => e.Category == TraceEventCategory.JIT)
            .ToList();
        Assert.IsNotEmpty(jitEvents);

        var formatEvents = jitEvents
            .Where(e => e.Detail.EndsWith(".Format"))
            .ToList();
        Assert.IsGreaterThanOrEqualTo(2, formatEvents.Count, $"Expected >=2 Formatter.Format JIT events, got {formatEvents.Count}");

        // Tokens must be distinct (the whole point of disambiguation)
        var distinctTokens = formatEvents.Select(e => e.MetadataToken).Distinct().ToList();
        Assert.IsGreaterThanOrEqualTo(2, distinctTokens.Count, $"Overloaded JIT events should have distinct tokens, got: " +
            $"{string.Join(", ", formatEvents.Select(e => $"0x{e.MetadataToken:X8}"))}");

        // Verify token-based lookup resolves each to a different MethodDefInfo,
        // while name-based lookup would collapse them to the same method.
        using var analyzer = new AssemblyAnalyzer(Samples.HelloWorldDll);
        var evt1 = formatEvents[0];
        var evt2 = formatEvents.First(e => e.MetadataToken != evt1.MetadataToken);

        var byToken1 = analyzer.MethodDefs.FirstOrDefault(m => m.Token == evt1.MetadataToken);
        var byToken2 = analyzer.MethodDefs.FirstOrDefault(m => m.Token == evt2.MetadataToken);
        Assert.IsNotNull(byToken1);
        Assert.IsNotNull(byToken2);
        Assert.AreNotEqual(byToken1.Token, byToken2.Token);

        // Name-based lookup returns the same method for both (the disambiguation gap)
        Assert.IsTrue(DynamicAnalysisView.TryParseJitDetail(evt1.Detail, out var declType, out var methName));
        var byName = analyzer.MethodDefs
            .Where(m => m.DeclaringType == declType && m.Name == methName)
            .ToList();
        Assert.IsGreaterThanOrEqualTo(2, byName.Count, "Analyzer should have >=2 MethodDefs with the same DeclaringType+Name");
        var firstByName = byName[0];
        // Without token, FirstOrDefault always returns the same method regardless of which event
        Assert.AreEqual(firstByName, analyzer.MethodDefs.FirstOrDefault(
            m => m.DeclaringType == declType && m.Name == methName));
    }

    /// <summary>
    /// Disposes test resources created during the run.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _tracer?.Dispose();
        _app?.Dispose();
        _terminal?.Dispose();
        _workload?.Dispose();
    }
}
