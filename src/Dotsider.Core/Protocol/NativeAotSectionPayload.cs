namespace Dotsider.Core.Protocol;

/// <summary>
/// A Native AOT module-section row.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record NativeAotSectionPayload(
    int SectionId,
    string Name,
    string Address,
    ulong VirtualAddress,
    long Size,
    int? FileOffset,
    bool IsMapped);
