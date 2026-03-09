using System.Text.Json;
using Dotsider.Core.Protocol;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace Dotsider.Mcp.Tools;

/// <summary>
/// MCP tools for discovering and querying running dotsider TUI instances.
/// </summary>
[McpServerToolType]
public sealed partial class SessionTools(DotsiderSessionManager sessionManager, ILogger<SessionTools> logger)
{
    /// <summary>
    /// Discovers running dotsider TUI instances by scanning for active Unix domain sockets.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON array of active sessions, or a message if none are found.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public async partial Task<string> DiscoverDotsiderSessions(CancellationToken ct = default)
    {
        var sessions = sessionManager.DiscoverSessions();
        if (sessions.Count == 0)
            return "No running dotsider instances found.";

        var results = new List<object>();
        foreach (var (pid, socketPath) in sessions)
        {
            var target = sessionManager.GetTarget(pid);
            try
            {
                var response = await target.SendAsync(
                    new DotsiderRequest { Method = "assembly-info" }, ct);
                if (response.Success)
                    results.Add(new { Pid = pid, SocketPath = socketPath, Info = response.Data });
            }
            catch (Exception ex)
            {
                // Unreachable socket — stale file from a crashed instance. Clean it up.
                LogStaleSocket(logger, ex, pid, socketPath);
                try { File.Delete(socketPath); } catch { }
            }
        }

        if (results.Count == 0)
            return "No running dotsider instances found.";

        return JsonSerializer.Serialize(results, DotsiderJsonOptions.Default);
    }

    /// <summary>
    /// Gets detailed info from a running dotsider instance including loaded assembly and current view.
    /// </summary>
    /// <param name="sessionId">PID of the running dotsider instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON with assembly info and current view state.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public async partial Task<string> GetSessionInfo(
        int sessionId,
        CancellationToken ct = default)
    {
        var target = sessionManager.GetTarget(sessionId);
        var info = await target.SendAsync(
            new DotsiderRequest { Method = "assembly-info" }, ct);
        var view = await target.SendAsync(
            new DotsiderRequest { Method = "get-current-view" }, ct);

        return JsonSerializer.Serialize(new { Assembly = info.Data, View = view.Data },
            DotsiderJsonOptions.Default);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Stale socket for PID {Pid} at {SocketPath} — removing")]
    private static partial void LogStaleSocket(ILogger logger, Exception exception, int pid, string socketPath);
}
