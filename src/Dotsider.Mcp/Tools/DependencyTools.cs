using System.Text.Json;
using Dotsider.Core.Analysis;
using Dotsider.Core.Protocol;
using ModelContextProtocol.Server;

namespace Dotsider.Mcp.Tools;

/// <summary>
/// MCP tools for assembly reference analysis, dependency graphs, and type reference inspection.
/// </summary>
[McpServerToolType]
public sealed partial class DependencyTools(DotsiderSessionManager sessionManager)
{
    /// <summary>
    /// Gets the list of assembly references including name, version, culture, and public key token.
    /// </summary>
    /// <param name="assemblyPath">Path to assembly file.</param>
    /// <param name="sessionId">PID of a running dotsider instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON array of assembly references.</returns>
    [McpServerTool]
    public async partial Task<string> GetAssemblyRefs(
        string? assemblyPath = null,
        int? sessionId = null,
        CancellationToken ct = default)
    {
        if (assemblyPath is not null)
        {
            using var analyzer = new AssemblyAnalyzer(assemblyPath);
            return JsonSerializer.Serialize(analyzer.AssemblyRefs, DotsiderJsonOptions.Default);
        }

        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value)
                .SendAndUnwrapAsync(new DotsiderRequest { Method = "get-assembly-refs" }, ct);
        }

        return "Error: Either assemblyPath or sessionId is required.";
    }

    /// <summary>
    /// Builds the full dependency graph with nodes and edges for visualization.
    /// </summary>
    /// <param name="assemblyPath">Path to assembly file.</param>
    /// <param name="sessionId">PID of a running dotsider instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON with Nodes and Edges arrays.</returns>
    [McpServerTool]
    public async partial Task<string> GetDependencyGraph(
        string? assemblyPath = null,
        int? sessionId = null,
        CancellationToken ct = default)
    {
        if (assemblyPath is not null)
        {
            using var analyzer = new AssemblyAnalyzer(assemblyPath);
            var (nodes, edges) = DependencyGraphBuilder.Build(analyzer);
            return JsonSerializer.Serialize(new { Nodes = nodes, Edges = edges },
                DotsiderJsonOptions.Default);
        }

        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value)
                .SendAndUnwrapAsync(new DotsiderRequest { Method = "get-dependency-graph" }, ct);
        }

        return "Error: Either assemblyPath or sessionId is required.";
    }

    /// <summary>
    /// Gets type references — types imported from other assemblies.
    /// </summary>
    /// <param name="assemblyPath">Path to assembly file.</param>
    /// <param name="sessionId">PID of a running dotsider instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON array of type references.</returns>
    [McpServerTool]
    public async partial Task<string> GetTypeRefs(
        string? assemblyPath = null,
        int? sessionId = null,
        CancellationToken ct = default)
    {
        if (assemblyPath is not null)
        {
            using var analyzer = new AssemblyAnalyzer(assemblyPath);
            return JsonSerializer.Serialize(analyzer.TypeRefs, DotsiderJsonOptions.Default);
        }

        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value)
                .SendAndUnwrapAsync(new DotsiderRequest { Method = "get-type-refs" }, ct);
        }

        return "Error: Either assemblyPath or sessionId is required.";
    }
}
