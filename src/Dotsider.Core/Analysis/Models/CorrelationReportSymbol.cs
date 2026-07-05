namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One native symbol carrying a correlated method's compiled code, flattened for the
/// programmatic surfaces (CLI, session, MCP) that report a correlation.
/// </summary>
/// <param name="Name">The symbol's managed name when joined, otherwise its raw name.</param>
/// <param name="VirtualAddress">The symbol's virtual address.</param>
/// <param name="FileOffset">The file offset the code is backed by, or null when not file-backed.</param>
/// <param name="Size">The symbol's size in bytes.</param>
public sealed record CorrelationReportSymbol(
    string Name,
    ulong VirtualAddress,
    long? FileOffset,
    long Size);
