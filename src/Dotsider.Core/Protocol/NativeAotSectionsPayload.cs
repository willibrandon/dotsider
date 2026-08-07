namespace Dotsider.Core.Protocol;

/// <summary>
/// A Native AOT module-section inventory.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record NativeAotSectionsPayload(
    string FilePath,
    int SectionCount,
    IReadOnlyList<NativeAotSectionPayload> Sections);
