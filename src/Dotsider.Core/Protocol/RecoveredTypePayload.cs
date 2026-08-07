namespace Dotsider.Core.Protocol;

/// <summary>
/// A recovered Native AOT type row.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record RecoveredTypePayload(
    string Source,
    string? AssemblyName,
    string FullName,
    int MethodCount);
