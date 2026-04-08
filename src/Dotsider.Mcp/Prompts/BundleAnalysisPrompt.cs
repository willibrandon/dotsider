using ModelContextProtocol.Server;

namespace Dotsider.Mcp.Prompts;

/// <summary>
/// MCP prompt for analyzing .NET single-file bundles.
/// </summary>
[McpServerPromptType]
public sealed partial class BundleAnalysisPrompt
{
    /// <summary>
    /// Guided analysis of a .NET single-file bundle: detection, entry listing,
    /// entry assembly analysis, and dependency resolution verification.
    /// </summary>
    /// <param name="assemblyPath">Path to the single-file bundle to analyze.</param>
    /// <returns>Prompt with step-by-step instructions.</returns>
    [McpServerPrompt]
    public static partial string BundleAnalysis(string assemblyPath)
    {
        return $"""
            You are analyzing a potential .NET single-file bundle at: {assemblyPath}

            Follow these steps:

            1. Use get_bundle_info to check if this is a single-file bundle and get its metadata.
            2. Use list_bundle_entries to see all files embedded in the bundle.
            3. Use get_assembly_info with the same path to analyze the entry assembly.
            4. Use get_assembly_refs to see what the entry assembly depends on.
            5. Use resolve_assembly to check if specific dependencies can be found
               (in the bundle, shared framework, or on disk).

            Summarize the bundle structure, its entry assembly, key dependencies,
            and whether all references are resolvable.
            """;
    }
}
