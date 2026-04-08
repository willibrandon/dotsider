using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

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

    [GlobalSetup]
    public void Setup()
    {
        BenchmarkHelpers.BuildSample("samples/HelloWorld");
        _helloWorldDll = BenchmarkHelpers.GetBuildPath("samples/HelloWorld", "HelloWorld.dll");

        if (!File.Exists(_helloWorldDll))
            throw new FileNotFoundException($"HelloWorld.dll not found: {_helloWorldDll}");

        // Start a real trace — HelloWorld prints and exits quickly, populating ring buffer
        _tracer = new RuntimeTracer(_helloWorldDll, "", static () => { });
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

    [GlobalCleanup]
    public void Cleanup()
    {
        _tracer.Stop();
        _tracer.Dispose();
    }

    [Benchmark(Description = "GetEvents (populated ring buffer)")]
    [BenchmarkCategory("DataRetrieval")]
    public IReadOnlyList<TraceEventEntry> GetEvents_Populated()
        => _tracer.GetEvents();

    [Benchmark(Description = "GetSummary (populated accumulators)")]
    [BenchmarkCategory("DataRetrieval")]
    public TraceSummary GetSummary_Populated()
        => _tracer.GetSummary();

    [Benchmark(Description = "GetOutput (populated queue)")]
    [BenchmarkCategory("DataRetrieval")]
    public IReadOnlyList<OutputLine> GetOutput_Populated()
        => _tracer.GetOutput();

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
        _tracer = new RuntimeTracer(_loadGenDll, "", static () => { });
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

    [GlobalCleanup]
    public void Cleanup()
    {
        _tracer.Stop();
        _tracer.Dispose();

        try { if (Directory.Exists(_loadGenDir)) Directory.Delete(_loadGenDir, recursive: true); }
        catch { /* best effort */ }
    }

    [Benchmark(Description = "GetEvents under load (lock contention)")]
    [BenchmarkCategory("Throughput")]
    public IReadOnlyList<TraceEventEntry> GetEvents_UnderLoad()
        => _tracer.GetEvents();

    [Benchmark(Description = "GetLatestCounters under load (volatile read)")]
    [BenchmarkCategory("Throughput")]
    public CounterSnapshot? GetLatestCounters_UnderLoad()
        => _tracer.GetLatestCounters();

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

    [IterationCleanup]
    public void IterationCleanup()
    {
        _lastTracer?.Stop();
        _lastTracer?.Dispose();
        _lastTracer = null;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _lastTracer?.Stop();
        _lastTracer?.Dispose();

        try { if (Directory.Exists(_loadGenDir)) Directory.Delete(_loadGenDir, recursive: true); }
        catch { /* best effort */ }
    }

    [Benchmark(Description = "Event collection throughput (2s trace)")]
    [BenchmarkCategory("WritePath")]
    public int EventCollectionThroughput()
    {
        var tracer = new RuntimeTracer(_loadGenDll, "", static () => { });
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

    [Benchmark(Description = "Counter acquisition pipeline (3s trace)")]
    [BenchmarkCategory("WritePath")]
    public CounterSnapshot? CounterAcquisitionThroughput()
    {
        var tracer = new RuntimeTracer(_loadGenDll, "", static () => { });
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

    [Benchmark(Description = "Start/Stop lifecycle (EventPipe connect latency)")]
    [BenchmarkCategory("Lifecycle")]
    public int StartStopLifecycle()
    {
        var tracer = new RuntimeTracer(_helloWorldDll, "", static () => { });
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
