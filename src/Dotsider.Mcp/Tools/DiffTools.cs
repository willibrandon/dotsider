using System.Text.Json;
using Dotsider.Core.Analysis;
using Dotsider.Core.Protocol;
using ModelContextProtocol.Server;

namespace Dotsider.Mcp.Tools;

/// <summary>
/// MCP tools for comparing two assemblies and identifying differences in types, methods, and references.
/// </summary>
[McpServerToolType]
public sealed partial class DiffTools(DotsiderSessionManager sessionManager)
{
    /// <summary>
    /// Compares two assemblies and returns added, removed, and changed types, methods, and references.
    /// </summary>
    /// <param name="leftPath">Path to the first (left/old) assembly.</param>
    /// <param name="rightPath">Path to the second (right/new) assembly.</param>
    /// <param name="sessionId">PID of a running dotsider instance (uses the session's diff if available).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON diff result with categorized changes.</returns>
    [McpServerTool]
    public async partial Task<string> DiffAssemblies(
        string leftPath,
        string rightPath,
        int? sessionId = null,
        CancellationToken ct = default)
    {
        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value)
                .SendAndUnwrapAsync(new DotsiderRequest { Method = "diff", LeftPath = leftPath, RightPath = rightPath }, ct);
        }

        // Direct mode
        using var left = new AssemblyAnalyzer(leftPath);
        using var right = new AssemblyAnalyzer(rightPath);
        var result = AssemblyDiffer.Compare(left, right);
        return JsonSerializer.Serialize(result, DotsiderJsonOptions.Default);
    }
}
