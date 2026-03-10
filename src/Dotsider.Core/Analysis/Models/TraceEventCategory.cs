namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Category of a traced runtime event, used for coloring in the events table.
/// </summary>
public enum TraceEventCategory
{
    /// <summary>Garbage collection events.</summary>
    GC,

    /// <summary>Just-in-time compilation events.</summary>
    JIT,

    /// <summary>Exception throw and catch events.</summary>
    Exception,

    /// <summary>Assembly and module loader events.</summary>
    Loader,

    /// <summary>Thread pool and synchronization events.</summary>
    Threading,

    /// <summary>HTTP request and response events.</summary>
    Http,

    /// <summary>Socket-level network I/O events.</summary>
    Socket,

    /// <summary>Runtime performance counter snapshots.</summary>
    Counter,

    /// <summary>Events that do not fit any other category.</summary>
    Other
}
