using ModelContextProtocol.Server;

namespace Dotsider.Mcp.Prompts;

/// <summary>
/// MCP prompt that guides breaking change detection between two versions of a .NET assembly.
/// </summary>
[McpServerPromptType]
public sealed partial class BreakingChangePrompt
{
    /// <summary>
    /// Guides breaking change detection between two versions of a .NET assembly.
    /// </summary>
    /// <param name="oldAssemblyPath">Path to the old version of the assembly.</param>
    /// <param name="newAssemblyPath">Path to the new version of the assembly.</param>
    /// <returns>Prompt text with step-by-step breaking change analysis instructions.</returns>
    [McpServerPrompt]
    public static partial string BreakingChangeDetection(
        string oldAssemblyPath,
        string newAssemblyPath)
    {
        return $"""
            You are detecting breaking changes between two versions of a .NET assembly.

            Old version: {oldAssemblyPath}
            New version: {newAssemblyPath}

            Follow these steps using the dotsider MCP tools:

            1. **Diff Overview**: Use diff_assemblies to get the full comparison including:
               - Added, removed, and changed types
               - Added, removed, and changed methods
               - Changed assembly references

            2. **Analyze Removed Types**: Any removed public type is a breaking change. List them with their full signatures.

            3. **Analyze Removed Methods**: Any removed public method is a breaking change. Check if they were replaced by overloads.

            4. **Analyze Changed Methods**: Look for:
               - Signature changes (parameter types, return type)
               - New required parameters (without defaults)
               - Changed visibility (public → internal)

            5. **Dependency Changes**: Use get_assembly_refs on both versions to identify:
               - Added dependencies (may affect deployment)
               - Removed dependencies (may break transitive consumers)
               - Version bumps (potential incompatibilities)

            6. **Framework Target Changes**: Compare target frameworks between versions.

            Categorize findings as:
            - **Binary Breaking**: Will cause runtime failures
            - **Source Breaking**: Will cause compilation failures
            - **Behavioral Breaking**: Same API, different behavior
            - **Non-Breaking**: Safe additions and improvements
            """;
    }
}
