using ModelContextProtocol.Server;

namespace Dotsider.Mcp.Prompts;

/// <summary>
/// MCP prompt that guides a security audit of a .NET assembly.
/// </summary>
[McpServerPromptType]
public sealed partial class SecurityAuditPrompt
{
    /// <summary>
    /// Guides a security audit of a .NET assembly — analyzes custom attributes, dangerous API usage, IL patterns, and dependency risks.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly to audit.</param>
    /// <returns>Prompt text with step-by-step security analysis instructions.</returns>
    [McpServerPrompt]
    public static partial string SecurityAudit(
        string assemblyPath)
    {
        return $"""
            You are performing a security audit of the .NET assembly at: {assemblyPath}

            Follow these steps using the dotsider MCP tools:

            1. **Assembly Overview**: Use get_assembly_info to understand the assembly's purpose, framework, and architecture.

            2. **Dependency Analysis**: Use get_assembly_refs and get_dependency_graph to identify external dependencies. Flag any known-vulnerable or suspicious packages.

            3. **Dangerous API Usage**: Use find_members to search for security-sensitive patterns:
               - Search for "Unsafe", "Marshal", "DllImport", "P/Invoke"
               - Search for "Deserialize", "BinaryFormatter", "XmlSerializer"
               - Search for "Process.Start", "Shell", "Exec"
               - Search for "Cryptography" to verify proper crypto usage

            4. **IL Inspection**: Use search_il_opcodes to find:
               - "call" instructions to dangerous methods
               - "calli" (indirect calls, potential for code injection)
               - "ldsfld" and "stsfld" (mutable static state)

            5. **String Analysis**: Use extract_strings to find:
               - Hardcoded secrets, connection strings, or API keys
               - SQL queries (potential injection)
               - File paths (potential path traversal)

            6. **Custom Attributes**: Use get_custom_attributes to check for:
               - AllowPartiallyTrustedCallers
               - SecurityCritical / SecuritySafeCritical usage
               - SuppressUnmanagedCodeSecurity

            Provide a structured security report with findings categorized by severity (Critical, High, Medium, Low, Info).
            """;
    }
}
