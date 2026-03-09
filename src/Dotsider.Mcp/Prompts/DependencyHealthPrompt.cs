using ModelContextProtocol.Server;

namespace Dotsider.Mcp.Prompts;

/// <summary>
/// MCP prompt that guides a dependency health analysis of a .NET assembly.
/// </summary>
[McpServerPromptType]
public sealed partial class DependencyHealthPrompt
{
    /// <summary>
    /// Guides a dependency health analysis of a .NET assembly — analyzes reference graph, transitive dependencies, and potential issues.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly to analyze.</param>
    /// <returns>Prompt text with step-by-step dependency analysis instructions.</returns>
    [McpServerPrompt]
    public static partial string DependencyHealth(
        string assemblyPath)
    {
        return $"""
            You are analyzing the dependency health of the .NET assembly at: {assemblyPath}

            Follow these steps using the dotsider MCP tools:

            1. **Assembly Info**: Use get_assembly_info for the assembly identity and framework.

            2. **Direct Dependencies**: Use get_assembly_refs to list all direct references. For each:
               - Note the version and whether it's a framework or third-party assembly
               - Flag any very old versions that may have known vulnerabilities

            3. **Dependency Graph**: Use get_dependency_graph to visualize the full dependency tree:
               - Identify diamond dependencies (same assembly referenced via multiple paths)
               - Look for circular references
               - Count total transitive dependency depth

            4. **Type References**: Use get_type_refs to understand which types are actually used from each dependency:
               - Identify dependencies with very few type usages (candidates for removal)
               - Identify heavily-used dependencies (critical dependencies)

            5. **Size Impact**: Use get_size_breakdown to understand each dependency's size contribution.

            6. **Framework Alignment**: Check that all dependencies target compatible frameworks.

            Provide a dependency health report with:
            - Dependency count and depth metrics
            - Risk assessment (high/medium/low) for each dependency
            - Recommendations for dependency upgrades, removals, or replacements
            """;
    }
}
