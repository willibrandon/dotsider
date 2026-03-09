namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Summary statistics aggregated from all collected trace events.
/// </summary>
public sealed record TraceSummary(
    int TotalEvents,
    IReadOnlyDictionary<TraceEventCategory, int> EventsByCategory,
    TimeSpan Duration,
    double PeakWorkingSetMb,
    double PeakGcHeapMb,
    long TotalExceptions,
    long TotalGcCollections,
    int JittedMethodCount);
