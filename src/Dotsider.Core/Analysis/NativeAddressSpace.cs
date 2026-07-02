using System.Buffers.Binary;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Maps virtual addresses to file offsets for a native image (PE, ELF, or Mach-O).
/// The ReadyToRun section table stores each section's location as an absolute virtual
/// address, so walking it requires translating those addresses back to file positions.
/// Regions that exist only in memory (a PE BSS tail, an ELF NOBITS segment) map to
/// nothing — callers treat that as "not file-backed" rather than an error.
/// </summary>
internal sealed class NativeAddressSpace
{
    /// <summary>The 36-bit target field of a DYLD_CHAINED_PTR_64 rebase pointer.</summary>
    private const ulong ChainedTargetMask = 0xF_FFFF_FFFF;

    // dyld chained pointer formats (mach-o/fixup-chains.h).
    private const int DyldChainedPtr64 = 2;         // rebase target is an absolute unslid address
    private const int DyldChainedPtr64Offset = 6;   // rebase target is an offset from the image base

    private readonly List<Segment> _segments;
    private readonly int _byteLength;
    private readonly ulong _machOImageBase;
    private readonly int _machOPointerFormat;
    private readonly bool _machOChained;

    private NativeAddressSpace(
        int pointerSize, List<Segment> segments, int byteLength,
        bool machOChained = false, int machOPointerFormat = 0, ulong machOImageBase = 0)
    {
        PointerSize = pointerSize;
        _segments = segments;
        _byteLength = byteLength;
        _machOChained = machOChained;
        _machOPointerFormat = machOPointerFormat;
        _machOImageBase = machOImageBase;
    }

    /// <summary>The image's pointer size in bytes (8 for 64-bit, 4 for 32-bit).</summary>
    public int PointerSize { get; }

    /// <summary>
    /// Builds an address space from a native image, or returns null when the format is
    /// unrecognized or has no mappable segments.
    /// </summary>
    /// <param name="bytes">The raw bytes of the image.</param>
    public static NativeAddressSpace? Create(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 4) return null;

        if (bytes[0] == (byte)'M' && bytes[1] == (byte)'Z')
            return CreatePe(bytes);
        if (bytes[0] == 0x7F && bytes[1] == (byte)'E' && bytes[2] == (byte)'L' && bytes[3] == (byte)'F')
            return CreateElf(bytes);

        var machMagic = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        if (machMagic is 0xFEEDFACF) // 64-bit little-endian Mach-O
            return CreateMachO(bytes);

