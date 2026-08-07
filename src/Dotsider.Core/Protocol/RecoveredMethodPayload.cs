namespace Dotsider.Core.Protocol;

/// <summary>
/// A recovered Native AOT method row.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record RecoveredMethodPayload(
    string Source,
    string? AssemblyName,
    string DeclaringType,
    string Name,
    int MethodIndex);
