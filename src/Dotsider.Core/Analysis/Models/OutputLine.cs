namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// A line of output captured from the traced process's stdout or stderr.
/// </summary>
/// <param name="Timestamp">Elapsed time since the trace started.</param>
/// <param name="IsStdErr">Whether the line came from stderr rather than stdout.</param>
/// <param name="Text">The captured text content.</param>
public sealed record OutputLine(
    TimeSpan Timestamp,
    bool IsStdErr,
    string Text);
