using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using System.Diagnostics;

namespace Dotsider.Benchmarks;

/// <summary>
/// Benchmarks for <see cref="RuntimeTracer"/> data retrieval with populated ring buffer
/// and summary accumulators. A real trace is started in GlobalSetup against HelloWorld
/// (exits immediately), populating events, counters, and output.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class RuntimeTracerDataRetrievalBenchmarks
{
    private RuntimeTracer _tracer = null!;
    private string _helloWorldDll = null!;

    /// <summary>
    /// Runs a real HelloWorld trace to completion so the ring buffer, summary, and output queues are populated.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        BenchmarkHelpers.BuildSample("samples/HelloWorld");
        _helloWorldDll = BenchmarkHelpers.GetBuildPath("samples/HelloWorld", "HelloWorld.dll");

        if (!File.Exists(_helloWorldDll))
            throw new FileNotFoundException($"HelloWorld.dll not found: {_helloWorldDll}");

        // Start a real trace — HelloWorld prints and exits quickly, populating ring buffer
        _tracer = new RuntimeTracer(_helloWorldDll, [], static () => { });
        _tracer.Start();

        // Wait for the process to exit and events to be collected
        var sw = Stopwatch.StartNew();
        while (_tracer.ProcessState is TraceProcessState.Idle or TraceProcessState.Starting
                   or TraceProcessState.Running
               && sw.Elapsed.TotalSeconds < 15)
        {
            Thread.Sleep(100);
        }

        // Brief additional wait to let the event processing task finish draining
        Thread.Sleep(500);
    }

    /// <summary>
    /// Stops and disposes the shared tracer.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _tracer.Stop();
        _tracer.Dispose();
    }

    /// <summary>
    /// Reads all events from a populated ring buffer — characterizes the snapshot-copy hot path.
    /// </summary>
    [Benchmark(Description = "GetEvents (populated ring buffer)")]
    [BenchmarkCategory("DataRetrieval")]
    public IReadOnlyList<TraceEventEntry> GetEvents_Populated()
        => _tracer.GetEvents();

    /// <summary>
    /// Reads the summary from populated per-provider accumulators.
    /// </summary>
    [Benchmark(Description = "GetSummary (populated accumulators)")]
    [BenchmarkCategory("DataRetrieval")]
    public TraceSummary GetSummary_Populated()
        => _tracer.GetSummary();

    /// <summary>
    /// Reads the populated stdout/stderr output queue.
    /// </summary>
    [Benchmark(Description = "GetOutput (populated queue)")]
    [BenchmarkCategory("DataRetrieval")]
    public IReadOnlyList<OutputLine> GetOutput_Populated()
        => _tracer.GetOutput();

    /// <summary>
    /// Reads the latest counter snapshot after the run has produced one.
    /// </summary>
    [Benchmark(Description = "GetLatestCounters (populated snapshot)")]
    [BenchmarkCategory("DataRetrieval")]
    public CounterSnapshot? GetLatestCounters_Populated()
        => _tracer.GetLatestCounters();
}

