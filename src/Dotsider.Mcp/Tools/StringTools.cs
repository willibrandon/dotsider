using System.Text.Json;
using Dotsider.Core.Analysis;
using Dotsider.Core.Protocol;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Dotsider.Mcp.Tools;

/// <summary>
/// MCP tools for extracting and searching strings embedded in .NET assemblies.
/// </summary>
[McpServerToolType]
public sealed partial class StringTools(DotsiderSessionManager sessionManager)
{
    /// <summary>
    /// Extracts user strings, metadata strings, and raw binary strings from an assembly.
    /// </summary>
    /// <param name="assemblyPath">Path to assembly file.</param>
    /// <param name="sessionId">PID of a running dotsider instance.</param>
    /// <param name="query">Filter strings by value (case-insensitive substring match).</param>
    /// <param name="minLength">Minimum length for raw string extraction (default: 4).</param>
    /// <param name="maxResults">Maximum number of results per category.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON with UserStrings, MetadataStrings, and RawStrings arrays.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public async partial Task<string> ExtractStrings(
        string? assemblyPath = null,
        int? sessionId = null,
        string? query = null,
        int? minLength = null,
        int? maxResults = null,
        CancellationToken ct = default)
    {
        if (assemblyPath is not null)
        {
            ToolHelpers.ValidateAssemblyPath(assemblyPath);
            using var analyzer = new AssemblyAnalyzer(assemblyPath);
            var extractor = new StringExtractor(analyzer);
            var user = extractor.ExtractUserStrings();
            var metadata = extractor.ExtractMetadataStrings();
            var raw = extractor.ExtractRawStrings(minLength ?? 4);

            if (!string.IsNullOrEmpty(query))
            {
                user = [.. user.Where(s => s.Value.Contains(query, StringComparison.OrdinalIgnoreCase))];
                metadata = [.. metadata.Where(s => s.Value.Contains(query, StringComparison.OrdinalIgnoreCase))];
                raw = [.. raw.Where(s => s.Value.Contains(query, StringComparison.OrdinalIgnoreCase))];
            }

            var max = maxResults ?? int.MaxValue;
            return JsonSerializer.Serialize(new
            {
                UserStrings = user.Take(max).ToList(),
                MetadataStrings = metadata.Take(max).ToList(),
                RawStrings = raw.Take(max).ToList()
            }, DotsiderJsonOptions.Default);
        }

        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value)
                .SendAndUnwrapAsync(new DotsiderRequest { Method = "get-strings", Query = query, MinLength = minLength, MaxResults = maxResults }, ct);
        }

        return "Error: Either assemblyPath or sessionId is required.";
    }
}
