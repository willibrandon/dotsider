namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Category of a traced runtime event, used for coloring in the events table.
/// </summary>
public enum TraceEventCategory
{
    GC,
    JIT,
    Exception,
    Loader,
    Threading,
    Http,
    Socket,
    Counter,
    Other
}
