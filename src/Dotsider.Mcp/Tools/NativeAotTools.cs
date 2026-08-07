using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Core.Protocol;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Dotsider.Mcp.Tools;

/// <summary>
/// MCP tools for Native AOT-specific binary, section, size, and dependency-root analysis.
/// </summary>
[McpServerToolType]
public sealed partial class NativeAotTools(DotsiderSessionManager sessionManager)
{
    /// <summary>
    /// Gets Native AOT identity, recovered metadata counts, native symbol provenance, and sidecar availability.
    /// </summary>
    /// <param name="assemblyPath">Path to a Native AOT binary.</param>
    /// <param name="sessionId">PID of a running dotsider instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON Native AOT summary.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public async partial Task<string> GetNativeAotInfo(
        string? assemblyPath = null,
        int? sessionId = null,
        CancellationToken ct = default)
    {
        if (assemblyPath is not null)
        {
            ToolHelpers.ValidateAssemblyPath(assemblyPath);
            using var analyzer = ToolHelpers.OpenAnalyzer(assemblyPath);
            return Serialize(() => NativeAotPayloadBuilder.BuildInfo(analyzer));
        }

        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value)
                .SendAndUnwrapAsync(new DotsiderRequest { Method = "get-native-aot-info" }, ct);
        }

        return "Error: Either assemblyPath or sessionId is required.";
    }

    /// <summary>
    /// Lists the Native AOT ReadyToRun module sections, distinct from ordinary PE sections.
    /// </summary>
    /// <param name="assemblyPath">Path to a Native AOT binary.</param>
    /// <param name="sessionId">PID of a running dotsider instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON section table.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public async partial Task<string> ListNativeAotSections(
        string? assemblyPath = null,
        int? sessionId = null,
        CancellationToken ct = default)
    {
        if (assemblyPath is not null)
        {
            ToolHelpers.ValidateAssemblyPath(assemblyPath);
            using var analyzer = ToolHelpers.OpenAnalyzer(assemblyPath);
            return Serialize(() => NativeAotPayloadBuilder.BuildSections(analyzer));
        }

        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value)
                .SendAndUnwrapAsync(new DotsiderRequest { Method = "list-native-aot-sections" }, ct);
        }

        return "Error: Either assemblyPath or sessionId is required.";
    }

    /// <summary>
    /// Gets top Native AOT size contributors from an mstat-backed binary or .mstat file.
    /// </summary>
    /// <param name="assemblyPath">Path to a Native AOT binary or .mstat file.</param>
    /// <param name="sessionId">PID of a running dotsider instance.</param>
    /// <param name="query">Optional contributor search string.</param>
    /// <param name="section">Optional mstat section filter, such as Method or FrozenObject.</param>
    /// <param name="assemblyName">Optional assembly attribution filter.</param>
    /// <param name="namespaceName">Optional namespace attribution filter.</param>
    /// <param name="topN">Maximum contributors to return (default: 20).</param>
    /// <param name="includeWhy">Include DGML root chains when available.</param>
    /// <param name="maxWhyChains">Maximum DGML chains per aggregated contributor.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON contributor rows.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public async partial Task<string> GetNativeAotSizeContributors(
        string? assemblyPath = null,
        int? sessionId = null,
        string? query = null,
        string? section = null,
        string? assemblyName = null,
        string? namespaceName = null,
        int? topN = null,
        bool includeWhy = false,
        int? maxWhyChains = null,
        CancellationToken ct = default)
    {
        if (assemblyPath is not null)
        {
            ToolHelpers.ValidateFilePath(assemblyPath, "assemblyPath");
            var source = ResolveMstatSource(assemblyPath, "assemblyPath");
            return Serialize(() => NativeAotPayloadBuilder.BuildSizeContributors(
                source, query, section, assemblyName, namespaceName, topN, includeWhy, maxWhyChains));
        }

        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value)
                .SendAndUnwrapAsync(new DotsiderRequest
                {
                    Method = "get-native-aot-size-contributors",
                    Query = query,
                    Section = section,
                    AssemblyName = assemblyName,
                    NamespaceName = namespaceName,
                    TopN = topN,
                    IncludeWhy = includeWhy,
                    MaxWhyChains = maxWhyChains
                }, ct);
        }

        return "Error: Either assemblyPath or sessionId is required.";
    }

    /// <summary>
    /// Explains why a Native AOT mstat contributor is rooted by walking its DGML dependency chain.
    /// </summary>
    /// <param name="target">Contributor full path, key, node label, display name, or substring.</param>
    /// <param name="assemblyPath">Path to a Native AOT binary or .mstat file.</param>
    /// <param name="sessionId">PID of a running dotsider instance.</param>
    /// <param name="maxCandidates">Maximum ambiguous candidates to return.</param>
    /// <param name="maxWhyChains">Maximum DGML chains for an aggregate contributor.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON resolved why chain or candidate list.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public async partial Task<string> ExplainNativeAotSize(
        string target,
        string? assemblyPath = null,
        int? sessionId = null,
        int? maxCandidates = null,
        int? maxWhyChains = null,
        CancellationToken ct = default)
    {
        if (assemblyPath is not null)
        {
            ToolHelpers.ValidateFilePath(assemblyPath, "assemblyPath");
            var source = ResolveMstatSource(assemblyPath, "assemblyPath");
            return Serialize(() => NativeAotPayloadBuilder.BuildWhy(
                source, target, maxCandidates, maxWhyChains));
        }

        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value)
                .SendAndUnwrapAsync(new DotsiderRequest
                {
                    Method = "explain-native-aot-size",
                    Target = target,
                    MaxCandidates = maxCandidates,
                    MaxWhyChains = maxWhyChains
                }, ct);
        }

        return "Error: Either assemblyPath or sessionId is required.";
    }

    private static MstatSource ResolveMstatSource(string path, string label) =>
        MstatLocator.Resolve(path)
        ?? throw new McpException(
            $"{label} is not mstat-backed: pass a .mstat size report or a Native AOT binary "
            + "published with IlcGenerateMstatFile.");

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
