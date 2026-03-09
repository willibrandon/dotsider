using System.Text.Json;
using Dotsider.Core.Analysis;
using Dotsider.Core.Protocol;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Dotsider.Mcp.Tools;

/// <summary>
/// MCP tools for assembly-level analysis: info, type listing, method listing, and member search.
/// </summary>
[McpServerToolType]
public sealed partial class AssemblyTools(DotsiderSessionManager sessionManager)
{
    /// <summary>
    /// Gets assembly metadata including name, version, framework, architecture, and member counts.
    /// </summary>
    /// <param name="assemblyPath">Path to assembly file (.dll or .exe).</param>
    /// <param name="sessionId">PID of a running dotsider instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON with assembly identity and statistics.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public async partial Task<string> GetAssemblyInfo(
        string? assemblyPath = null,
        int? sessionId = null,
        CancellationToken ct = default)
    {
        if (assemblyPath is not null)
        {
            ToolHelpers.ValidateAssemblyPath(assemblyPath);
            using var analyzer = new AssemblyAnalyzer(assemblyPath);
            return JsonSerializer.Serialize(new
            {
                analyzer.FilePath, analyzer.FileName, analyzer.FileSize,
                analyzer.AssemblyName, analyzer.AssemblyVersion, analyzer.TargetFramework,
                analyzer.Culture, analyzer.PublicKeyToken, analyzer.Architecture,
                analyzer.HasMetadata,
                TypeCount = analyzer.TypeDefs.Count,
                MethodCount = analyzer.MethodDefs.Count,
                AssemblyRefCount = analyzer.AssemblyRefs.Count
            }, DotsiderJsonOptions.Default);
        }

        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value)
                .SendAndUnwrapAsync(new DotsiderRequest { Method = "assembly-info" }, ct);
        }

        return "Error: Either assemblyPath or sessionId is required.";
    }

    /// <summary>
    /// Lists type definitions with optional name filtering and result limiting.
    /// </summary>
    /// <param name="assemblyPath">Path to assembly file.</param>
    /// <param name="sessionId">PID of a running dotsider instance.</param>
    /// <param name="query">Filter types by name (case-insensitive substring match).</param>
    /// <param name="maxResults">Maximum number of results.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON array of type definitions.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public async partial Task<string> ListTypes(
        string? assemblyPath = null,
        int? sessionId = null,
        string? query = null,
        int? maxResults = null,
        CancellationToken ct = default)
    {
        if (assemblyPath is not null)
        {
            ToolHelpers.ValidateAssemblyPath(assemblyPath);
            using var analyzer = new AssemblyAnalyzer(assemblyPath);
            var types = analyzer.TypeDefs.AsEnumerable();
            if (!string.IsNullOrEmpty(query))
                types = types.Where(t => t.FullName.Contains(query, StringComparison.OrdinalIgnoreCase));
            if (maxResults is > 0)
                types = types.Take(maxResults.Value);
            return JsonSerializer.Serialize(types.ToList(), DotsiderJsonOptions.Default);
        }

        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value)
                .SendAndUnwrapAsync(new DotsiderRequest { Method = "list-types", Query = query, MaxResults = maxResults }, ct);
        }

        return "Error: Either assemblyPath or sessionId is required.";
    }

    /// <summary>
    /// Lists method definitions with optional type and name filtering.
    /// </summary>
    /// <param name="assemblyPath">Path to assembly file.</param>
    /// <param name="sessionId">PID of a running dotsider instance.</param>
    /// <param name="typeName">Filter by declaring type name.</param>
    /// <param name="query">Filter by method name.</param>
    /// <param name="maxResults">Maximum number of results.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON array of method definitions.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public async partial Task<string> ListMethods(
        string? assemblyPath = null,
        int? sessionId = null,
        string? typeName = null,
        string? query = null,
        int? maxResults = null,
        CancellationToken ct = default)
    {
        if (assemblyPath is not null)
        {
            ToolHelpers.ValidateAssemblyPath(assemblyPath);
            using var analyzer = new AssemblyAnalyzer(assemblyPath);
            var methods = analyzer.MethodDefs.AsEnumerable();
            if (!string.IsNullOrEmpty(typeName))
                methods = methods.Where(m => m.DeclaringType.Contains(typeName, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(query))
                methods = methods.Where(m => m.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
            if (maxResults is > 0)
                methods = methods.Take(maxResults.Value);
            return JsonSerializer.Serialize(methods.ToList(), DotsiderJsonOptions.Default);
        }

        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value)
                .SendAndUnwrapAsync(new DotsiderRequest { Method = "list-methods", TypeName = typeName, Query = query, MaxResults = maxResults }, ct);
        }

        return "Error: Either assemblyPath or sessionId is required.";
    }

    /// <summary>
    /// Searches for types, methods, and member references matching a query string.
    /// </summary>
    /// <param name="query">Search query (case-insensitive substring match).</param>
    /// <param name="assemblyPath">Path to assembly file.</param>
    /// <param name="sessionId">PID of a running dotsider instance.</param>
    /// <param name="maxResults">Maximum number of results per category.</param>
    /// <param name="includeCompilerGenerated">Include compiler-generated members (default: false).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON with Types, Methods, and MemberRefs arrays.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public async partial Task<string> FindMembers(
        string query,
        string? assemblyPath = null,
        int? sessionId = null,
        int? maxResults = null,
        bool includeCompilerGenerated = false,
        CancellationToken ct = default)
    {
        if (assemblyPath is not null)
        {
            ToolHelpers.ValidateAssemblyPath(assemblyPath);
            using var analyzer = new AssemblyAnalyzer(assemblyPath);
            var max = maxResults ?? 100;

            var types = analyzer.TypeDefs
                .Where(t => t.FullName.Contains(query, StringComparison.OrdinalIgnoreCase));
            var methods = analyzer.MethodDefs
                .Where(m => m.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || m.DeclaringType.Contains(query, StringComparison.OrdinalIgnoreCase));

            if (!includeCompilerGenerated)
            {
                types = types.Where(t => !t.Name.StartsWith("<>") && !t.Name.Contains("__"));
                methods = methods.Where(m => !m.DeclaringType.StartsWith("<>"));
            }

            return JsonSerializer.Serialize(new
            {
                Types = types.Take(max).ToList(),
                Methods = methods.Take(max).ToList(),
                MemberRefs = analyzer.MemberRefs.Where(r => r.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).Take(max).ToList()
            }, DotsiderJsonOptions.Default);
        }

        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value)
                .SendAndUnwrapAsync(new DotsiderRequest { Method = "find-members", Query = query, MaxResults = maxResults }, ct);
        }

        return "Error: Either assemblyPath or sessionId is required.";
    }
}
