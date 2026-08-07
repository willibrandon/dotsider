namespace Dotsider.Core.Protocol;

/// <summary>
/// Summary of an mstat source and its matching binary.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record MstatSourceSummaryPayload(
    string Target,
    string? BinaryPath,
    long? BinaryFileSize,
    string MstatPath,
    string? DgmlPath,
    string Format,
    long MstatTotal,
    long? FileSize,
    int EntryCount);
