namespace Dotsider.Core.Protocol;

/// <summary>
/// A method identity extracted from an mstat entry.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record MstatMethodPayload(
    string AssemblyName,
    string Namespace,
    string DeclaringType,
    string Name,
    string? Signature);
