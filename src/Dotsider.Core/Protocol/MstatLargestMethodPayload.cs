namespace Dotsider.Core.Protocol;

/// <summary>
/// A large method reported by mstat.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record MstatLargestMethodPayload(
    string Source,
    MstatMethodPayload Method,
    long Size,
    string FullPath,
    IReadOnlyList<string> NodeNames);
