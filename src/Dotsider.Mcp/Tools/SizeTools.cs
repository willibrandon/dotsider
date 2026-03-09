using System.Text.Json;
using Dotsider.Core.Analysis;
using Dotsider.Core.Protocol;
using ModelContextProtocol.Server;

namespace Dotsider.Mcp.Tools;

/// <summary>
/// MCP tools for analyzing assembly size: hierarchical size trees and largest method ranking.
/// </summary>
[McpServerToolType]
public sealed partial class SizeTools(DotsiderSessionManager sessionManager)
{
    /// <summary>
    /// Gets a hierarchical size breakdown of namespaces, types, and methods.
    /// </summary>
    /// <param name="assemblyPath">Path to assembly file.</param>
    /// <param name="sessionId">PID of a running dotsider instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON size tree with nested nodes.</returns>
    [McpServerTool]
    public async partial Task<string> GetSizeBreakdown(
        string? assemblyPath = null,
        int? sessionId = null,
        CancellationToken ct = default)
    {
        if (assemblyPath is not null)
        {
            using var analyzer = new AssemblyAnalyzer(assemblyPath);
            var disassembler = new IlDisassembler(analyzer);
            var tree = SizeAnalyzer.BuildSizeTree(analyzer, disassembler);
            return JsonSerializer.Serialize(tree, DotsiderJsonOptions.Default);
        }

        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value)
                .SendAndUnwrapAsync(new DotsiderRequest { Method = "get-size-tree" }, ct);
        }

        return "Error: Either assemblyPath or sessionId is required.";
    }

    /// <summary>
    /// Gets the largest methods by IL byte size, sorted descending.
    /// </summary>
    /// <param name="assemblyPath">Path to assembly file.</param>
    /// <param name="sessionId">PID of a running dotsider instance.</param>
    /// <param name="maxResults">Number of methods to return (default: 20).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON array of methods with their IL byte sizes.</returns>
    [McpServerTool]
    public async partial Task<string> GetLargestMethods(
        string? assemblyPath = null,
        int? sessionId = null,
        int? maxResults = null,
        CancellationToken ct = default)
    {
        if (assemblyPath is not null)
        {
            using var analyzer = new AssemblyAnalyzer(assemblyPath);
            var max = maxResults ?? 20;
            var methods = analyzer.MethodDefs
                .Select(m =>
                {
                    try
                    {
                        var body = analyzer.GetMethodBody(m);
                        return new { Method = m, Size = body?.GetILBytes()?.Length ?? 0 };
                    }
                    catch { return new { Method = m, Size = 0 }; }
                })
                .OrderByDescending(x => x.Size)
                .Take(max)
                .ToList();

            return JsonSerializer.Serialize(methods, DotsiderJsonOptions.Default);
        }

        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value)
                .SendAndUnwrapAsync(new DotsiderRequest { Method = "get-largest-methods", MaxResults = maxResults }, ct);
        }

        return "Error: Either assemblyPath or sessionId is required.";
    }
}
