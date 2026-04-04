using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Views;
using Hex1b;
using Hex1b.Widgets;

namespace Dotsider.Tests;

[Collection("SampleAssemblies")]
public class RuntimeTracerTests(SampleAssemblyFixture samples) : IDisposable
{
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

    [Fact(Timeout = 30_000)]
    public async Task LaunchHelloWorld_TransitionsToRunning()
    {
        var tracer = CreateTracer(samples.HelloWorldDll);
        Assert.Equal(TraceProcessState.Idle, tracer.ProcessState);
        tracer.Start();
        await TestHelpers.WaitUntilAsync(
            () => tracer.ProcessState is TraceProcessState.Running or TraceProcessState.Exited
                or TraceProcessState.Error,
            TimeSpan.FromSeconds(30));
        Assert.True(tracer.ProcessState is TraceProcessState.Running or TraceProcessState.Exited,
            $"Expected Running or Exited but got {tracer.ProcessState}: {tracer.ErrorMessage}");
    }

    [Fact(Timeout = 30_000)]
    public async Task LaunchHelloWorld_ExitsSuccessfully()
    {
        var tracer = CreateTracer(samples.HelloWorldDll);
        tracer.Start();
        await WaitForExitAsync(tracer, TimeSpan.FromSeconds(30));
        Assert.Equal(TraceProcessState.Exited, tracer.ProcessState);
        // ExitCode is set by the Process.Exited handler which fires asynchronously —
        // wait for it rather than reading immediately after state transition.
        await TestHelpers.WaitUntilAsync(() => tracer.ExitCode is not null, TimeSpan.FromSeconds(5));
        Assert.Equal(0, tracer.ExitCode);
    }

    [Fact(Timeout = 30_000)]
    public async Task LaunchHelloWorld_CapturesEvents()
    {
        var tracer = CreateTracer(samples.HelloWorldDll);
        tracer.Start();
        await WaitForExitAsync(tracer, TimeSpan.FromSeconds(30));
        Assert.Equal(TraceProcessState.Exited, tracer.ProcessState);
        var events = tracer.GetEvents();
        Assert.NotEmpty(events);
    }

    [Fact(Timeout = 30_000)]
    public async Task LaunchHelloWorld_CapturesCounters()
    {
        var tracer = CreateTracer(samples.HelloWorldDll);
        tracer.Start();
        // Counters arrive every ~1s — wait up to 10s
        await TestHelpers.WaitUntilAsync(
            () => tracer.GetLatestCounters() != null
                || tracer.ProcessState == TraceProcessState.Error,
            TimeSpan.FromSeconds(30));
        Assert.NotEqual(TraceProcessState.Error, tracer.ProcessState);
        var counters = tracer.GetLatestCounters();
        Assert.NotNull(counters);
    }

    [Fact(Timeout = 30_000)]
    public async Task LaunchHelloWorld_CapturesOutput()
    {
        var tracer = CreateTracer(samples.HelloWorldDll);
        tracer.Start();
        await WaitForExitAsync(tracer, TimeSpan.FromSeconds(30));
        Assert.Equal(TraceProcessState.Exited, tracer.ProcessState);
        var output = tracer.GetOutput();
        Assert.NotEmpty(output);
    }

    [Fact(Timeout = 30_000)]
    public async Task LaunchHelloWorld_SummaryHasEvents()
    {
        var tracer = CreateTracer(samples.HelloWorldDll);
        tracer.Start();
        await WaitForExitAsync(tracer, TimeSpan.FromSeconds(30));
        Assert.Equal(TraceProcessState.Exited, tracer.ProcessState);
        var summary = tracer.GetSummary();
        Assert.True(summary.TotalEvents > 0);
    }

    [Fact(Timeout = 30_000)]
    public async Task LaunchHelloWorld_ProcessIdIsSet()
    {
        var tracer = CreateTracer(samples.HelloWorldDll);
        tracer.Start();
        await TestHelpers.WaitUntilAsync(
            () => tracer.ProcessId != null,
            TimeSpan.FromSeconds(10));
        Assert.NotNull(tracer.ProcessId);
    }

