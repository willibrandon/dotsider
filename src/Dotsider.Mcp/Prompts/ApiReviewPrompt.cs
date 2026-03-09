using ModelContextProtocol.Server;

namespace Dotsider.Mcp.Prompts;

/// <summary>
/// MCP prompt that guides an API surface review of a .NET assembly.
/// </summary>
[McpServerPromptType]
public sealed partial class ApiReviewPrompt
{
    /// <summary>
    /// Guides an API surface review of a .NET assembly — analyzes public types, naming conventions, and design patterns.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly to review.</param>
    /// <returns>Prompt text with step-by-step API review instructions.</returns>
    [McpServerPrompt]
    public static partial string ApiReview(
        string assemblyPath)
    {
        return $"""
            You are reviewing the public API surface of the .NET assembly at: {assemblyPath}

            Follow these steps using the dotsider MCP tools:

            1. **Assembly Info**: Use get_assembly_info for the assembly identity and framework target.

            2. **Public Types**: Use list_types to enumerate all public types. Analyze:
               - Naming conventions (PascalCase, consistent prefixes/suffixes)
               - Namespace organization (logical grouping, no conflicts)
               - Type hierarchy (appropriate use of inheritance vs composition)

            3. **Public Methods**: Use list_methods to review the method surface:
               - Method naming consistency
               - Parameter types and nullability
               - Async patterns (Task return types, CancellationToken parameters)
               - Overload groups and their consistency

            4. **Dependencies**: Use get_assembly_refs to assess:
               - Dependency count (fewer is better for a library)
               - Framework dependencies vs third-party
               - Version constraints

            5. **Custom Attributes**: Use get_custom_attributes for:
               - Obsolete markers (are they documented?)
               - EditorBrowsable attributes (API hiding)
               - InternalsVisibleTo (test accessibility)

            Provide a structured API review with recommendations for improvements.
            """;
    }
}
