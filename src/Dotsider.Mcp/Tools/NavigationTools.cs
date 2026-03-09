using System.Text.Json;
using Dotsider.Core.Protocol;
using ModelContextProtocol.Server;

namespace Dotsider.Mcp.Tools;

/// <summary>
/// MCP tools for navigating and capturing the live dotsider TUI session.
/// </summary>
[McpServerToolType]
public sealed partial class NavigationTools(DotsiderSessionManager sessionManager)
{
    /// <summary>
    /// Gets the current view state including active tab, loaded assembly, and trace state.
    /// </summary>
    /// <param name="sessionId">PID of the running dotsider instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON with tab, sub-tab, assembly path, and tracer state.</returns>
    [McpServerTool]
    public async partial Task<string> GetCurrentView(
        int sessionId,
        CancellationToken ct = default)
    {
        return await sessionManager.GetTarget(sessionId)
            .SendAndUnwrapAsync(new DotsiderRequest { Method = "get-current-view" }, ct);
    }

    /// <summary>
    /// Navigates to a specific tab in the running dotsider TUI.
    /// </summary>
    /// <param name="sessionId">PID of the running dotsider instance.</param>
    /// <param name="tabId">Tab ID (0=General, 1=PE/Metadata, 2=IL, 3=Strings, 4=Deps, 5=Hex, 6=Size, 7=Dynamic).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON confirmation that navigation was queued.</returns>
    [McpServerTool]
    public async partial Task<string> NavigateTo(
        int sessionId,
        int tabId,
        CancellationToken ct = default)
    {
        return await sessionManager.GetTarget(sessionId)
            .SendAndUnwrapAsync(new DotsiderRequest { Method = "navigate", TabId = tabId }, ct);
    }

    /// <summary>
    /// Captures the current TUI screen via the hex1b diagnostics socket.
    /// </summary>
    /// <param name="sessionId">PID of the running dotsider instance.</param>
    /// <param name="format">Output format: text, ansi, html, or svg (default: text).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Screen capture content in the requested format.</returns>
    [McpServerTool]
    public async partial Task<string> CaptureScreen(
        int sessionId,
        string format = "text",
        CancellationToken ct = default)
    {
        var hex1bSocket = DotsiderSessionManager.GetHex1bSocketPath(sessionId);
        if (!File.Exists(hex1bSocket))
            return $"Error: No hex1b socket found for PID {sessionId}";

        var target = sessionManager.GetTarget(sessionId);
        var requestJson = JsonSerializer.Serialize(
            new { method = "capture", format }, DotsiderJsonOptions.Default);

        var responseJson = await target.SendRawAsync(hex1bSocket, requestJson, ct);
        var response = JsonSerializer.Deserialize<JsonElement>(responseJson);

        if (response.TryGetProperty("success", out var success) && success.GetBoolean()
            && response.TryGetProperty("data", out var data))
        {
            return data.GetString() ?? "";
        }

        var error = response.TryGetProperty("error", out var err) ? err.GetString() : "Unknown error";
        return $"Error: {error}";
    }
}
