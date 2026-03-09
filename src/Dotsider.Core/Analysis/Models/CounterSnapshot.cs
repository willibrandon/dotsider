namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// A snapshot of runtime performance counters at a point in time.
/// </summary>
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
