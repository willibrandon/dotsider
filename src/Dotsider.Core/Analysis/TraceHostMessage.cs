using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Carries one state change or data item from the runtime trace host.
/// Uses a fixed source-generated JSON shape for Native AOT compatibility.
/// Populates only the value associated with the selected message kind.
/// </summary>
internal sealed record TraceHostMessage(
    TraceHostMessageKind Kind,
    TraceProcessState? State = null,
    int? ProcessId = null,
    int? ExitCode = null,
    string? Error = null,
    TimeSpan? Elapsed = null,
    TraceEventEntry? Event = null,
    CounterSnapshot? Counters = null,
    OutputLine? Output = null);
