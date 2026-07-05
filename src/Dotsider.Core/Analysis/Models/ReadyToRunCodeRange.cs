namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One contiguous block of a precompiled ReadyToRun method's native code, derived from a single
/// runtime function. A method body is not one slice: it is the ordered list of these ranges
/// (hot entry, funclets, cold), each of which is disassembled, sized, and navigated on its own.
/// </summary>
/// <param name="Kind">Whether this range is the hot entry, a funclet, or the cold range.</param>
/// <param name="StartRva">The range's relative virtual address (machine-specific fixups already applied).</param>
/// <param name="Size">The range size in bytes.</param>
/// <param name="VirtualAddress">The range's absolute virtual address (image base + <paramref name="StartRva"/>) in its code image.</param>
/// <param name="FileOffset">The file offset of the range within its code image, or null when not file-backed.</param>
public sealed record ReadyToRunCodeRange(
    ReadyToRunCodeRangeKind Kind,
    int StartRva,
    long Size,
    ulong VirtualAddress,
    int? FileOffset);
