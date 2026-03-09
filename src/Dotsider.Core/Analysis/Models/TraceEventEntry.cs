namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// A single traced runtime event captured from the EventPipe session.
/// </summary>
public sealed record TraceEventEntry(
    TimeSpan Timestamp,
    TraceEventCategory Category,
    string EventName,
    string Detail,
    int MetadataToken = 0);
