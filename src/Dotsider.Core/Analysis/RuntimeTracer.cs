using Dotsider.Core.Analysis.Models;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Launches a .NET target and collects its runtime diagnostics.
/// Uses the bundled trace host to keep TraceEvent outside the Native AOT process.
/// Exposes events, counters, output, and lifecycle state to dotsider callers.
/// </summary>
/// <param name="assemblyPath">The managed DLL or executable apphost to launch.</param>
/// <param name="arguments">The literal application arguments to pass to the launched process.</param>
/// <param name="invalidate">The callback that requests a UI refresh.</param>
public sealed class RuntimeTracer(
    string assemblyPath,
    IReadOnlyList<string> arguments,
    Action invalidate) : IDisposable
{
    private const int MaxEvents = 10_000;
    private const int MaxOutputLines = 5_000;
    private const int MinimumTraceHostRuntimeMajor = 10;
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(5);
    private static readonly string[] RequiredTraceHostFiles =
    [
        "dotsider-tracehost.deps.json",
        "dotsider-tracehost.dll",
        "dotsider-tracehost.runtimeconfig.json"
    ];

    private readonly string _assemblyPath = TraceTargetPath.Validate(assemblyPath);
    private readonly string[] _arguments = CopyArguments(arguments);
    private readonly Action _invalidate = invalidate
        ?? throw new ArgumentNullException(nameof(invalidate));

    private readonly TraceEventEntry[] _eventRing = new TraceEventEntry[MaxEvents];
    private readonly Lock _eventLock = new();
    private readonly Dictionary<TraceEventCategory, int> _eventCounts = [];
    private readonly ConcurrentQueue<OutputLine> _outputQueue = new();
    private readonly Lock _stateLock = new();
    private readonly Lock _errorOutputLock = new();
    private readonly StringBuilder _errorOutput = new();

    private Process? _traceHostProcess;
    private CancellationTokenSource? _cts;
    private Task? _messageTask;
    private Task? _errorTask;
    private Stopwatch? _stopwatch;
    private Timer? _invalidateTimer;
    private CounterSnapshot? _latestCounters;
    private TraceProcessState _processState = TraceProcessState.Idle;
    private int? _processId;
    private int? _exitCode;
    private string? _errorMessage;
    private TimeSpan? _finalElapsed;
    private int _eventHead;
    private int _eventCount;
    private int _jittedMethodCount;
    private double _peakWorkingSetMb;
    private double _peakGcHeapMb;
    private int _dirty;
    private int _invalidateTimerCallbackActive;
    private int _disposed;

    /// <summary>The current state of the traced process.</summary>
    public TraceProcessState ProcessState
    {
        get { lock (_stateLock) return _processState; }
    }

    /// <summary>The exit code of the traced process, or null if not yet exited.</summary>
    public int? ExitCode
    {
        get { lock (_stateLock) return _exitCode; }
    }

    /// <summary>The error message if the trace failed, or null.</summary>
    public string? ErrorMessage
    {
        get { lock (_stateLock) return _errorMessage; }
    }

    /// <summary>The OS process ID of the traced process, or null if not started.</summary>
    public int? ProcessId
    {
        get { lock (_stateLock) return _processId; }
    }

    /// <summary>The elapsed time since the trace was started.</summary>
    public TimeSpan Elapsed
    {
        get
        {
            lock (_stateLock)
            {
                return _finalElapsed ?? _stopwatch?.Elapsed ?? TimeSpan.Zero;
            }
        }
    }

    /// <summary>Returns a snapshot of all collected events (copied under lock).</summary>
    public IReadOnlyList<TraceEventEntry> GetEvents()
    {
        lock (_eventLock)
        {
            var count = _eventCount;
            var head = _eventHead;
            var result = new TraceEventEntry[count];
            for (var index = 0; index < count; index++)
            {
                result[index] = _eventRing[((head - count + index) % MaxEvents + MaxEvents) % MaxEvents];
            }

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
        int totalEvents;
        int jitted;
        lock (_eventLock)
        {
            counts = new Dictionary<TraceEventCategory, int>(_eventCounts);
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
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

        lock (_stateLock)
        {
            if (_processState != TraceProcessState.Idle)
            {
                throw new InvalidOperationException("The runtime trace has already been started.");
            }

            _processState = TraceProcessState.Starting;
        }

        _cts = new CancellationTokenSource();
        _stopwatch = Stopwatch.StartNew();
        StartInvalidationTimer();

        try
        {
            _traceHostProcess = Process.Start(CreateTraceHostStartInfo());
            if (_traceHostProcess is null)
            {
                SetError("Failed to start the bundled runtime trace host.");
                return;
            }

            _messageTask = ReadMessagesAsync(
                _traceHostProcess.StandardOutput,
                _traceHostProcess,
                _cts.Token);
            _errorTask = ReadTraceHostErrorsAsync(
                _traceHostProcess.StandardError,
                _cts.Token);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            SetError(exception.Message);
        }

        MarkDirty();
    }

    /// <summary>Stops the traced process and event collection.</summary>
    public void Stop()
    {
        var process = _traceHostProcess;
        if (process is not null)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.StandardInput.WriteLine("stop");
                    process.StandardInput.Flush();
                    if (!process.WaitForExit((int)StopTimeout.TotalMilliseconds))
                    {
                        process.Kill(entireProcessTree: true);
                        process.WaitForExit((int)StopTimeout.TotalMilliseconds);
                    }
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidOperationException)
            {
            }
        }

        _cts?.Cancel();
        var transportTasks = new[] { _messageTask, _errorTask }
            .OfType<Task>()
            .ToArray();
        if (transportTasks.Length > 0)
        {
            Task.WaitAll(transportTasks, StopTimeout);
        }

        StopInvalidationTimer();
        _stopwatch?.Stop();

        bool changed;
        lock (_stateLock)
        {
            changed = _processState is TraceProcessState.Running or TraceProcessState.Starting;
            if (changed)
            {
                _finalElapsed ??= _stopwatch?.Elapsed;
                _processState = TraceProcessState.Exited;
            }
        }

        if (changed)
        {
            _invalidate();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Stop();
        _traceHostProcess?.Dispose();
        _cts?.Dispose();
    }

    internal static string? GetUnavailableReason() =>
        GetUnavailableReason(AppContext.BaseDirectory, ResolveDotNetBasePath());

    internal static string? GetUnavailableReason(
        string baseDirectory,
        string? dotNetBasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);

        if (!TryResolveTraceHostPath(baseDirectory, out _))
        {
            return "The bundled runtime trace host is missing or incomplete. Reinstall dotsider to restore it.";
        }

        if (dotNetBasePath is null
            || ResolveDotNetHostPath(dotNetBasePath) is null
            || !HasCompatibleRuntime(dotNetBasePath))
        {
            return "Dynamic analysis requires the .NET 10 runtime or later. Install .NET, then restart dotsider.";
        }

        return null;
    }

    internal ProcessStartInfo CreateTraceHostStartInfo()
    {
        var hostPath = ResolveTraceHostPath(AppContext.BaseDirectory);
        var startInfo = new ProcessStartInfo
        {
            FileName = ResolveDotNetHost(),
            RedirectStandardError = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("exec");
        startInfo.ArgumentList.Add(hostPath);
        startInfo.ArgumentList.Add(_assemblyPath);
        foreach (var argument in _arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    private static string ResolveTraceHostPath(string baseDirectory)
    {
        if (TryResolveTraceHostPath(baseDirectory, out var traceHostPath))
        {
            return traceHostPath;
        }

        var expectedPath = Path.Combine(
            baseDirectory,
            "tracehost",
            "dotsider-tracehost.dll");
        throw new InvalidOperationException(
            $"The bundled runtime trace host was not found at '{expectedPath}'. Reinstall dotsider to restore it.");
    }

    private static bool TryResolveTraceHostPath(
        string baseDirectory,
        out string traceHostPath)
    {
        var traceHostDirectory = Path.Combine(baseDirectory, "tracehost");
        if (RequiredTraceHostFiles.All(file => File.Exists(Path.Combine(traceHostDirectory, file))))
        {
            traceHostPath = Path.Combine(traceHostDirectory, "dotsider-tracehost.dll");
            return true;
        }

        if (RequiredTraceHostFiles.All(file => File.Exists(Path.Combine(baseDirectory, file))))
        {
            traceHostPath = Path.Combine(baseDirectory, "dotsider-tracehost.dll");
            return true;
        }

        traceHostPath = "";
        return false;
    }

    private static string ResolveDotNetHost()
    {
        var configuredHost = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(configuredHost) && File.Exists(configuredHost))
        {
            return configuredHost;
        }

        var basePath = ResolveDotNetBasePath();
        return basePath is not null
            ? ResolveDotNetHostPath(basePath) ?? "dotnet"
            : "dotnet";
    }

    private static string? ResolveDotNetBasePath()
    {
        var configuredHost = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(configuredHost) && File.Exists(configuredHost))
        {
            var configuredBasePath = Path.GetDirectoryName(configuredHost);
            if (configuredBasePath is not null
                && Directory.Exists(Path.Combine(configuredBasePath, "shared")))
            {
                return configuredBasePath;
            }
        }

        return DotNetRuntimeLocator.FindDotNetBasePath();
    }

    private static string? ResolveDotNetHostPath(string dotNetBasePath)
    {
        var hostName = OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet";
        var hostPath = Path.Combine(dotNetBasePath, hostName);
        return File.Exists(hostPath) ? hostPath : null;
    }

    private static bool HasCompatibleRuntime(string dotNetBasePath)
    {
        var frameworkDirectory = Path.Combine(
            dotNetBasePath,
            "shared",
            "Microsoft.NETCore.App");
        if (!Directory.Exists(frameworkDirectory))
        {
            return false;
        }

        try
        {
            foreach (var directory in Directory.EnumerateDirectories(frameworkDirectory))
            {
                var directoryName = Path.GetFileName(directory);
                var suffixIndex = directoryName.IndexOf('-');
                var versionText = suffixIndex < 0
                    ? directoryName
                    : directoryName[..suffixIndex];
                if (Version.TryParse(versionText, out var version)
                    && version.Major >= MinimumTraceHostRuntimeMajor)
                {
                    return true;
                }
            }
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }

        return false;
    }

    private async Task ReadMessagesAsync(
        StreamReader reader,
        Process process,
        CancellationToken cancellationToken)
    {
        try
        {
            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                var message = JsonSerializer.Deserialize(
                    line,
                    TraceHostJsonContext.Default.TraceHostMessage);
                if (message is null)
                {
                    SetError("The runtime trace host returned an empty protocol message.");
                    return;
                }

                HandleMessage(message);
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            CompleteAfterTraceHostExit(process.ExitCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (JsonException exception)
        {
            SetError($"The runtime trace host returned invalid data: {exception.Message}");
        }
        catch (IOException exception)
        {
            SetError($"The runtime trace host connection failed: {exception.Message}");
        }
    }

    private async Task ReadTraceHostErrorsAsync(
        StreamReader reader,
        CancellationToken cancellationToken)
    {
        try
        {
            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                lock (_errorOutputLock)
                {
                    if (_errorOutput.Length > 0)
                    {
                        _errorOutput.AppendLine();
                    }

                    _errorOutput.Append(line);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (IOException)
        {
        }
    }

    private void HandleMessage(TraceHostMessage message)
    {
        switch (message.Kind)
        {
            case TraceHostMessageKind.Status:
                ApplyStatus(message);
                break;
            case TraceHostMessageKind.Event when message.Event is not null:
                AddEvent(message.Event);
                break;
            case TraceHostMessageKind.Counters when message.Counters is not null:
                ApplyCounters(message.Counters);
                break;
            case TraceHostMessageKind.Output when message.Output is not null:
                AddOutput(message.Output);
                break;
            default:
                SetError($"The runtime trace host returned an incomplete '{message.Kind}' message.");
                break;
        }
    }

    private void ApplyStatus(TraceHostMessage message)
    {
        lock (_stateLock)
        {
            if (message.State is not null)
            {
                _processState = message.State.Value;
            }

            _processId = message.ProcessId ?? _processId;
            _exitCode = message.ExitCode ?? _exitCode;
            _errorMessage = message.Error;
            if (_processState is TraceProcessState.Exited or TraceProcessState.Error)
            {
                _finalElapsed = message.Elapsed ?? _stopwatch?.Elapsed;
                _stopwatch?.Stop();
            }
        }

        MarkDirty();
    }

    private void AddEvent(TraceEventEntry entry)
    {
        lock (_eventLock)
        {
            _eventRing[_eventHead % MaxEvents] = entry;
            _eventHead++;
            if (_eventCount < MaxEvents)
            {
                _eventCount++;
            }

            _eventCounts.TryGetValue(entry.Category, out var count);
            _eventCounts[entry.Category] = count + 1;
            if (entry.Category == TraceEventCategory.JIT)
            {
                _jittedMethodCount++;
            }
        }

        MarkDirty();
    }

    private void ApplyCounters(CounterSnapshot counters)
    {
        Interlocked.Exchange(ref _latestCounters, counters);
        if (counters.WorkingSetMb > _peakWorkingSetMb)
        {
            _peakWorkingSetMb = counters.WorkingSetMb;
        }

        if (counters.GcHeapSizeMb > _peakGcHeapMb)
        {
            _peakGcHeapMb = counters.GcHeapSizeMb;
        }

        MarkDirty();
    }

    private void AddOutput(OutputLine output)
    {
        _outputQueue.Enqueue(output);
        while (_outputQueue.Count > MaxOutputLines)
        {
            _outputQueue.TryDequeue(out _);
        }

        MarkDirty();
    }

    private void CompleteAfterTraceHostExit(int traceHostExitCode)
    {
        lock (_stateLock)
        {
            if (_processState is TraceProcessState.Exited or TraceProcessState.Error)
            {
                return;
            }

            _finalElapsed ??= _stopwatch?.Elapsed;
            _stopwatch?.Stop();

            if (traceHostExitCode == 0)
            {
                _processState = TraceProcessState.Exited;
                return;
            }

            string details;
            lock (_errorOutputLock)
            {
                details = _errorOutput.ToString();
            }

            _errorMessage = string.IsNullOrWhiteSpace(details)
                ? $"The runtime trace host exited with code {traceHostExitCode.ToString(CultureInfo.InvariantCulture)}."
                : details;
            _processState = TraceProcessState.Error;
        }

        MarkDirty();
    }

    private void SetError(string message)
    {
        lock (_stateLock)
        {
            if (_processState == TraceProcessState.Exited)
            {
                return;
            }

            _errorMessage = message;
            _finalElapsed ??= _stopwatch?.Elapsed;
            _stopwatch?.Stop();
            _processState = TraceProcessState.Error;
        }

        MarkDirty();
    }

    private void StartInvalidationTimer()
    {
        _invalidateTimer = new Timer(_ =>
        {
            if (Interlocked.Exchange(ref _invalidateTimerCallbackActive, 1) == 1)
            {
                return;
            }

            try
            {
                if (Interlocked.Exchange(ref _dirty, 0) == 1
                    || ProcessState == TraceProcessState.Running)
                {
                    _invalidate();
                }
            }
            finally
            {
                Volatile.Write(ref _invalidateTimerCallbackActive, 0);
            }
        }, null, 0, 100);
    }

    private void StopInvalidationTimer()
    {
        _invalidateTimer?.Dispose();
        _invalidateTimer = null;
    }

    private void MarkDirty() => Volatile.Write(ref _dirty, 1);

    private static string[] CopyArguments(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var copy = new string[arguments.Count];
        for (var index = 0; index < copy.Length; index++)
        {
            copy[index] = arguments[index]
                ?? throw new ArgumentException(
                    "Trace arguments cannot contain null values.",
                    nameof(arguments));
        }

        return copy;
    }

}
