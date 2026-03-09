using System.Text.Json;
using Dotsider.Core.Analysis;
using Dotsider.Core.Protocol;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Dotsider.Mcp.Tools;

/// <summary>
/// MCP tools for PE headers, CLR metadata, sections, attributes, resources, and token resolution.
/// </summary>
[McpServerToolType]
public sealed partial class MetadataTools(DotsiderSessionManager sessionManager)
{
    /// <summary>
    /// Gets PE headers including machine type, subsystem, and characteristics.
    /// </summary>
    /// <param name="assemblyPath">Path to assembly file.</param>
    /// <param name="sessionId">PID of a running dotsider instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON with PE header fields.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public async partial Task<string> GetPeHeaders(
        string? assemblyPath = null,
        int? sessionId = null,
        CancellationToken ct = default)
    {
        if (assemblyPath is not null)
        {
            ToolHelpers.ValidateAssemblyPath(assemblyPath);
            using var analyzer = new AssemblyAnalyzer(assemblyPath);
            return JsonSerializer.Serialize(analyzer.PeHeaders, DotsiderJsonOptions.Default);
        }

        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value)
                .SendAndUnwrapAsync(new DotsiderRequest { Method = "get-pe-headers" }, ct);
        }

        return "Error: Either assemblyPath or sessionId is required.";
    }

    /// <summary>
    /// Gets CLR header including runtime version, flags, and entry point token.
    /// </summary>
    /// <param name="assemblyPath">Path to assembly file.</param>
    /// <param name="sessionId">PID of a running dotsider instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON with CLR header fields.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public async partial Task<string> GetClrHeader(
        string? assemblyPath = null,
        int? sessionId = null,
        CancellationToken ct = default)
    {
        if (assemblyPath is not null)
        {
            ToolHelpers.ValidateAssemblyPath(assemblyPath);
            using var analyzer = new AssemblyAnalyzer(assemblyPath);
            return JsonSerializer.Serialize(analyzer.ClrHeader, DotsiderJsonOptions.Default);
        }

        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value)
                .SendAndUnwrapAsync(new DotsiderRequest { Method = "get-clr-header" }, ct);
        }

        return "Error: Either assemblyPath or sessionId is required.";
    }

    /// <summary>
    /// Gets PE sections with virtual address, size, and raw data info.
    /// </summary>
    /// <param name="assemblyPath">Path to assembly file.</param>
    /// <param name="sessionId">PID of a running dotsider instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON array of PE sections.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public async partial Task<string> GetSections(
        string? assemblyPath = null,
        int? sessionId = null,
        CancellationToken ct = default)
    {
        if (assemblyPath is not null)
        {
            ToolHelpers.ValidateAssemblyPath(assemblyPath);
            using var analyzer = new AssemblyAnalyzer(assemblyPath);
            return JsonSerializer.Serialize(analyzer.Sections, DotsiderJsonOptions.Default);
        }

        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value)
                .SendAndUnwrapAsync(new DotsiderRequest { Method = "get-sections" }, ct);
        }

        return "Error: Either assemblyPath or sessionId is required.";
    }

    private static readonly string[] CompilerGeneratedAttributeNames =
    [
        "CompilerGeneratedAttribute",
        "NullableContextAttribute",
        "NullableAttribute",
        "DebuggerBrowsableAttribute",
        "CompilerFeatureRequiredAttribute",
        "IsExternalInit",
    ];

    /// <summary>
    /// Gets custom attributes applied to assembly metadata entities.
    /// </summary>
    /// <param name="assemblyPath">Path to assembly file.</param>
    /// <param name="sessionId">PID of a running dotsider instance.</param>
    /// <param name="includeCompilerGenerated">Include compiler-generated attributes (default: false).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON array of custom attributes.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public async partial Task<string> GetCustomAttributes(
        string? assemblyPath = null,
        int? sessionId = null,
        bool includeCompilerGenerated = false,
        CancellationToken ct = default)
    {
        if (assemblyPath is not null)
        {
            ToolHelpers.ValidateAssemblyPath(assemblyPath);
            using var analyzer = new AssemblyAnalyzer(assemblyPath);
            var attributes = analyzer.CustomAttributes.AsEnumerable();

            if (!includeCompilerGenerated)
            {
                attributes = attributes.Where(a =>
                    !CompilerGeneratedAttributeNames.Any(name =>
                        a.Constructor.Contains(name, StringComparison.Ordinal)));
            }

            return JsonSerializer.Serialize(attributes.ToList(), DotsiderJsonOptions.Default);
        }

        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value)
                .SendAndUnwrapAsync(new DotsiderRequest { Method = "get-custom-attributes" }, ct);
        }

        return "Error: Either assemblyPath or sessionId is required.";
    }

    /// <summary>
    /// Gets manifest resources defined in the assembly.
    /// </summary>
    /// <param name="assemblyPath">Path to assembly file.</param>
    /// <param name="sessionId">PID of a running dotsider instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON array of manifest resources.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public async partial Task<string> GetResources(
        string? assemblyPath = null,
        int? sessionId = null,
        CancellationToken ct = default)
    {
        if (assemblyPath is not null)
        {
            ToolHelpers.ValidateAssemblyPath(assemblyPath);
            using var analyzer = new AssemblyAnalyzer(assemblyPath);
            return JsonSerializer.Serialize(analyzer.Resources, DotsiderJsonOptions.Default);
        }

        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value)
                .SendAndUnwrapAsync(new DotsiderRequest { Method = "get-resources" }, ct);
        }

        return "Error: Either assemblyPath or sessionId is required.";
    }

    /// <summary>
    /// Resolves a metadata token to a human-readable name.
    /// </summary>
    /// <param name="token">Metadata token (integer).</param>
    /// <param name="assemblyPath">Path to assembly file.</param>
    /// <param name="sessionId">PID of a running dotsider instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON with the token value and its resolved name.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public async partial Task<string> ResolveToken(
        int token,
        string? assemblyPath = null,
        int? sessionId = null,
        CancellationToken ct = default)
    {
        if (assemblyPath is not null)
        {
            ToolHelpers.ValidateAssemblyPath(assemblyPath);
            using var analyzer = new AssemblyAnalyzer(assemblyPath);
            var resolved = analyzer.ResolveToken(token);
            return JsonSerializer.Serialize(new { Token = token, Resolved = resolved },
                DotsiderJsonOptions.Default);
        }

        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value)
                .SendAndUnwrapAsync(new DotsiderRequest { Method = "resolve-token", Token = token }, ct);
        }

        return "Error: Either assemblyPath or sessionId is required.";
    }
}
