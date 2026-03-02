namespace Dotsider.Analysis.Models;

/// <summary>
/// A line of output captured from the traced process's stdout or stderr.
/// </summary>
public sealed record OutputLine(
    TimeSpan Timestamp,
    bool IsStdErr,
    string Text);
