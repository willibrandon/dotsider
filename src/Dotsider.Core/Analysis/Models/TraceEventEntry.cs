namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// A single traced runtime event captured from the EventPipe session.
/// </summary>
/// <param name="Timestamp">Elapsed time since the trace started.</param>
/// <param name="Category">The event category (JIT, GC, Loader, etc.).</param>
/// <param name="EventName">Name of the event (e.g., <c>MethodJittingStarted</c>).</param>
/// <param name="Detail">Human-readable description of the event payload.</param>
/// <param name="MetadataToken">Metadata token associated with the event, or 0 if not applicable.</param>
public sealed record TraceEventEntry(
    TimeSpan Timestamp,
    TraceEventCategory Category,
    string EventName,
    string Detail,
    int MetadataToken = 0);
