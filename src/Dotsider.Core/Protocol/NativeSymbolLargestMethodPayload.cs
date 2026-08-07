namespace Dotsider.Core.Protocol;

/// <summary>
/// A large method reported by native symbols.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record NativeSymbolLargestMethodPayload(
    string Source,
    NativeSymbolMethodPayload Method,
    long Size,
    long? FileOffset,
    ulong VirtualAddress);
