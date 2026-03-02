namespace Dotsider.Analysis.Models;

/// <summary>
/// Current state of the traced process lifecycle.
/// </summary>
public enum TraceProcessState
{
    Idle,
    Starting,
    Running,
    Exited,
    Error
}
