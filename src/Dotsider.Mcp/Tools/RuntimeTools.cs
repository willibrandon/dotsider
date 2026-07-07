using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Core.Protocol;
using ModelContextProtocol.Server;
using System.Text.Json;

namespace Dotsider.Mcp.Tools;

/// <summary>
/// MCP tools for .NET runtime discovery and assembly resolution.
/// </summary>
[McpServerToolType]
public sealed partial class RuntimeTools(DotsiderSessionManager sessionManager)
{
    /// <summary>
    /// Finds an assembly in the system .NET shared framework installation.
    /// Returns the full path and the runtime pack it was found in.
    /// </summary>
    /// <param name="assemblyName">Assembly name without extension (e.g. "System.Runtime").</param>
    /// <param name="targetFramework">Optional TFM for version matching (e.g. ".NETCoreApp,Version=v10.0").</param>
    /// <param name="preferredRuntimePack">Optional runtime pack to probe first.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON with path and runtime pack, or null if not found.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public static partial Task<string> FindFrameworkAssembly(
        string assemblyName,
        string? targetFramework = null,
        string? preferredRuntimePack = null,
        CancellationToken ct = default)
    {
        var result = DotNetRuntimeLocator.FindAssemblyInSharedFramework(
            assemblyName, targetFramework, preferredRuntimePack);

        return Task.FromResult(JsonSerializer.Serialize(result, DotsiderJsonOptions.Default));
    }

    /// <summary>
    /// Resolves an assembly reference using the full 6-step resolution chain:
    /// app-local, runtime directory, source bundle, host bundle, adjacent bundles,
    /// and .NET shared framework.
    /// </summary>
    /// <param name="assemblyName">Assembly name to resolve (e.g. "System.Runtime").</param>
    /// <param name="assemblyPath">Path to the referencing assembly for context. Required for direct mode.</param>
    /// <param name="sessionId">PID of a running dotsider instance for session mode.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON with resolution kind (file/bundle), path, and source info.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public async partial Task<string> ResolveAssembly(
        string assemblyName,
        string? assemblyPath = null,
        int? sessionId = null,
        CancellationToken ct = default)
    {
        if (assemblyPath is not null)
        {
            ToolHelpers.ValidateAssemblyPath(assemblyPath);
            using var analyzer = ToolHelpers.OpenAnalyzer(assemblyPath);
            var resolved = AssemblyAnalyzer.ResolveAssembly(
                analyzer.FilePath, assemblyName,
                analyzer.TargetFramework, analyzer.PreferredRuntimePack,
                analyzer.SourceBundlePath);

            if (resolved is null)
                return "null";

            var info = resolved switch
            {
                ResolvedAssembly.FromFile(var p) =>
                    new ResolvedAssemblyInfo("file", p, null, null),
                ResolvedAssembly.FromBundle(_, var name, var bundle) =>
                    new ResolvedAssemblyInfo("bundle", null, name, bundle),
                _ => null
            };
            return JsonSerializer.Serialize(info, DotsiderJsonOptions.Default);
        }

        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value)
                .SendAndUnwrapAsync(new DotsiderRequest
                {
                    Method = "resolve-assembly",
                    AssemblyName = assemblyName
                }, ct);
        }

        return "Error: Either assemblyPath or sessionId is required.";
    }
}
