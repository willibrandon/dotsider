using Dotsider.Core.Protocol;
using ModelContextProtocol.Server;

namespace Dotsider.Mcp.Tools;

/// <summary>
/// MCP tools for runtime tracing — requires a running dotsider session with a traced process.
/// </summary>
[McpServerToolType]
public sealed partial class TraceTools(DotsiderSessionManager sessionManager)
{
    /// <summary>
    /// Gets trace events (JIT, GC, exceptions, etc.) from an active trace session.
    /// </summary>
    /// <param name="sessionId">PID of the running dotsider instance.</param>
    /// <param name="categoryFilter">Filter by category (e.g., 'Jit', 'Gc', 'Exception').</param>
    /// <param name="maxResults">Maximum number of events to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON array of trace events.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public async partial Task<string> GetTraceEvents(
        int sessionId,
        string? categoryFilter = null,
        int? maxResults = null,
        CancellationToken ct = default)
    {
        return await sessionManager.GetTarget(sessionId)
            .SendAndUnwrapAsync(new DotsiderRequest { Method = "get-trace-events", CategoryFilter = categoryFilter, MaxResults = maxResults }, ct);
    }

    /// <summary>
    /// Gets the latest performance counter snapshot from a running trace session.
    /// </summary>
    /// <param name="sessionId">PID of the running dotsider instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON with counter values.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public async partial Task<string> GetTraceCounters(
        int sessionId,
        CancellationToken ct = default)
    {
        return await sessionManager.GetTarget(sessionId)
            .SendAndUnwrapAsync(new DotsiderRequest { Method = "get-trace-counters" }, ct);
    }

    /// <summary>
    /// Gets stdout/stderr output captured from the traced process.
    /// </summary>
    /// <param name="sessionId">PID of the running dotsider instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON with process output lines.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public async partial Task<string> GetProcessOutput(
        int sessionId,
        CancellationToken ct = default)
    {
        return await sessionManager.GetTarget(sessionId)
            .SendAndUnwrapAsync(new DotsiderRequest { Method = "get-process-output" }, ct);
    }

    /// <summary>
    /// Starts a trace session to launch and monitor the loaded assembly.
    /// </summary>
    /// <param name="sessionId">PID of the running dotsider instance.</param>
    /// <param name="arguments">Command-line arguments to pass to the traced process.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON confirmation that trace start was queued.</returns>
    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    public async partial Task<string> StartTrace(
        int sessionId,
        string? arguments = null,
        CancellationToken ct = default)
    {
        return await sessionManager.GetTarget(sessionId)
            .SendAndUnwrapAsync(new DotsiderRequest { Method = "start-trace", Arguments = arguments }, ct);
    }

    /// <summary>
    /// Stops the currently running trace session.
    /// </summary>
    /// <param name="sessionId">PID of the running dotsider instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON confirmation that the trace was stopped.</returns>
    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    public async partial Task<string> StopTrace(
        int sessionId,
        CancellationToken ct = default)
    {
        return await sessionManager.GetTarget(sessionId)
            .SendAndUnwrapAsync(new DotsiderRequest { Method = "stop-trace" }, ct);
    }
}