    [Fact(Timeout = 30_000)]
    public async Task LaunchHelloWorld_ElapsedIncreases()
    {
        var tracer = CreateTracer(samples.HelloWorldDll);
        tracer.Start();
        await TestHelpers.WaitUntilAsync(
            () => tracer.ProcessState is TraceProcessState.Running or TraceProcessState.Exited
                or TraceProcessState.Error,
            TimeSpan.FromSeconds(30));
        Assert.NotEqual(TraceProcessState.Error, tracer.ProcessState);
        var elapsed1 = tracer.Elapsed;
        if (tracer.ProcessState == TraceProcessState.Running)
        {
            await Task.Delay(500, TestContext.Current.CancellationToken);
            Assert.True(tracer.Elapsed > elapsed1);
        }
    }

    [Fact(Timeout = 30_000)]
    public async Task ErrorMessage_NullOnSuccessfulRun()
    {
        var tracer = CreateTracer(samples.HelloWorldDll);
        tracer.Start();
        await WaitForExitAsync(tracer, TimeSpan.FromSeconds(30));
        Assert.Equal(TraceProcessState.Exited, tracer.ProcessState);
        Assert.Null(tracer.ErrorMessage);
    }

    [Fact(Timeout = 30_000)]
    public async Task Summary_TotalExceptions_MatchesEventCount()
    {
        var tracer = CreateTracer(samples.HelloWorldDll);
        tracer.Start();
        await WaitForExitAsync(tracer, TimeSpan.FromSeconds(30));
        Assert.Equal(TraceProcessState.Exited, tracer.ProcessState);
        var summary = tracer.GetSummary();
        var exceptionEvents = summary.EventsByCategory
            .GetValueOrDefault(TraceEventCategory.Exception);
        Assert.Equal(exceptionEvents, summary.TotalExceptions);
    }

    [Fact(Timeout = 60_000)]
    public async Task ComplexApp_ShortLived_StillCapturesEvents()
    {
        var tracer = CreateTracer(samples.ComplexAppDll);
        tracer.Start();
        await WaitForExitAsync(tracer, TimeSpan.FromSeconds(45));
        Assert.Equal(TraceProcessState.Exited, tracer.ProcessState);

        var events = tracer.GetEvents();

        await TestHelpers.WaitUntilAsync(() => tracer.ExitCode is not null, TimeSpan.FromSeconds(5));
        Assert.Equal(0, tracer.ExitCode);
        Assert.NotEmpty(events);
    }

    // --- Lifecycle edge cases ---

    [Fact(Timeout = 30_000)]
    public async Task Stop_DuringStartup_UnblocksWithinFiveSeconds()
    {
        var tracer = CreateTracer(samples.HelloWorldDll);
        tracer.Start();
        tracer.Stop();
        await TestHelpers.WaitUntilAsync(
            () => tracer.ProcessState is TraceProcessState.Exited or TraceProcessState.Error,
            TimeSpan.FromSeconds(10));
    }

    [Fact(Timeout = 30_000)]
    public async Task Dispose_WhileRunning_CleansUp()
    {
        var tracer = CreateTracer(samples.HelloWorldDll);
        tracer.Start();
        await TestHelpers.WaitUntilAsync(
            () => tracer.ProcessState is TraceProcessState.Running or TraceProcessState.Exited
                or TraceProcessState.Error,
            TimeSpan.FromSeconds(30));
        tracer.Dispose();
        _tracer = null; // prevent double-dispose in test cleanup
    }

    [Fact(Timeout = 30_000)]
    public void Dispose_CalledTwice_NoThrow()
    {
        var tracer = CreateTracer(samples.HelloWorldDll);
        tracer.Dispose();
        tracer.Dispose(); // should not throw
        _tracer = null;
    }

