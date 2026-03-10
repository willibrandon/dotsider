namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Current state of the traced process lifecycle.
/// </summary>
public enum TraceProcessState
{
    /// <summary>No process is being traced.</summary>
    Idle,

    /// <summary>The trace session is initializing and attaching to the process.</summary>
    Starting,

    /// <summary>The trace session is actively collecting events from the process.</summary>
    Running,

    /// <summary>The traced process has terminated normally.</summary>
    Exited,

    /// <summary>The trace session encountered an error.</summary>
    Error
}
