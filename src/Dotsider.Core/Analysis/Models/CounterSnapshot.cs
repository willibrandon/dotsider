namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// A snapshot of runtime performance counters at a point in time.
/// </summary>
/// <param name="Timestamp">Elapsed time since the trace started.</param>
/// <param name="CpuUsagePercent">CPU usage as a percentage (0–100).</param>
/// <param name="WorkingSetMb">Process working set in megabytes.</param>
/// <param name="GcHeapSizeMb">GC heap size in megabytes.</param>
/// <param name="Gen0Collections">Cumulative generation 0 collection count.</param>
/// <param name="Gen1Collections">Cumulative generation 1 collection count.</param>
/// <param name="Gen2Collections">Cumulative generation 2 collection count.</param>
/// <param name="ThreadPoolThreadCount">Number of active thread pool threads.</param>
/// <param name="ThreadPoolQueueLength">Number of work items queued to the thread pool.</param>
/// <param name="ExceptionCount">Cumulative exception count.</param>
/// <param name="ActiveTimerCount">Number of active timers.</param>
public sealed record CounterSnapshot(
    TimeSpan Timestamp,
    double CpuUsagePercent,
    double WorkingSetMb,
    double GcHeapSizeMb,
    long Gen0Collections,
    long Gen1Collections,
    long Gen2Collections,
    int ThreadPoolThreadCount,
    long ThreadPoolQueueLength,
    long ExceptionCount,
    long ActiveTimerCount);