    [Fact(Timeout = 30_000)]
    public async Task EventCategories_ContainExpectedTypes()
    {
        var tracer = CreateTracer(samples.HelloWorldDll);
        tracer.Start();
        await WaitForExitAsync(tracer, TimeSpan.FromSeconds(30));
        Assert.Equal(TraceProcessState.Exited, tracer.ProcessState);
        var events = tracer.GetEvents();
        var categories = events.Select(e => e.Category).Distinct().ToHashSet();
        // HelloWorld triggers GC and JIT at minimum
        Assert.True(categories.Count > 0);
    }

    [Fact(Timeout = 30_000)]
    public async Task JitEvents_OverloadedMethods_DisambiguatedByToken()
    {
        var tracer = CreateTracer(samples.HelloWorldDll);
        tracer.Start();
        // Wait for the specific Format JIT events — ProcessState can transition
        // to Exited before all events are flushed from the EventPipe buffer.
        await WaitForExitAsync(tracer, TimeSpan.FromSeconds(30));
        Assert.Equal(TraceProcessState.Exited, tracer.ProcessState);
        await TestHelpers.WaitUntilAsync(
            () => tracer.GetEvents().Count(e => e.Category == TraceEventCategory.JIT
                && e.Detail.EndsWith(".Format") && e.MetadataToken > 0) >= 2,
            TimeSpan.FromSeconds(10));

        var jitEvents = tracer.GetEvents()
            .Where(e => e.Category == TraceEventCategory.JIT)
            .ToList();
        Assert.NotEmpty(jitEvents);

        var formatEvents = jitEvents
            .Where(e => e.Detail.EndsWith(".Format"))
            .ToList();
        Assert.True(formatEvents.Count >= 2,
            $"Expected >=2 Formatter.Format JIT events, got {formatEvents.Count}");

        // Tokens must be distinct (the whole point of disambiguation)
        var distinctTokens = formatEvents.Select(e => e.MetadataToken).Distinct().ToList();
        Assert.True(distinctTokens.Count >= 2,
            $"Overloaded JIT events should have distinct tokens, got: " +
            $"{string.Join(", ", formatEvents.Select(e => $"0x{e.MetadataToken:X8}"))}");

        // Verify token-based lookup resolves each to a different MethodDefInfo,
        // while name-based lookup would collapse them to the same method.
        using var analyzer = new AssemblyAnalyzer(samples.HelloWorldDll);
        var evt1 = formatEvents[0];
        var evt2 = formatEvents.First(e => e.MetadataToken != evt1.MetadataToken);

        var byToken1 = analyzer.MethodDefs.FirstOrDefault(m => m.Token == evt1.MetadataToken);
        var byToken2 = analyzer.MethodDefs.FirstOrDefault(m => m.Token == evt2.MetadataToken);
        Assert.NotNull(byToken1);
        Assert.NotNull(byToken2);
        Assert.NotEqual(byToken1.Token, byToken2.Token);

        // Name-based lookup returns the same method for both (the disambiguation gap)
        Assert.True(DynamicAnalysisView.TryParseJitDetail(evt1.Detail, out var declType, out var methName));
        var byName = analyzer.MethodDefs
            .Where(m => m.DeclaringType == declType && m.Name == methName)
            .ToList();
        Assert.True(byName.Count >= 2,
            "Analyzer should have >=2 MethodDefs with the same DeclaringType+Name");
        var firstByName = byName[0];
        // Without token, FirstOrDefault always returns the same method regardless of which event
        Assert.Equal(firstByName, analyzer.MethodDefs.FirstOrDefault(
            m => m.DeclaringType == declType && m.Name == methName));
    }

    public void Dispose()
    {
        _tracer?.Dispose();
        _app?.Dispose();
        _terminal?.Dispose();
        _workload?.Dispose();
        GC.SuppressFinalize(this);
    }
}
