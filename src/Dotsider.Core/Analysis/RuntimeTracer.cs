using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Globalization;
using Dotsider.Core.Analysis.Models;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using TraceEventCategory = Dotsider.Core.Analysis.Models.TraceEventCategory;
using TraceEventEntry = Dotsider.Core.Analysis.Models.TraceEventEntry;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Manages launching a .NET assembly as a child process and collecting
/// runtime events via EventPipe diagnostics (PID-based connect with retry).
/// </summary>
public sealed class RuntimeTracer(string assemblyPath, string arguments, Action invalidate) : IDisposable
{
    private const int MaxEvents = 10_000;
    private const int MaxOutputLines = 5_000;
    private const int MaxConnectRetries = 25;        // 25 × 200ms = 5s max wait
    private const int ConnectRetryDelayMs = 200;

    private readonly bool _isExe = assemblyPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

    private Process? _process;
    private EventPipeSession? _session;
    private EventPipeEventSource? _eventSource;
    private Task? _processingTask;
    private CancellationTokenSource? _cts;
    private Stopwatch? _stopwatch;
    private Timer? _invalidateTimer;

    // Ring buffer for events — lock protects both read and write
    private readonly TraceEventEntry[] _eventRing = new TraceEventEntry[MaxEvents];
    private int _eventHead;
    private int _eventCount;
    private readonly Lock _eventLock = new();

    // Counter snapshot — atomic via Interlocked.Exchange
    private CounterSnapshot? _latestCounters;
    
    // Workaround the analyzer's unsupported preview feature recommendation
    #pragma warning disable IDE0028
    private readonly Dictionary<string, double> _counterAccumulators = new(StringComparer.OrdinalIgnoreCase);
    #pragma warning restore IDE0028

    // Summary accumulators (written under _eventLock for simplicity)
    private readonly Dictionary<TraceEventCategory, int> _eventCounts = [];
    private int _jittedMethodCount;
    private double _peakWorkingSetMb;
    private double _peakGcHeapMb;

    // Dirty flag for throttled invalidation
    private int _dirty;

    // Process output
    private readonly ConcurrentQueue<OutputLine> _outputQueue = new();

    // --- Synchronized public state ---
    // ProcessState, ExitCode, and ErrorMessage are read from the UI thread
    // and written from up to 3 threads (Process.Exited handler, EventPipe
    // background task, Stop/Dispose). A single lock synchronizes both reads
    // and writes so that observers always see consistent state (e.g. ExitCode
    // is set before ProcessState transitions to Exited).
    private readonly Lock _stateLock = new();
    private TraceProcessState _processState = TraceProcessState.Idle;
    private int? _exitCode;
    private string? _errorMessage;

    /// <summary>The current state of the traced process.</summary>
    public TraceProcessState ProcessState
    {
        get { lock (_stateLock) return _processState; }
        private set { lock (_stateLock) _processState = value; }
    }

    /// <summary>The exit code of the traced process, or null if not yet exited.</summary>
    public int? ExitCode
    {
        get { lock (_stateLock) return _exitCode; }
        private set { lock (_stateLock) _exitCode = value; }
    }

    /// <summary>The error message if the trace failed, or null.</summary>
    public string? ErrorMessage
    {
        get { lock (_stateLock) return _errorMessage; }
        private set { lock (_stateLock) _errorMessage = value; }
    }

    /// <summary>The OS process ID of the traced process, or null if not started.</summary>
    public int? ProcessId => _process?.Id;

    /// <summary>The elapsed time since the trace was started.</summary>
    public TimeSpan Elapsed => _stopwatch?.Elapsed ?? TimeSpan.Zero;

    /// <summary>Returns a snapshot of all collected events (copied under lock).</summary>
    public IReadOnlyList<TraceEventEntry> GetEvents()
    {
        lock (_eventLock)
        {
            var count = _eventCount;
            var head = _eventHead;
            var result = new TraceEventEntry[count];
            for (var i = 0; i < count; i++)
                result[i] = _eventRing[((head - count + i) % MaxEvents + MaxEvents) % MaxEvents];
            return result;
        }
    }

    /// <summary>Returns the most recent counter snapshot, or null.</summary>
    public CounterSnapshot? GetLatestCounters() => Volatile.Read(ref _latestCounters);

