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
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
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
    /// <param name="tabId">Tab number (1=General, 2=PE/Metadata, 3=IL / Native, 4=Strings, 5=Hex Dump, 6=Dep Graph, 7=Size Map, 8=Dynamic).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON confirmation that navigation was queued.</returns>
    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    public async partial Task<string> NavigateTo(
        int sessionId,
        int tabId,
        CancellationToken ct = default)
    {
        return await sessionManager.GetTarget(sessionId)
            .SendAndUnwrapAsync(new DotsiderRequest { Method = "navigate", TabId = tabId }, ct);
    }

    /// <summary>
    /// Captures the current TUI screen as plain text via the hex1b diagnostics socket.
    /// </summary>
    /// <param name="sessionId">PID of the running dotsider instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Plain text screen capture of the current TUI view.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public static async partial Task<string> CaptureScreen(
        int sessionId,
        CancellationToken ct = default)
    {
        var hex1bSocket = DotsiderSessionManager.GetHex1bSocketPath(sessionId);
        if (!File.Exists(hex1bSocket))
            return $"Error: No hex1b socket found for PID {sessionId}";

        var requestJson = JsonSerializer.Serialize(
            new { method = "capture", format = "text" }, DotsiderJsonOptions.Default);

        var responseJson = await RemoteDotsiderTarget.SendRawAsync(hex1bSocket, requestJson, ct);
        var response = JsonSerializer.Deserialize<JsonElement>(responseJson);

        if (response.TryGetProperty("success", out var success) && success.GetBoolean()
            && response.TryGetProperty("data", out var data))
        {
            return data.GetString() ?? "";
        }

        var error = response.TryGetProperty("error", out var err) ? err.GetString() : "Unknown error";
        return $"Error: {error}";
    }

    /// <summary>
    /// Navigates to the definition of a metadata token in tab 3's IL view.
    /// Works for method calls, field accesses, type references, and other
    /// token-bearing IL instructions. Cross-assembly navigation is supported.
    /// </summary>
    /// <param name="sessionId">PID of the running dotsider instance.</param>
    /// <param name="token">The metadata token from the IL instruction operand.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON confirmation that navigation was queued.</returns>
    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    public async partial Task<string> NavigateToIlDefinition(
        int sessionId,
        int token,
        CancellationToken ct = default)
    {
        return await sessionManager.GetTarget(sessionId)
            .SendAndUnwrapAsync(new DotsiderRequest
            {
                Method = "navigate-to-il-definition",
                Token = token
            }, ct);
    }

    /// <summary>
    /// Goes back in the navigation history, following the same priority as
    /// pressing Escape in the TUI: IL back stack, cross-view back, assembly
    /// stack pop, then IL selection clear.
    /// </summary>
    /// <param name="sessionId">PID of the running dotsider instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON confirmation that back navigation was queued.</returns>
    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    public async partial Task<string> NavigateBack(
        int sessionId,
        CancellationToken ct = default)
    {
        return await sessionManager.GetTarget(sessionId)
            .SendAndUnwrapAsync(new DotsiderRequest { Method = "navigate-back" }, ct);
    }

    /// <summary>
    /// Opens a dependency assembly by explicit path or by resolving an assembly name
    /// using the current analyzer's context (target framework, runtime pack, bundle).
    /// When both are provided, the explicit path takes precedence.
    /// </summary>
    /// <param name="sessionId">PID of the running dotsider instance.</param>
    /// <param name="assemblyPath">Explicit path to the assembly to open.</param>
    /// <param name="assemblyName">Assembly name to resolve (e.g. "System.Runtime").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON confirmation that the push was queued.</returns>
    [McpServerTool(ReadOnly = false, Destructive = false, OpenWorld = false)]
    public async partial Task<string> PushAssembly(
        int sessionId,
        string? assemblyPath = null,
        string? assemblyName = null,
        CancellationToken ct = default)
    {
        return await sessionManager.GetTarget(sessionId)
            .SendAndUnwrapAsync(new DotsiderRequest
            {
                Method = "push-assembly",
                AssemblyPath = assemblyPath,
                AssemblyName = assemblyName
            }, ct);
    }
}
