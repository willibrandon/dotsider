namespace Dotsider.Core.Analysis;

/// <summary>
/// Describes one validated Mach-O section.
/// </summary>
/// <param name="Name">The section name.</param>
/// <param name="Segment">The owning segment name.</param>
/// <param name="Address">The section's virtual address.</param>
/// <param name="FileOffset">The section's file offset.</param>
/// <param name="Size">The section's byte size.</param>
/// <param name="Flags">The section flags.</param>
/// <param name="Ordinal">The one-based section ordinal used by symbol records.</param>
/// <param name="IndirectSymbolIndex">The section's first indirect-symbol index.</param>
/// <param name="StubSize">The byte size of each symbol stub.</param>
internal readonly record struct MachOSection(
    string Name,
    string Segment,
    ulong Address,
    long FileOffset,
    long Size,
    uint Flags,
    int Ordinal,
    int IndirectSymbolIndex = 0,
    int StubSize = 0)
{
    /// <summary>The section type stored in the low byte of <see cref="Flags"/>.</summary>
    public uint Type => Flags & 0xFF;

    /// <summary>
    /// Gets a value indicating whether the section carries executable instructions.
    /// </summary>
    public bool IsExecutable => (Flags & 0x8000_0400) != 0;
}