        return null;
    }

    /// <summary>
    /// Translates a virtual address to a file offset when it falls inside file-backed data.
    /// On Mach-O the value may be a chained-fixup pointer, so the raw and decoded
    /// interpretations are each tried.
    /// </summary>
    /// <param name="virtualAddress">The virtual address (possibly a chained-fixup pointer).</param>
    /// <param name="fileOffset">The resulting file offset.</param>
    /// <param name="available">Bytes available from <paramref name="fileOffset"/> to the end of the containing segment.</param>
    /// <returns>True when the address maps into file-backed data.</returns>
    public bool TryGetFileOffset(ulong virtualAddress, out int fileOffset, out int available)
    {
        if (TryMap(ResolvePointer(virtualAddress), out fileOffset, out available)) return true;

        // Fallback for a chained image whose pointer format could not be read: try the two
        // rebase interpretations and accept whichever maps. This keeps file-backed lookups
        // (metadata, headers) working even when the deterministic decode is unavailable.
        if (_machOChained && _machOPointerFormat == 0 && virtualAddress != 0)
        {
            var target = virtualAddress & ChainedTargetMask;
            if (TryMap(target, out fileOffset, out available)) return true;
            if (TryMap(_machOImageBase + target, out fileOffset, out available)) return true;
        }

        return false;
    }

    /// <summary>
    /// Resolves a pointer value to the virtual address it targets, decoding a Mach-O
    /// chained-fixup rebase pointer when the image uses them. The decode is deterministic
    /// (driven by the image's pointer format) so it resolves correctly even for targets in a
    /// zero-fill region that has no file backing. Returns the input unchanged when it is
    /// zero, the image is not chained, or the pointer is an import bind rather than a rebase.
    /// </summary>
    /// <param name="pointer">The raw pointer value read from the image.</param>
    public ulong ResolvePointer(ulong pointer)
    {
        if (pointer == 0) return 0;

        // The high bit marks an import bind (no local target); leave it for the caller.
        if (_machOPointerFormat != 0 && (pointer >> 63) == 0)
        {
            var target = pointer & ChainedTargetMask;
            return _machOPointerFormat switch
            {
                DyldChainedPtr64Offset => _machOImageBase + target,
                DyldChainedPtr64 => (((pointer >> 36) & 0xFF) << 56) | target,
                _ => pointer,
            };
        }

        return pointer;
    }

    private bool TryMap(ulong virtualAddress, out int fileOffset, out int available)
    {
        foreach (var segment in _segments)
        {
            if (virtualAddress < segment.VirtualAddress) continue;
            var delta = virtualAddress - segment.VirtualAddress;
            if (delta >= (ulong)segment.FileSize) continue;

            fileOffset = segment.FileOffset + (int)delta;
            if (fileOffset < 0 || fileOffset > _byteLength)
            {
                fileOffset = 0;
                available = 0;
                return false;
            }

            available = Math.Min(segment.FileSize - (int)delta, _byteLength - fileOffset);
            return true;
        }

        fileOffset = 0;
        available = 0;
        return false;
    }

    private static NativeAddressSpace? CreatePe(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 0x40) return null;
        var peHeader = BinaryPrimitives.ReadInt32LittleEndian(bytes[0x3C..]);
        if (peHeader <= 0 || peHeader + 24 > bytes.Length) return null;
        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes[peHeader..]) != 0x00004550) return null; // "PE\0\0"

        var sectionCount = BinaryPrimitives.ReadUInt16LittleEndian(bytes[(peHeader + 6)..]);
        var optionalHeaderSize = BinaryPrimitives.ReadUInt16LittleEndian(bytes[(peHeader + 20)..]);
        var optionalHeader = peHeader + 24;
        if (optionalHeader + 2 > bytes.Length) return null;

        var magic = BinaryPrimitives.ReadUInt16LittleEndian(bytes[optionalHeader..]);
        var is64Bit = magic == 0x20B;
        var pointerSize = is64Bit ? 8 : 4;

        // ImageBase: u64 at optional+24 (PE32+) or u32 at optional+28 (PE32)
        ulong imageBase = is64Bit
            ? BinaryPrimitives.ReadUInt64LittleEndian(bytes[(optionalHeader + 24)..])
            : BinaryPrimitives.ReadUInt32LittleEndian(bytes[(optionalHeader + 28)..]);

        var sectionTable = optionalHeader + optionalHeaderSize;
        var segments = new List<Segment>(sectionCount);
        for (var i = 0; i < sectionCount; i++)
        {
            var entry = sectionTable + i * 40;
            if (entry + 40 > bytes.Length) break;

            var virtualAddress = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(entry + 12)..]);
            var rawSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(entry + 16)..]);
            var rawOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(entry + 20)..]);
            if (rawSize == 0) continue;

            segments.Add(new Segment(imageBase + virtualAddress, (int)rawOffset, (int)rawSize));
        }

        return segments.Count > 0 ? new NativeAddressSpace(pointerSize, segments, bytes.Length) : null;
    }

    private static NativeAddressSpace? CreateElf(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 64 || bytes[5] != 1) return null; // 64-bit little-endian only
        if (bytes[4] != 2) return null;

        var programHeaderOffset = (long)BinaryPrimitives.ReadUInt64LittleEndian(bytes[0x20..]);
        var entrySize = BinaryPrimitives.ReadUInt16LittleEndian(bytes[0x36..]);
        var entryCount = BinaryPrimitives.ReadUInt16LittleEndian(bytes[0x38..]);
        if (programHeaderOffset <= 0 || entrySize < 56) return null;

        var segments = new List<Segment>(entryCount);
        for (var i = 0; i < entryCount; i++)
        {
            var entry = programHeaderOffset + (long)i * entrySize;
            if (entry + 56 > bytes.Length) break;

            var type = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(int)entry..]);
            if (type != 1) continue; // PT_LOAD

            var fileOffset = (long)BinaryPrimitives.ReadUInt64LittleEndian(bytes[(int)(entry + 8)..]);
            var virtualAddress = BinaryPrimitives.ReadUInt64LittleEndian(bytes[(int)(entry + 16)..]);
            var fileSize = (long)BinaryPrimitives.ReadUInt64LittleEndian(bytes[(int)(entry + 32)..]);
            if (fileSize == 0) continue;

            segments.Add(new Segment(virtualAddress, (int)fileOffset, (int)fileSize));
        }

        return segments.Count > 0 ? new NativeAddressSpace(8, segments, bytes.Length) : null;
    }

    private static NativeAddressSpace? CreateMachO(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 32) return null;
        var commandCount = BinaryPrimitives.ReadUInt32LittleEndian(bytes[16..]);
        if (commandCount > 4096) return null;

        const uint lcSegment64 = 0x19;
        const uint lcDyldChainedFixups = 0x80000034;

        var segments = new List<Segment>();
        var chained = false;
        var pointerFormat = 0;
        var imageBase = ulong.MaxValue;
        var command = 32;
        for (var i = 0; i < commandCount; i++)
        {
            if (command + 8 > bytes.Length) break;
            var cmd = BinaryPrimitives.ReadUInt32LittleEndian(bytes[command..]);
            var cmdSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[(command + 4)..]);
            if (cmdSize < 8 || command + cmdSize > bytes.Length) break;

            if (cmd == lcDyldChainedFixups && command + 16 <= bytes.Length)
            {
                chained = true;
                var dataOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[(command + 8)..]);
                pointerFormat = ReadChainedPointerFormat(bytes, dataOffset);
            }

            if (cmd == lcSegment64 && command + 56 <= bytes.Length)
            {
                var virtualAddress = BinaryPrimitives.ReadUInt64LittleEndian(bytes[(command + 24)..]);
                var fileOffset = (long)BinaryPrimitives.ReadUInt64LittleEndian(bytes[(command + 40)..]);
                var fileSize = (long)BinaryPrimitives.ReadUInt64LittleEndian(bytes[(command + 48)..]);
                if (fileSize > 0)
                {
                    segments.Add(new Segment(virtualAddress, (int)fileOffset, (int)fileSize));
                    // The image base is the mach header's address — the lowest file-backed
                    // segment (__TEXT); chained rebase offsets are relative to it.
                    if (virtualAddress < imageBase) imageBase = virtualAddress;
                }
            }

            command += cmdSize;
        }

        return segments.Count > 0
            ? new NativeAddressSpace(8, segments, bytes.Length, chained, pointerFormat,
                imageBase == ulong.MaxValue ? 0 : imageBase)
            : null;
    }

    /// <summary>
    /// Reads the chained-fixup pointer format from an <c>LC_DYLD_CHAINED_FIXUPS</c> payload:
    /// the fixups header points to a starts-in-image table, whose first segment records the
    /// format shared by the image's rebased pointers. Returns 0 when it cannot be read.
    /// </summary>
    private static int ReadChainedPointerFormat(ReadOnlySpan<byte> bytes, int fixupsDataOffset)
    {
        if (fixupsDataOffset <= 0 || fixupsDataOffset + 8 > bytes.Length) return 0;

        var startsOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[(fixupsDataOffset + 4)..]);
        var starts = fixupsDataOffset + startsOffset; // dyld_chained_starts_in_image
        if (starts <= 0 || starts + 4 > bytes.Length) return 0;

        var segCount = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[starts..]);
        if (segCount is < 1 or > 4096) return 0;

        for (var s = 0; s < segCount; s++)
        {
            var slot = starts + 4 + s * 4;
            if (slot + 4 > bytes.Length) break;

            var segInfoOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[slot..]);
            if (segInfoOffset == 0) continue; // segment has no fixups

            // dyld_chained_starts_in_segment: size(u32), page_size(u16), pointer_format(u16), ...
            var pointerFormatField = starts + segInfoOffset + 6;
            if (pointerFormatField + 2 > bytes.Length) break;
            return BinaryPrimitives.ReadUInt16LittleEndian(bytes[pointerFormatField..]);
        }

        return 0;
    }

    private readonly record struct Segment(ulong VirtualAddress, int FileOffset, int FileSize);
}
