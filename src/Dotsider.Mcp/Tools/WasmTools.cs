using Dotsider.Core.Protocol;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Dotsider.Mcp.Tools;

/// <summary>
/// MCP tools for raw .NET WebAssembly runtime modules such as dotnet.native.wasm.
/// Webcil app assemblies are managed metadata and should use the normal assembly and IL tools.
/// These tools expose Wasm sections and function-index inventories for agents and automation.
/// </summary>
[McpServerToolType]
public sealed partial class WasmTools(DotsiderSessionManager sessionManager)
{
    /// <summary>
    /// Lists the sections of a raw WebAssembly module, preserving payload offsets and sizes.
    /// </summary>
    /// <param name="assemblyPath">Path to a raw WebAssembly module.</param>
    /// <param name="sessionId">PID of a running dotsider instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON section table for the raw Wasm module.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public async partial Task<string> ListWasmSections(
        string? assemblyPath = null,
        int? sessionId = null,
        CancellationToken ct = default)
    {
        if (assemblyPath is not null)
        {
            ToolHelpers.ValidateAssemblyPath(assemblyPath);
            using var analyzer = ToolHelpers.OpenAnalyzer(assemblyPath);
            return Serialize(() => WasmPayloadBuilder.BuildSections(analyzer));
        }

        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value)
                .SendAndUnwrapAsync(new DotsiderRequest { Method = "list-wasm-sections" }, ct);
        }

        return "Error: Either assemblyPath or sessionId is required.";
    }

    /// <summary>
    /// Lists imported and defined functions in WebAssembly function-index order.
    /// </summary>
    /// <param name="assemblyPath">Path to a raw WebAssembly module.</param>
    /// <param name="sessionId">PID of a running dotsider instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON function inventory for the raw Wasm module.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public async partial Task<string> ListWasmFunctions(
        string? assemblyPath = null,
        int? sessionId = null,
        CancellationToken ct = default)
    {
        if (assemblyPath is not null)
        {
            ToolHelpers.ValidateAssemblyPath(assemblyPath);
            using var analyzer = ToolHelpers.OpenAnalyzer(assemblyPath);
            return Serialize(() => WasmPayloadBuilder.BuildFunctions(analyzer));
        }

        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value)
                .SendAndUnwrapAsync(new DotsiderRequest { Method = "list-wasm-functions" }, ct);
        }

        return "Error: Either assemblyPath or sessionId is required.";
    }

    private static string Serialize(Func<object> build)
    {
        try
        {
            return McpJson.Serialize(build());
        }
        catch (InvalidOperationException ex)
        {
            throw new McpException(ex.Message);
        }
    }
}
