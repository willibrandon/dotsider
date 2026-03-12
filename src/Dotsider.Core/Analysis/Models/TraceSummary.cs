namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Summary statistics aggregated from all collected trace events.
/// </summary>
/// <param name="TotalEvents">Total number of events captured during the trace.</param>
/// <param name="EventsByCategory">Event counts grouped by <see cref="TraceEventCategory"/>.</param>
/// <param name="Duration">Wall-clock duration of the trace session.</param>
/// <param name="PeakWorkingSetMb">Peak process working set in megabytes.</param>
/// <param name="PeakGcHeapMb">Peak GC heap size in megabytes.</param>
/// <param name="TotalExceptions">Total number of exceptions thrown during the trace.</param>
/// <param name="TotalGcCollections">Total number of garbage collections across all generations.</param>
/// <param name="JittedMethodCount">Number of methods JIT-compiled during the trace.</param>
public sealed record TraceSummary(
    int TotalEvents,
    IReadOnlyDictionary<TraceEventCategory, int> EventsByCategory,
    TimeSpan Duration,
    double PeakWorkingSetMb,
    double PeakGcHeapMb,
    long TotalExceptions,
    long TotalGcCollections,
    int JittedMethodCount);
