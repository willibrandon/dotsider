using System.Text.Json;
using Dotsider.Core.Protocol;
using ModelContextProtocol.Server;

namespace Dotsider.Mcp.Tools;

/// <summary>
/// MCP tools for querying field definitions.
/// </summary>
[McpServerToolType]
public sealed partial class FieldTools(DotsiderSessionManager sessionManager)
{
    /// <summary>
    /// Lists field definitions in an assembly with optional filtering by type name,
    /// query string, and result limit.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly file.</param>
    /// <param name="sessionId">PID of a running dotsider instance.</param>
    /// <param name="typeName">Filter fields to those declared in this type.</param>
    /// <param name="query">Filter fields by name substring.</param>
    /// <param name="maxResults">Maximum number of results to return.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>JSON array of field definitions.</returns>
    [McpServerTool(ReadOnly = true, OpenWorld = false)]
    public async partial Task<string> ListFields(
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
            var fields = analyzer.FieldDefs.AsEnumerable();

            if (!string.IsNullOrEmpty(typeName))
                fields = fields.Where(f =>
                    f.DeclaringType.Contains(typeName, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(query))
                fields = fields.Where(f =>
                    f.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
            if (maxResults is > 0)
                fields = fields.Take(maxResults.Value);

            return JsonSerializer.Serialize(fields, DotsiderJsonOptions.Default);
        }

        if (sessionId is not null)
        {
            return await sessionManager.GetTarget(sessionId.Value)
                .SendAndUnwrapAsync(new DotsiderRequest
                {
                    Method = "list-fields",
                    TypeName = typeName,
                    Query = query,
                    MaxResults = maxResults
                }, ct);
        }

        return "Error: Either assemblyPath or sessionId is required.";
    }
}
