using System.Text.Json;
using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
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
    /// <param name="includeCompilerGenerated">Include compiler-generated types and methods (default: false).</param>
    /// <param name="maxTypeDiffs">Maximum number of type diffs to return (default: all). The summary always reflects full counts.</param>
    /// <param name="maxMethodDiffs">Maximum number of method diffs to return (default: all). The summary always reflects full counts.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON diff result with categorized changes and a summary with full counts.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public async partial Task<string> DiffAssemblies(
        string leftPath,
        string rightPath,
        int? sessionId = null,
        bool includeCompilerGenerated = false,
        int? maxTypeDiffs = null,
        int? maxMethodDiffs = null,
        CancellationToken ct = default)
    {
        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value)
                .SendAndUnwrapAsync(new DotsiderRequest { Method = "diff", LeftPath = leftPath, RightPath = rightPath }, ct);
        }

        // Direct mode
        ToolHelpers.ValidateAssemblyPath(leftPath);
        ToolHelpers.ValidateAssemblyPath(rightPath);
        using var left = new AssemblyAnalyzer(leftPath);
        using var right = new AssemblyAnalyzer(rightPath);
        var result = AssemblyDiffer.Compare(left, right);

        if (!includeCompilerGenerated)
        {
            static bool IsCompilerGeneratedType(string? name) =>
                name is not null && (name.StartsWith("<>") || (name.StartsWith('<') && name.Contains('>')));

            var filteredTypeDiffs = result.TypeDiffs
                .Where(d =>
                {
                    var name = (d.Left ?? d.Right)?.Name;
                    return !IsCompilerGeneratedType(name);
                });

            var filteredMethodDiffs = result.MethodDiffs
                .Where(d =>
                {
                    var declaringType = (d.Left ?? d.Right)?.DeclaringType;
                    return declaringType is null || !declaringType.StartsWith("<>");
                });

            result = new AssemblyDiffResult([.. filteredTypeDiffs], [.. filteredMethodDiffs], result.AssemblyRefDiffs, result.MetadataSummary);
        }

        // Apply limits after filtering so the summary (computed from the full diff) stays accurate
        if (maxTypeDiffs is > 0 && result.TypeDiffs.Count > maxTypeDiffs.Value)
        {
            result = result with { TypeDiffs = [.. result.TypeDiffs.Take(maxTypeDiffs.Value)] };
        }

        if (maxMethodDiffs is > 0 && result.MethodDiffs.Count > maxMethodDiffs.Value)
        {
            result = result with { MethodDiffs = [.. result.MethodDiffs.Take(maxMethodDiffs.Value)] };
        }

        return JsonSerializer.Serialize(result, DotsiderJsonOptions.Default);
    }
}