/// <summary>
/// Throughput benchmarks for <see cref="RuntimeTracer"/> that read data while a live
/// trace is actively processing events from a load-generating target app.
/// Measures lock contention between the event-processing write path and the read path.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class RuntimeTracerThroughputBenchmarks
{
    private RuntimeTracer _tracer = null!;
    private string _loadGenDll = null!;
    private string _loadGenDir = null!;

    /// <summary>
    /// Builds a 30-second load-generator app and starts a live trace so readers contend with the write path.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        // Create a temporary load-generator app that runs for 30 seconds
        // generating steady GC/JIT/Loader events
        _loadGenDir = Path.Combine(Path.GetTempPath(), $"dotsider-bench-loadgen-{Guid.NewGuid().ToString("N")[..8]}");
        Directory.CreateDirectory(_loadGenDir);

        File.WriteAllText(Path.Combine(_loadGenDir, "LoadGen.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
            </Project>
            """);

        File.WriteAllText(Path.Combine(_loadGenDir, "Program.cs"), """
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var list = new List<byte[]>();
            int i = 0;
            while (sw.Elapsed.TotalSeconds < 30)
            {
                list.Add(new byte[1024]);
                if (++i % 1000 == 0)
                {
                    list.Clear();
                    GC.Collect(0);
                }
            }
            """);

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "build -c Release -v q",
            WorkingDirectory = _loadGenDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        var buildProcess = Process.Start(psi)!;
        buildProcess.WaitForExit();
        if (buildProcess.ExitCode != 0)
            throw new InvalidOperationException("Failed to build load-gen app");

        _loadGenDll = Path.Combine(_loadGenDir, "bin", "Release", "net10.0", "LoadGen.dll");
        if (!File.Exists(_loadGenDll))
            throw new FileNotFoundException($"LoadGen.dll not found: {_loadGenDll}");

        // Start the live trace
        _tracer = new RuntimeTracer(_loadGenDll, [], static () => { });
        _tracer.Start();

        // Wait for the trace to reach Running state
        var sw2 = Stopwatch.StartNew();
        while (_tracer.ProcessState != TraceProcessState.Running && sw2.Elapsed.TotalSeconds < 15)
            Thread.Sleep(100);

        if (_tracer.ProcessState != TraceProcessState.Running)
            throw new InvalidOperationException(
                $"Tracer did not reach Running state: {_tracer.ProcessState} — {_tracer.ErrorMessage}");

        // Let some events accumulate before benchmarking
        Thread.Sleep(2000);
    }

    /// <summary>
    /// Stops the live tracer and deletes the load-generator workspace.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _tracer.Stop();
        _tracer.Dispose();

        try { if (Directory.Exists(_loadGenDir)) Directory.Delete(_loadGenDir, recursive: true); }
        catch { /* best effort */ }
    }

    /// <summary>
    /// Reads events while the write path is active — measures lock contention on the ring buffer.
    /// </summary>
    [Benchmark(Description = "GetEvents under load (lock contention)")]
    [BenchmarkCategory("Throughput")]
    public IReadOnlyList<TraceEventEntry> GetEvents_UnderLoad()
        => _tracer.GetEvents();

    /// <summary>
    /// Reads the latest counters under load — characterizes the volatile-read path.
    /// </summary>
    [Benchmark(Description = "GetLatestCounters under load (volatile read)")]
    [BenchmarkCategory("Throughput")]
    public CounterSnapshot? GetLatestCounters_UnderLoad()
        => _tracer.GetLatestCounters();

    /// <summary>
    /// Reads the summary under load — measures dictionary copy and aggregation while the writer is active.
    /// </summary>
    [Benchmark(Description = "GetSummary under load (dict copy + aggregation)")]
    [BenchmarkCategory("Throughput")]
    public TraceSummary GetSummary_UnderLoad()
        => _tracer.GetSummary();
}

/// <summary>
/// Write-path benchmarks for <see cref="RuntimeTracer"/> that measure the complete
/// event processing pipeline: EventPipe → CLR event handlers → AddEvent (lock + ring
/// buffer write + summary accumulation) and counter parsing pipeline.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class RuntimeTracerWritePathBenchmarks
{
    private string _helloWorldDll = null!;
    private string _loadGenDll = null!;
    private string _loadGenDir = null!;
    private RuntimeTracer? _lastTracer;

    /// <summary>
    /// Builds HelloWorld for lifecycle runs and a 30-second load-generator for throughput runs.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        // HelloWorld for lifecycle benchmark
        BenchmarkHelpers.BuildSample("samples/HelloWorld");
        _helloWorldDll = BenchmarkHelpers.GetBuildPath("samples/HelloWorld", "HelloWorld.dll");

        // Load-gen for throughput benchmarks
        _loadGenDir = Path.Combine(Path.GetTempPath(), $"dotsider-bench-wp-{Guid.NewGuid().ToString("N")[..8]}");
        Directory.CreateDirectory(_loadGenDir);

        File.WriteAllText(Path.Combine(_loadGenDir, "LoadGen.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
              </PropertyGroup>
            </Project>
            """);

        File.WriteAllText(Path.Combine(_loadGenDir, "Program.cs"), """
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var list = new List<byte[]>();
            int i = 0;
            while (sw.Elapsed.TotalSeconds < 30)
            {
                list.Add(new byte[1024]);
                if (++i % 1000 == 0)
                {
                    list.Clear();
                    GC.Collect(0);
                }
            }
            """);

        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "build -c Release -v q",
            WorkingDirectory = _loadGenDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        var buildProcess = Process.Start(psi)!;
        buildProcess.WaitForExit();
        if (buildProcess.ExitCode != 0)
            throw new InvalidOperationException("Failed to build load-gen app");

        _loadGenDll = Path.Combine(_loadGenDir, "bin", "Release", "net10.0", "LoadGen.dll");
    }

    /// <summary>
    /// Stops and disposes the per-iteration tracer so each run starts from a fresh EventPipe session.
    /// </summary>
    [IterationCleanup]
    public void IterationCleanup()
    {
        _lastTracer?.Stop();
        _lastTracer?.Dispose();
        _lastTracer = null;
    }

    /// <summary>
    /// Ensures any remaining tracer is stopped and removes the load-generator workspace.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _lastTracer?.Stop();
        _lastTracer?.Dispose();

        try { if (Directory.Exists(_loadGenDir)) Directory.Delete(_loadGenDir, recursive: true); }
        catch { /* best effort */ }
    }

    /// <summary>
    /// Drives the full write path: EventPipe reader, CLR event handlers, and ring-buffer writes for two seconds.
    /// </summary>
    [Benchmark(Description = "Event collection throughput (2s trace)")]
    [BenchmarkCategory("WritePath")]
    public int EventCollectionThroughput()
    {
        var tracer = new RuntimeTracer(_loadGenDll, [], static () => { });
        _lastTracer = tracer;
        tracer.Start();

        // Wait for Running state
        var sw = Stopwatch.StartNew();
        while (tracer.ProcessState != TraceProcessState.Running && sw.Elapsed.TotalSeconds < 10)
            Thread.Sleep(50);

        // Collect events for 2 seconds — exercises the full write path:
        // ProcessEventsLoop → CLR event handlers → AddEvent lock + ring buffer write
        Thread.Sleep(2000);
        tracer.Stop();

        return tracer.GetSummary().TotalEvents;
    }

    /// <summary>
    /// Drives the counter pipeline end-to-end: dynamic event handling, counter parsing, and snapshot publication.
    /// </summary>
    [Benchmark(Description = "Counter acquisition pipeline (3s trace)")]
    [BenchmarkCategory("WritePath")]
    public CounterSnapshot? CounterAcquisitionThroughput()
    {
        var tracer = new RuntimeTracer(_loadGenDll, [], static () => { });
        _lastTracer = tracer;
        tracer.Start();

        // Wait for Running state
        var sw = Stopwatch.StartNew();
        while (tracer.ProcessState != TraceProcessState.Running && sw.Elapsed.TotalSeconds < 10)
            Thread.Sleep(50);

        // Counters publish every 1s — collect for 3s to get ~3 snapshots through:
        // HandleDynamicEvent → ParseCounterEvent → BuildCounterSnapshot → Interlocked.Exchange
        Thread.Sleep(3000);
        tracer.Stop();

        return tracer.GetLatestCounters();
    }

    /// <summary>
    /// Measures end-to-end Start/Stop latency including EventPipe connect and shutdown on a short-lived process.
    /// </summary>
    [Benchmark(Description = "Start/Stop lifecycle (EventPipe connect latency)")]
    [BenchmarkCategory("Lifecycle")]
    public int StartStopLifecycle()
    {
        var tracer = new RuntimeTracer(_helloWorldDll, [], static () => { });
        _lastTracer = tracer;
        tracer.Start();

        // Wait for completion — HelloWorld exits immediately
        var sw = Stopwatch.StartNew();
        while (tracer.ProcessState is TraceProcessState.Idle or TraceProcessState.Starting
                   or TraceProcessState.Running
               && sw.Elapsed.TotalSeconds < 15)
        {
            Thread.Sleep(50);
        }

        tracer.Stop();
        return tracer.GetSummary().TotalEvents;
    }
}
