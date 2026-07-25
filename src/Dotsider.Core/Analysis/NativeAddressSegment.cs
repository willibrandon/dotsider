namespace Dotsider.Core.Analysis;

/// <summary>
/// Describes a validated file-backed segment in a native image.
/// </summary>
/// <param name="VirtualAddress">The segment's starting virtual address.</param>
/// <param name="FileOffset">The segment's starting file offset.</param>
/// <param name="FileSize">The number of file-backed bytes in the segment.</param>
internal readonly record struct NativeAddressSegment(
    ulong VirtualAddress,
    int FileOffset,
    int FileSize);
