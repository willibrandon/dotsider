namespace Dotsider.Core.Protocol;

/// <summary>
/// Compact Webcil container facts.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record WebcilSummary(
    int VersionMajor,
    int VersionMinor,
    bool IsWasmWrapped,
    long PayloadOffset,
    int SectionCount,
    int MetadataSize,
    int DebugDirectorySize);
