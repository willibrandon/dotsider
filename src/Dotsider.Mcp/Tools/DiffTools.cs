using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Core.Protocol;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using System.Text.Json;

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

    /// <summary>
    /// Compares the size of two Native AOT builds via their mstat size reports. Inputs are
    /// bare .mstat files or AOT binaries with mstat sidecars. Returns the summary, per-assembly
    /// and per-namespace deltas, and the top contributors; the delta tree only on request,
    /// pruned to a node cap with truncation metadata.
    /// </summary>
    /// <param name="leftPath">The baseline .mstat or AOT binary.</param>
    /// <param name="rightPath">The .mstat or AOT binary under comparison.</param>
    /// <param name="sessionId">PID of a running dotsider instance (forwards to the session).</param>
    /// <param name="topN">How many top contributors to return (default: 20).</param>
    /// <param name="includeTree">Include the hierarchical delta tree (default: false — it is large).</param>
    /// <param name="maxNodes">Delta-tree node cap when includeTree is set (default: 500).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON size-diff result.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public async partial Task<string> DiffSize(
        string leftPath,
        string rightPath,
        int? sessionId = null,
        int? topN = null,
        bool includeTree = false,
        int? maxNodes = null,
        CancellationToken ct = default)
    {
        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value)
                .SendAndUnwrapAsync(new DotsiderRequest
                {
                    Method = "diff-size",
                    LeftPath = leftPath,
                    RightPath = rightPath,
                    TopN = topN,
                    IncludeTree = includeTree,
                    MaxNodes = maxNodes,
                }, ct);
        }

        var left = ResolveMstatSource(leftPath, "leftPath");
        var right = ResolveMstatSource(rightPath, "rightPath");
        var payload = SizeDiffPayloadBuilder.BuildDiffPayload(left, right, topN, includeTree, maxNodes);
        return JsonSerializer.Serialize(payload, DotsiderJsonOptions.Default);
    }

    /// <summary>
    /// Checks a Native AOT build against size budgets, optionally versus a baseline. Budgets
    /// come as grammar strings (e.g. "max=25mb", "ns=System.Text.Json:growth=10kb"), an inline
    /// budgets JSON document, and/or a budgets file — the object form carries names, severity
    /// (error/warning), and per-budget contributor counts. The report fails only on
    /// error-severity breaches.
    /// </summary>
    /// <param name="targetPath">The .mstat or AOT binary to check.</param>
    /// <param name="budgets">Budget spec strings in the size-budget grammar.</param>
    /// <param name="budgetsJson">An inline budgets document ({ "budgets": [spec or object, ...] }).</param>
    /// <param name="budgetFilePath">Path to a budgets JSON file in the same schema.</param>
    /// <param name="baselinePath">The baseline .mstat or AOT binary; required by growth budgets.</param>
    /// <param name="sessionId">PID of a running dotsider instance (forwards to the session).</param>
    /// <param name="topN">Contributors per violated budget (default: 20).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON budget report with per-budget evaluations.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public async partial Task<string> CheckSizeBudgets(
        string targetPath,
        string[]? budgets = null,
        string? budgetsJson = null,
        string? budgetFilePath = null,
        string? baselinePath = null,
        int? sessionId = null,
        int? topN = null,
        CancellationToken ct = default)
    {
        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value)
                .SendAndUnwrapAsync(new DotsiderRequest
                {
                    Method = "check-size-budgets",
                    AssemblyPath = targetPath,
                    BaselinePath = baselinePath,
                    Budgets = budgets,
                    BudgetsJson = budgetsJson,
                    BudgetFilePath = budgetFilePath,
                    TopN = topN,
                }, ct);
        }

        var parsed = ParseBudgets(budgets, budgetsJson, budgetFilePath);
        var target = ResolveMstatSource(targetPath, "targetPath");
        MstatSource? baseline = null;
        if (baselinePath is not null)
            baseline = ResolveMstatSource(baselinePath, "baselinePath");

        if (baseline is null)
        {
            var growth = parsed.FirstOrDefault(b =>
                b.MaxGrowthBytes is not null || b.MaxGrowthPercent is not null);
            if (growth is not null)
            {
                throw new McpException(
                    $"Budget '{growth.Name ?? growth.ToString()}' limits growth, which needs baselinePath.");
            }
        }

        var payload = SizeDiffPayloadBuilder.BuildBudgetPayload(target, baseline, parsed, topN);
        return JsonSerializer.Serialize(payload, DotsiderJsonOptions.Default);
    }

    private static List<SizeBudget> ParseBudgets(
        string[]? budgets, string? budgetsJson, string? budgetFilePath)
    {
        var parsed = new List<SizeBudget>();
        try
        {
            if (budgetFilePath is not null)
            {
                ToolHelpers.ValidateFilePath(budgetFilePath, "budgetFilePath");
                parsed.AddRange(SizeBudgetFile.Load(budgetFilePath));
            }

            if (budgetsJson is not null)
                parsed.AddRange(SizeBudgetFile.Parse(budgetsJson));

            foreach (var spec in budgets ?? [])
                parsed.Add(SizeBudgetParser.Parse(spec));
        }
        catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException)
        {
            throw new McpException(ex.Message);
        }

        if (parsed.Count == 0)
        {
            throw new McpException(
                "At least one budget source is required: budgets, budgetsJson, or budgetFilePath.");
        }

        return parsed;
    }

    private static MstatSource ResolveMstatSource(string path, string label)
    {
        ToolHelpers.ValidateFilePath(path, label);
        return MstatLocator.Resolve(path)
            ?? throw new McpException(
                $"{label} is not mstat-backed: pass a .mstat size report or a Native AOT binary "
                + "published with IlcGenerateMstatFile (sidecar beside the binary).");
    }
}
