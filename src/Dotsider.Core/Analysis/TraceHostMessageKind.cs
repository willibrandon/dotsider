namespace Dotsider.Core.Analysis;

/// <summary>
/// Identifies a message emitted by the runtime trace host.
/// Distinguishes lifecycle updates from events, counters, and process output.
/// Allows the Native AOT client to validate each protocol payload explicitly.
/// </summary>
internal enum TraceHostMessageKind
{
    Status,
    Event,
    Counters,
    Output
}
