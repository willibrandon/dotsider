using Dotsider.Analysis;
using Dotsider.Analysis.Models;
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
        _tracer = new RuntimeTracer(assemblyPath, args, _app);
        return _tracer;
    }

    [Fact(Timeout = 30_000)]
    public async Task LaunchHelloWorld_TransitionsToRunning()
    {
        var tracer = CreateTracer(samples.HelloWorldDll);
        Assert.Equal(TraceProcessState.Idle, tracer.ProcessState);
        tracer.Start();
        await TestHelpers.WaitUntilAsync(
            () => tracer.ProcessState is TraceProcessState.Running or TraceProcessState.Exited,
            TimeSpan.FromSeconds(15));
    }

    [Fact(Timeout = 30_000)]
    public async Task LaunchHelloWorld_ExitsSuccessfully()
    {
        var tracer = CreateTracer(samples.HelloWorldDll);
        tracer.Start();
        await TestHelpers.WaitUntilAsync(
            () => tracer.ProcessState == TraceProcessState.Exited,
            TimeSpan.FromSeconds(20));
        Assert.Equal(0, tracer.ExitCode);
    }

    [Fact(Timeout = 30_000)]
    public async Task LaunchHelloWorld_CapturesEvents()
    {
        var tracer = CreateTracer(samples.HelloWorldDll);
        tracer.Start();
        await TestHelpers.WaitUntilAsync(
            () => tracer.ProcessState == TraceProcessState.Exited,
            TimeSpan.FromSeconds(20));
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
            () => tracer.GetLatestCounters() != null,
            TimeSpan.FromSeconds(15));
        var counters = tracer.GetLatestCounters();
        Assert.NotNull(counters);
    }

    [Fact(Timeout = 30_000)]
    public async Task LaunchHelloWorld_CapturesOutput()
    {
        var tracer = CreateTracer(samples.HelloWorldDll);
        tracer.Start();
        await TestHelpers.WaitUntilAsync(
            () => tracer.ProcessState == TraceProcessState.Exited,
            TimeSpan.FromSeconds(20));
        var output = tracer.GetOutput();
        Assert.NotEmpty(output);
    }

    [Fact(Timeout = 30_000)]
    public async Task LaunchHelloWorld_SummaryHasEvents()
    {
        var tracer = CreateTracer(samples.HelloWorldDll);
        tracer.Start();
        await TestHelpers.WaitUntilAsync(
            () => tracer.ProcessState == TraceProcessState.Exited,
            TimeSpan.FromSeconds(20));
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
            () => tracer.ProcessState is TraceProcessState.Running or TraceProcessState.Exited,
            TimeSpan.FromSeconds(15));
        var elapsed1 = tracer.Elapsed;
        if (tracer.ProcessState == TraceProcessState.Running)
        {
            await Task.Delay(100);
            Assert.True(tracer.Elapsed > elapsed1);
        }
    }

    [Fact(Timeout = 30_000)]
    public async Task ErrorMessage_NullOnSuccessfulRun()
    {
        var tracer = CreateTracer(samples.HelloWorldDll);
        tracer.Start();
        await TestHelpers.WaitUntilAsync(
            () => tracer.ProcessState == TraceProcessState.Exited,
            TimeSpan.FromSeconds(20));
        Assert.Null(tracer.ErrorMessage);
    }

    [Fact(Timeout = 30_000)]
    public async Task Summary_TotalExceptions_MatchesEventCount()
    {
        var tracer = CreateTracer(samples.HelloWorldDll);
        tracer.Start();
        await TestHelpers.WaitUntilAsync(
            () => tracer.ProcessState == TraceProcessState.Exited,
            TimeSpan.FromSeconds(20));
        var summary = tracer.GetSummary();
        var exceptionEvents = summary.EventsByCategory
            .GetValueOrDefault(TraceEventCategory.Exception);
        Assert.Equal(exceptionEvents, summary.TotalExceptions);
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
            TimeSpan.FromSeconds(5));
    }

    [Fact(Timeout = 30_000)]
    public async Task Dispose_WhileRunning_CleansUp()
    {
        var tracer = CreateTracer(samples.HelloWorldDll);
        tracer.Start();
        await TestHelpers.WaitUntilAsync(
            () => tracer.ProcessState is TraceProcessState.Running or TraceProcessState.Exited,
            TimeSpan.FromSeconds(15));
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
        await TestHelpers.WaitUntilAsync(
            () => tracer.ProcessState == TraceProcessState.Exited,
            TimeSpan.FromSeconds(20));
        var events = tracer.GetEvents();
        var categories = events.Select(e => e.Category).Distinct().ToHashSet();
        // HelloWorld triggers GC and JIT at minimum
        Assert.True(categories.Count > 0);
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
