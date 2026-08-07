namespace Dotsider.Core.Protocol;

/// <summary>
/// Compact ReadyToRun image facts returned by assembly inspection.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record ReadyToRunSummary(
    string Status,
    int MajorVersion,
    int MinorVersion,
    bool IsComposite,
    bool IsComponent,
    bool IsPartialImage,
    string Architecture,
    string? OwnerCompositeExecutable,
    int PrecompiledMethods,
    int InstantiationCount,
    long TotalCodeSize);
