using Dotsider.Core.Protocol;
using ModelContextProtocol.Server;
using System.Text.Json;

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
    /// <param name="assemblyPath">Path to an assembly file or supported native module.</param>
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
            using var analyzer = ToolHelpers.OpenAnalyzer(assemblyPath);
            return JsonSerializer.Serialize(new
            {
                analyzer.FilePath, analyzer.FileName, analyzer.FileSize,
                analyzer.AssemblyName, analyzer.AssemblyVersion, analyzer.TargetFramework,
                analyzer.Culture, analyzer.PublicKeyToken, analyzer.Architecture,
                analyzer.HasMetadata,
                analyzer.BinaryKind,
                analyzer.NativeAotInfo,
                analyzer.DisplayName,
                analyzer.SourceBundlePath,
                analyzer.IsBundleBacked,
                analyzer.PreferredRuntimePack,
                analyzer.LaunchPath,
                analyzer.CanSaveInPlace,
                analyzer.PdbProvenance,
                analyzer.SourceLink,
                TypeCount = analyzer.TypeDefs.Count,
                MethodCount = analyzer.MethodDefs.Count,
                AssemblyRefCount = analyzer.AssemblyRefs.Count,
                ReadyToRunSectionCount = analyzer.ReadyToRunSections.Count,
                RecoveredTypeCount = analyzer.RecoveredTypes.Count,
                FrozenStringCount = analyzer.FrozenStrings.Count,
                NativeSymbolCount = analyzer.NativeSymbols?.Symbols.Count ?? 0,
                NativeSymbolSource = analyzer.NativeSymbols?.Source,
                NativeSymbolStatus = analyzer.NativeSymbols?.Status,
                PreIlc = BuildPreIlcSummary(analyzer),
                ReadyToRun = BuildReadyToRunSummary(analyzer),
                Webcil = WebcilPayloadBuilder.BuildSummary(analyzer),
                Wasm = WasmPayloadBuilder.BuildSummary(analyzer)
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
    /// Builds the cheap pre-ILC probe summary for a Native AOT binary — origin, sidecar
    /// availability, and reference counts. Never attaches; package and other references are
    /// counts, not dumps. Returns null when no sidecars were found.
    /// </summary>
    private static object? BuildPreIlcSummary(Core.Analysis.AssemblyAnalyzer analyzer)
    {
        if (analyzer.PreIlcSidecars is not { } s)
            return null;

        return new
        {
            s.HasAttachableCompanion,
            RootAssembly = s.ManagedAssemblyPath is { } p ? Path.GetFileName(p) : null,
            Origin = s.Origin.ToString(),
            PdbStatus = s.PdbStatus.ToString(),
            HasMstat = s.MstatPath is not null,
            HasDgml = (s.CodegenDgmlPath ?? s.ScanDgmlPath) is not null,
            LocalReferenceCount = s.LocalReferencePaths.Count,
            s.PackageReferenceCount,
            s.OtherReferenceCount
        };
    }

    /// <summary>
    /// Builds the ReadyToRun summary for a crossgen2 image — status, version, composite flags,
    /// architecture, and precompiled-method counts. Returns null when the image is not ReadyToRun.
    /// </summary>
    private static object? BuildReadyToRunSummary(Core.Analysis.AssemblyAnalyzer analyzer)
    {
        if (analyzer.ReadyToRunInfo is not { } info)
            return null;

        return new
        {
            Status = info.Status.ToString(),
            info.MajorVersion,
            info.MinorVersion,
            info.IsComposite,
            info.IsComponent,
            info.IsPartialImage,
            Architecture = info.Architecture.ToString(),
            info.OwnerCompositeExecutable,
            PrecompiledMethods = analyzer.ReadyToRunIndex?.Methods.Count ?? 0,
            InstantiationCount = analyzer.ReadyToRunIndex?.InstantiationCount ?? 0,
            TotalCodeSize = analyzer.ReadyToRunIndex?.TotalCodeSize ?? 0
        };
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
            using var analyzer = ToolHelpers.OpenAnalyzer(assemblyPath);

            // A Native AOT binary has no metadata TypeDefs; fall back to the types
            // recovered from its embedded NativeFormat metadata.
            if (!analyzer.HasMetadata && analyzer.RecoveredTypes.Count > 0)
            {
                var recovered = analyzer.RecoveredTypes.AsEnumerable();
                if (!string.IsNullOrEmpty(query))
                    recovered = recovered.Where(t => t.FullName.Contains(query, StringComparison.OrdinalIgnoreCase));
                if (maxResults is > 0)
                    recovered = recovered.Take(maxResults.Value);
                return JsonSerializer.Serialize(recovered.ToList(), DotsiderJsonOptions.Default);
            }

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
            using var analyzer = ToolHelpers.OpenAnalyzer(assemblyPath);
            return JsonSerializer.Serialize(
                NativeAotPayloadBuilder.BuildMethodInventory(analyzer, typeName, query, maxResults),
                DotsiderJsonOptions.Default);
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
            using var analyzer = ToolHelpers.OpenAnalyzer(assemblyPath);
            return JsonSerializer.Serialize(
                NativeAotPayloadBuilder.BuildMemberSearch(analyzer, query, maxResults, includeCompilerGenerated),
                DotsiderJsonOptions.Default);
        }

        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value)
                .SendAndUnwrapAsync(new DotsiderRequest
                {
                    Method = "find-members",
                    Query = query,
                    MaxResults = maxResults,
                    IncludeCompilerGenerated = includeCompilerGenerated
                }, ct);
        }

        return "Error: Either assemblyPath or sessionId is required.";
    }
}
