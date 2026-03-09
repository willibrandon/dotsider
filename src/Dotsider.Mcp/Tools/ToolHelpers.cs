using ModelContextProtocol;

namespace Dotsider.Mcp.Tools;

/// <summary>
/// Shared validation helpers for MCP tool methods.
/// </summary>
/// <remarks>
/// These helpers throw <see cref="McpException"/> rather than standard .NET exceptions
/// so that error messages are surfaced directly in the MCP tool response instead of being
/// replaced with a generic "An error occurred invoking" message by the MCP framework.
/// </remarks>
internal static class ToolHelpers
{
    /// <summary>
    /// Validates that an assembly path is provided and points to an existing file.
    /// </summary>
    /// <param name="path">The assembly file path to validate.</param>
    /// <exception cref="McpException">
    /// Thrown when <paramref name="path"/> is null/empty or the file does not exist.
    /// </exception>
    internal static void ValidateAssemblyPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
            throw new McpException("assemblyPath is required when sessionId is not provided.");
        if (!File.Exists(path))
            throw new McpException($"File not found: {path}");
    }

    /// <summary>
    /// Validates that a file path is provided and points to an existing file.
    /// </summary>
    /// <param name="path">The file path to validate.</param>
    /// <param name="label">A descriptive label for the parameter, used in error messages.</param>
    /// <exception cref="McpException">
    /// Thrown when <paramref name="path"/> is null/empty or the file does not exist.
    /// </exception>
    internal static void ValidateFilePath(string? path, string label)
    {
        if (string.IsNullOrEmpty(path))
            throw new McpException($"{label} is required.");
        if (!File.Exists(path))
            throw new McpException($"File not found: {path}");
    }
}