    /// <summary>Returns process output lines.</summary>
    public IReadOnlyList<OutputLine> GetOutput() => [.. _outputQueue];

    /// <summary>Returns aggregated summary statistics.</summary>
    public TraceSummary GetSummary()
    {
        Dictionary<TraceEventCategory, int> counts;
        int totalEvents, jitted;
        lock (_eventLock)
        {
            // Workaround the analyzer's unsupported preview feature recommendation
            #pragma warning disable IDE0028
            counts = new Dictionary<TraceEventCategory, int>(_eventCounts);
            #pragma warning restore IDE0028
            totalEvents = counts.Values.Sum();
            jitted = _jittedMethodCount;
        }

        var counters = GetLatestCounters();
        var exceptionEvents = counts.GetValueOrDefault(TraceEventCategory.Exception);
        return new TraceSummary(
            totalEvents,
            counts,
            Elapsed,
            _peakWorkingSetMb,
            _peakGcHeapMb,
            exceptionEvents,
            (counters?.Gen0Collections ?? 0) + (counters?.Gen1Collections ?? 0) + (counters?.Gen2Collections ?? 0),
            jitted);
    }

    /// <summary>Launches the target process and starts collecting events.</summary>
    public void Start()
    {
        _cts = new CancellationTokenSource();
        _stopwatch = Stopwatch.StartNew();

        // Launch with diagnostic port suspend so we can attach EventPipe
        // before Main() runs — this captures events even for short-lived processes
        var psi = new ProcessStartInfo
        {
            FileName = _isExe ? assemblyPath : "dotnet",
            Arguments = _isExe ? arguments : $"exec \"{assemblyPath}\" {arguments}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.Environment["DOTNET_DefaultDiagnosticPortSuspend"] = "1";
        _process = Process.Start(psi);

        if (_process is null)
        {
            lock (_stateLock)
            {
                _errorMessage = "Failed to start process";
                _processState = TraceProcessState.Error;
            }

            MarkDirty();
            return;
        }

        // Capture stdout/stderr on background threads
        var startTime = _stopwatch;
        Task.Run(() => ReadOutput(_process.StandardOutput, false, startTime));
        Task.Run(() => ReadOutput(_process.StandardError, true, startTime));

        // Handle process exit
        _process.EnableRaisingEvents = true;
        _process.Exited += (_, _) =>
        {
            bool transitioned;
            lock (_stateLock)
            {
                transitioned = _processState == TraceProcessState.Running;
                if (transitioned)
                {
                    _exitCode = _process.HasExited ? _process.ExitCode : null;
                    _processState = TraceProcessState.Exited;
                    invalidate();
                }
            }

            if (transitioned)
            {
                _stopwatch?.Stop();
                // StopProcessing unblocks source.Process() synchronously.
                // session.Stop() can deadlock on Windows when the pipe is
                // already broken, so we only use the TraceEventSource path.
                _eventSource?.StopProcessing();
            }
        };

        lock (_stateLock) _processState = TraceProcessState.Starting;
        MarkDirty();

        // Invalidation timer (100ms interval). While the process is Running,
        // always invalidate so the elapsed time display stays current.
        // Otherwise, only invalidate when the dirty flag is set.
        _invalidateTimer = new Timer(_ =>
        {
            if (Interlocked.Exchange(ref _dirty, 0) == 1
                || ProcessState == TraceProcessState.Running)
                invalidate();
        }, null, 0, 100);

        // Connection + event processing on background task.
        // The process is suspended (DOTNET_DefaultDiagnosticPortSuspend=1)
        // so it won't exit while we're connecting.
        var providers = BuildProviders();
        var pid = _process.Id;
        _processingTask = Task.Run(async () =>
        {
            try
            {
                // Retry connecting — the diagnostic IPC endpoint may not be
                // available immediately after process start
                DiagnosticsClient? client = null;
                EventPipeSession? session = null;

                for (var attempt = 0; attempt < MaxConnectRetries; attempt++)
                {
                    _cts.Token.ThrowIfCancellationRequested();

                    try
                    {
                        client = new DiagnosticsClient(pid);
                        session = client.StartEventPipeSession(providers, requestRundown: false);
                        break; // connected
                    }
                    catch (ServerNotAvailableException)
                    {
                        // Runtime diagnostic pipe not ready yet — wait and retry
                        await Task.Delay(ConnectRetryDelayMs, _cts.Token);
                    }
                    catch (EndOfStreamException)
                    {
                        await Task.Delay(ConnectRetryDelayMs, _cts.Token);
                    }
                }

                if (session is null)
                {
                    lock (_stateLock)
                    {
                        _errorMessage = "Timed out connecting to runtime diagnostics (5s). Is this a valid .NET assembly?";
                        _processState = TraceProcessState.Error;
                    }

                    // Kill suspended process so it doesn't hang
                    try { _process.Kill(); } catch { }
                    return;
                }

                _session = session;
                lock (_stateLock) _processState = TraceProcessState.Running;
                MarkDirty();

                // Resume the suspended runtime now that EventPipe is attached
                client!.ResumeRuntime();

                // Process events — blocks until session ends
                ProcessEventsLoop(session);
            }
            catch (OperationCanceledException) { /* user cancelled */ }
            catch (Exception ex) when (ex is EndOfStreamException or IOException or ObjectDisposedException)
            {
                // Expected: process exited (pipe broke) or user cancelled
                lock (_stateLock)
                {
                    if (_processState is TraceProcessState.Running or TraceProcessState.Starting)
                    {
                        _exitCode = _process?.HasExited == true ? _process.ExitCode : null;
                        _processState = TraceProcessState.Exited;
                    }
                }
            }
            catch (Exception ex)
            {
                lock (_stateLock)
                {
                    _errorMessage = ex.Message;
                    _processState = TraceProcessState.Error;
                }
            }
            finally
            {
                _stopwatch?.Stop();
                invalidate();
            }
        });
    }

    /// <summary>Stops the traced process and event collection.</summary>
    public void Stop()
    {
        _cts?.Cancel();
        _eventSource?.StopProcessing();
        try { _session?.Stop(); } catch { }

        if (_process is { HasExited: false } p)
        {
            // Process.Kill() sends SIGKILL on Unix, TerminateProcess on Windows.
            // Both are immediate and forceful. There is no cross-platform
            // graceful shutdown for arbitrary console processes.
            try { p.Kill(entireProcessTree: true); } catch { }
            try { p.WaitForExit(5000); } catch { }
        }

        _invalidateTimer?.Dispose();
        _invalidateTimer = null;
        _stopwatch?.Stop();

        lock (_stateLock)
        {
            if (_processState is TraceProcessState.Running or TraceProcessState.Starting)
            {
                _exitCode = _process?.HasExited == true ? _process.ExitCode : null;
                _processState = TraceProcessState.Exited;
                invalidate();
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Stop();
        _process?.Dispose();
        _cts?.Dispose();
    }

    // --- Private implementation ---

    private void MarkDirty() => Volatile.Write(ref _dirty, 1);

    private static List<EventPipeProvider> BuildProviders() =>
    [
        // Verbose level is required for MethodJittingStarted events (which
        // are classified as Verbose in the CLR provider).  The keyword mask
        // still limits the event categories, so only GC/JIT/Exception/Loader/
        // Threading events are delivered — Verbose just unlocks the JIT ones.
        new("Microsoft-Windows-DotNETRuntime",
            EventLevel.Verbose,
            (long)(ClrTraceEventParser.Keywords.GC          // 0x1
                 | ClrTraceEventParser.Keywords.Jit         // 0x10
                 | ClrTraceEventParser.Keywords.Exception   // 0x8000
                 | ClrTraceEventParser.Keywords.Loader      // 0x8
                 | ClrTraceEventParser.Keywords.Threading)), // 0x10000

        new("System.Runtime",
            EventLevel.Informational, 0L,
            new Dictionary<string, string> { ["EventCounterIntervalSec"] = "1" }),

        new("System.Net.Http",
            EventLevel.Informational, long.MaxValue),

        new("System.Net.Sockets",
            EventLevel.Informational, long.MaxValue)
    ];

    private void ProcessEventsLoop(EventPipeSession session)
    {
        using var source = new EventPipeEventSource(session.EventStream);
        _eventSource = source;

        // CLR events
        source.Clr.GCStart += data =>
            AddEvent(TraceEventCategory.GC, "GCStart", $"Gen {data.Depth}, Reason: {data.Reason}");

        source.Clr.GCStop += data =>
            AddEvent(TraceEventCategory.GC, "GCStop", $"Gen {data.Depth}");

        source.Clr.GCHeapStats += data =>
            AddEvent(TraceEventCategory.GC, "GCHeapStats",
                $"Gen0: {data.GenerationSize0 / 1024}KB, Gen1: {data.GenerationSize1 / 1024}KB, Gen2: {data.GenerationSize2 / 1024}KB");

        source.Clr.MethodJittingStarted += data =>
        {
            lock (_eventLock) _jittedMethodCount++;
            AddEvent(TraceEventCategory.JIT, "MethodJitting",
                $"{data.MethodNamespace}.{data.MethodName}",
                data.MethodToken);
        };

        source.Clr.ExceptionStart += data =>
            AddEvent(TraceEventCategory.Exception, "ExceptionThrown", data.ExceptionType);

        source.Clr.LoaderAssemblyLoad += data =>
            AddEvent(TraceEventCategory.Loader, "AssemblyLoad",
                TruncateAssemblyName(data.FullyQualifiedAssemblyName));

        source.Clr.ThreadPoolWorkerThreadStart += data =>
            AddEvent(TraceEventCategory.Threading, "ThreadPoolStart",
                $"Active: {data.ActiveWorkerThreadCount}");

        // Dynamic events (counters, HTTP, sockets)
        source.Dynamic.All += HandleDynamicEvent;

        try { source.Process(); }
        catch (Exception ex) when (ex is EndOfStreamException or IOException or ObjectDisposedException)
        {
            // Expected: process exited, pipe broke
        }
        finally
        {
            _eventSource = null;
        }
    }

    private void HandleDynamicEvent(Microsoft.Diagnostics.Tracing.TraceEvent data)
    {
        if (data.ProviderName == "System.Runtime" && data.EventName == "EventCounters")
        {
            var snapshot = ParseCounterEvent(data);
            if (snapshot != null)
            {
                Interlocked.Exchange(ref _latestCounters, snapshot);
                if (snapshot.WorkingSetMb > _peakWorkingSetMb)
                    _peakWorkingSetMb = snapshot.WorkingSetMb;
                if (snapshot.GcHeapSizeMb > _peakGcHeapMb)
                    _peakGcHeapMb = snapshot.GcHeapSizeMb;
            }
            return;
        }

        if (data.ProviderName == "System.Net.Http")
        {
            AddEvent(TraceEventCategory.Http, data.EventName, data.ToString());
            return;
        }

        if (data.ProviderName == "System.Net.Sockets")
        {
            AddEvent(TraceEventCategory.Socket, data.EventName, data.ToString());
        }
    }

    // The System.Runtime EventCounters payload structure:
    //   data.PayloadByName("Payload") → IDictionary<string, object>
    //   Inside: "Name" (string), "DisplayName" (string),
    //           "Mean" (double, for gauge counters like cpu-usage)
    //           "Increment" (double, for rate counters like gen-0-gc-count)
    //           "CounterType" → "Mean" or "Sum"
    //
    // IMPORTANT: Counter names and payload shape vary across .NET versions.
    // Be defensive: use TryGetValue, default to 0 for missing counters,
    // don't crash on unexpected names. Known counters:
    //   cpu-usage, working-set, gc-heap-size, gen-0-gc-count,
    //   gen-1-gc-count, gen-2-gc-count, threadpool-thread-count,
    //   threadpool-queue-length, exception-count, active-timer-count
    // Some of these may not exist on older runtimes.
    // working-set and gc-heap-size are reported in MB on modern runtimes.
    private CounterSnapshot? ParseCounterEvent(Microsoft.Diagnostics.Tracing.TraceEvent data)
    {
        // EventCounters have a NESTED payload structure:
        //   PayloadValue(0) → outer IDictionary with a "Payload" key
        //   outer["Payload"] → inner IDictionary with Name, CounterType, Mean/Increment
        if (data.PayloadValue(0) is not IDictionary<string, object> outerPayload)
            return BuildCounterSnapshot();

        if (!outerPayload.TryGetValue("Payload", out var innerObj)
            || innerObj is not IDictionary<string, object> payload)
            return BuildCounterSnapshot();

        if (!payload.TryGetValue("Name", out var counterNameRaw) || counterNameRaw is not string counterName)
            return BuildCounterSnapshot();

        if (!payload.TryGetValue("CounterType", out var counterTypeRaw) || counterTypeRaw is not string counterType)
            return BuildCounterSnapshot();

        if (counterType.Equals("Mean", StringComparison.OrdinalIgnoreCase))
        {
            if (TryGetDouble(payload, "Mean", out var meanValue))
                _counterAccumulators[counterName] = meanValue;
        }
        else if (counterType.Equals("Sum", StringComparison.OrdinalIgnoreCase))
        {
            if (TryGetDouble(payload, "Increment", out var incrementValue))
                _counterAccumulators[counterName] = ReadCounter(counterName) + incrementValue;
        }

        return BuildCounterSnapshot();
    }

    private CounterSnapshot BuildCounterSnapshot()
    {
        var gen0Collections = ToNonNegativeLong(ReadCounter("gen-0-gc-count"));
        var gen1Collections = ToNonNegativeLong(ReadCounter("gen-1-gc-count"));
        var gen2Collections = ToNonNegativeLong(ReadCounter("gen-2-gc-count"));
        var threadPoolThreadCount = ToNonNegativeInt(ReadCounter("threadpool-thread-count"));
        var threadPoolQueueLength = ToNonNegativeLong(ReadCounter("threadpool-queue-length"));
        var exceptionCount = ToNonNegativeLong(ReadCounter("exception-count"));
        var activeTimerCount = ToNonNegativeLong(ReadCounter("active-timer-count"));

        return new CounterSnapshot(
            Elapsed,
            CpuUsagePercent: ReadCounter("cpu-usage"),
            WorkingSetMb: ReadCounter("working-set"),
            GcHeapSizeMb: ReadCounter("gc-heap-size"),
            Gen0Collections: gen0Collections,
            Gen1Collections: gen1Collections,
            Gen2Collections: gen2Collections,
            ThreadPoolThreadCount: threadPoolThreadCount,
            ThreadPoolQueueLength: threadPoolQueueLength,
            ExceptionCount: exceptionCount,
            ActiveTimerCount: activeTimerCount);
    }

    private double ReadCounter(string name) =>
        _counterAccumulators.TryGetValue(name, out var value) ? value : 0d;

    private static bool TryGetDouble(IDictionary<string, object> payload, string key, out double value)
    {
        value = 0d;
        if (!payload.TryGetValue(key, out var raw) || raw is null)
            return false;

        switch (raw)
        {
            case double d:
                value = d;
                return true;
            case float f:
                value = f;
                return true;
            case int i:
                value = i;
                return true;
            case long l:
                value = l;
                return true;
            case decimal m:
                value = (double)m;
                return true;
            case string s when double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed):
                value = parsed;
                return true;
            default:
                return false;
        }
    }

    private static long ToNonNegativeLong(double value)
    {
        if (value <= 0) return 0;
        return (long)Math.Round(value, MidpointRounding.AwayFromZero);
    }

    private static int ToNonNegativeInt(double value)
    {
        if (value <= 0) return 0;
        return (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }

    private void AddEvent(TraceEventCategory category, string eventName, string detail,
        int metadataToken = 0)
    {
        var entry = new TraceEventEntry(Elapsed, category, eventName, detail, metadataToken);
        lock (_eventLock)
        {
            _eventRing[_eventHead % MaxEvents] = entry;
            _eventHead++;
            if (_eventCount < MaxEvents) _eventCount++;

            _eventCounts.TryGetValue(category, out var count);
            _eventCounts[category] = count + 1;
        }
        MarkDirty();
    }

    private void ReadOutput(StreamReader reader, bool isStdErr, Stopwatch timer)
    {
        try
        {
            while (reader.ReadLine() is { } line)
            {
                _outputQueue.Enqueue(new OutputLine(timer.Elapsed, isStdErr, line));
                while (_outputQueue.Count > MaxOutputLines)
                    _outputQueue.TryDequeue(out _);
                MarkDirty();
            }
        }
        catch (Exception) { /* stream closed */ }
    }

    private static string TruncateAssemblyName(string name)
    {
        var comma = name.IndexOf(',');
        return comma > 0 ? name[..comma] : name;
    }
}
