using System.Text.Json;
using Dotsider.Core.Protocol;
using ModelContextProtocol.Server;

namespace Dotsider.Mcp.Tools;

/// <summary>
/// MCP tools for native symbols: function names, addresses, and sizes read from a Native AOT
/// binary's PDB, DWARF, or dSYM — or unwind-data boundaries when no symbol file exists.
/// </summary>
[McpServerToolType]
public sealed partial class SymbolTools(DotsiderSessionManager sessionManager)
{
    /// <summary>
    /// Gets a binary's native symbols with their provenance (source, status, symbol file path).
    /// </summary>
    /// <param name="assemblyPath">Path to the binary file.</param>
    /// <param name="sessionId">PID of a running dotsider instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON with the symbol list and its provenance, or an error for managed assemblies.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public async partial Task<string> GetNativeSymbols(
        string? assemblyPath = null,
        int? sessionId = null,
        CancellationToken ct = default)
    {
        if (assemblyPath is not null)
        {
            ToolHelpers.ValidateAssemblyPath(assemblyPath);
            using var analyzer = ToolHelpers.OpenAnalyzer(assemblyPath);
            return analyzer.NativeSymbols is { } info
                ? JsonSerializer.Serialize(info, DotsiderJsonOptions.Default)
                : "Error: managed assembly; no native symbols to read.";
        }

        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value)
                .SendAndUnwrapAsync(new DotsiderRequest { Method = "get-native-symbols" }, ct);
        }

        return "Error: Either assemblyPath or sessionId is required.";
    }
}
