using System.Buffers.Binary;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Maps virtual addresses to file offsets for a native image (PE, ELF, or Mach-O). The
/// ReadyToRun section table stores each section's location as a virtual address, so walking
/// it requires translating those addresses back to file positions. Regions that exist only
/// in memory (a PE BSS tail, an ELF NOBITS segment, a Mach-O zero-fill section) map to
/// nothing — callers treat that as "not file-backed" rather than an error.
///
/// On Mach-O the section pointers are chained-fixup encoded rather than stored as plain
/// addresses; <see cref="TryDecodeChainedRebase"/> decodes them, and the caller determines
/// which of the two rebase forms the image uses (see the ReadyToRun reader's calibration).
/// </summary>
internal sealed class NativeAddressSpace
{
    private const int CoffHeaderSize = 24;
    private const int Elf64HeaderSize = 64;
    private const int Elf64ProgramHeaderSize = 56;
    private const int MachHeader64Size = 32;
    private const int MachSegment64Size = 72;
    private const int PeSectionHeaderSize = 40;

    /// <summary>The 36-bit target field of a DYLD_CHAINED_PTR_64 rebase pointer.</summary>
    private const ulong ChainedTargetMask = 0xF_FFFF_FFFF;

    private readonly List<NativeAddressSegment> _segments;
    private NativeAddressSpace(
        int pointerSize, List<NativeAddressSegment> segments,
        bool machOChained = false, ulong machOImageBase = 0)
    {
        PointerSize = pointerSize;
        _segments = segments;
        MachOChained = machOChained;
        MachOImageBase = machOImageBase;
    }

    /// <summary>The image's pointer size in bytes (8 for 64-bit, 4 for 32-bit).</summary>
    public int PointerSize { get; }

    /// <summary>Whether this is a Mach-O image whose pointers are chained-fixup encoded.</summary>
    public bool MachOChained { get; }

    /// <summary>The Mach-O image base (the __TEXT segment address); 0 for other formats.</summary>
    public ulong MachOImageBase { get; }

    /// <summary>
    /// Builds an address space from a native image, or returns null when the format is
    /// unrecognized, malformed, truncated, or has no mappable segments.
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
    /// Decodes a Mach-O chained-fixup rebase pointer to the virtual address it targets. The
    /// low 36 bits are the target; <paramref name="offsetForm"/> selects whether that is an
    /// offset from the image base (DYLD_CHAINED_PTR_64_OFFSET, arm64) or an absolute address
    /// (DYLD_CHAINED_PTR_64, x64). Null and import-bind values are returned unchanged.
    /// </summary>
    /// <param name="raw">The encoded pointer.</param>
    /// <param name="offsetForm">Whether the target is relative to <paramref name="imageBase"/>.</param>
    /// <param name="imageBase">The Mach-O image base.</param>
    /// <param name="address">The decoded address.</param>
    /// <returns>True when the decoded address is representable.</returns>
    public static bool TryDecodeChainedRebase(
        ulong raw,
        bool offsetForm,
        ulong imageBase,
        out ulong address)
    {
        if (raw == 0 || (raw >> 63) != 0)
        {
            address = raw;
            return true;
        }

        var target = raw & ChainedTargetMask;
        if (!offsetForm)
        {
            address = (((raw >> 36) & 0xFF) << 56) | target;
            return true;
        }

        return NativeImageRange.TryAdd(imageBase, target, out address);
    }

    /// <summary>
    /// Translates a virtual address to a file offset when it falls inside file-backed data.
    /// The address must already be decoded (see <see cref="TryDecodeChainedRebase"/> for Mach-O).
    /// </summary>
    /// <param name="virtualAddress">The virtual address to translate.</param>
    /// <param name="fileOffset">The resulting file offset.</param>
    /// <param name="available">Bytes available from <paramref name="fileOffset"/> to the end of the containing segment.</param>
    /// <returns>True when the address maps into file-backed data.</returns>
    public bool TryGetFileOffset(ulong virtualAddress, out int fileOffset, out int available)
    {
        foreach (var segment in _segments)
        {
            if (virtualAddress < segment.VirtualAddress) continue;
            var delta = virtualAddress - segment.VirtualAddress;
            if (delta >= (ulong)segment.FileSize) continue;

            fileOffset = segment.FileOffset + (int)delta;
            available = segment.FileSize - (int)delta;
            return true;
        }

        fileOffset = 0;
        available = 0;
        return false;
    }

    /// <summary>
    /// Gets the bytes remaining in the file-backed segment containing a file offset.
    /// </summary>
    /// <param name="fileOffset">The file offset to locate.</param>
    /// <param name="available">Bytes remaining from the offset to the end of its segment.</param>
    /// <returns>True when the offset belongs to a validated file-backed segment.</returns>
    public bool TryGetAvailableBytes(int fileOffset, out int available)
    {
        foreach (var segment in _segments)
        {
            if (fileOffset < segment.FileOffset)
            {
                continue;
            }

            var delta = fileOffset - segment.FileOffset;
            if (delta >= segment.FileSize)
            {
                continue;
            }

            available = segment.FileSize - delta;
            return true;
        }

        available = 0;
        return false;
    }

    private static NativeAddressSpace? CreatePe(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 0x40) return null;
        var peHeaderValue = BinaryPrimitives.ReadUInt32LittleEndian(bytes[0x3C..]);
        if (peHeaderValue == 0
            || !NativeImageRange.TryGet(
                bytes.Length,
                peHeaderValue,
                CoffHeaderSize,
                out var peHeader,
                out _))
        {
            return null;
        }

        if (BinaryPrimitives.ReadUInt32LittleEndian(bytes[peHeader..]) != 0x00004550) return null; // "PE\0\0"

        var sectionCount = BinaryPrimitives.ReadUInt16LittleEndian(bytes[(peHeader + 6)..]);
        var optionalHeaderSize = BinaryPrimitives.ReadUInt16LittleEndian(bytes[(peHeader + 20)..]);
        var optionalHeader = peHeader + CoffHeaderSize;
        if (!NativeImageRange.TryGet(
                bytes.Length,
                (ulong)optionalHeader,
                optionalHeaderSize,
                out _,
                out _)
            || optionalHeaderSize < 32)
        {
            return null;
        }

        var magic = BinaryPrimitives.ReadUInt16LittleEndian(bytes[optionalHeader..]);
        if (magic is not (0x10B or 0x20B)) return null;

        var is64Bit = magic == 0x20B;
        var minimumOptionalHeaderSize = is64Bit ? 112 : 96;
        if (optionalHeaderSize < minimumOptionalHeaderSize) return null;
        var pointerSize = is64Bit ? 8 : 4;

        // ImageBase: u64 at optional+24 (PE32+) or u32 at optional+28 (PE32)
        ulong imageBase = is64Bit
            ? BinaryPrimitives.ReadUInt64LittleEndian(bytes[(optionalHeader + 24)..])
            : BinaryPrimitives.ReadUInt32LittleEndian(bytes[(optionalHeader + 28)..]);

        var sectionTable = optionalHeader + optionalHeaderSize;
        if (!NativeImageRange.TryGetTable(
                bytes.Length,
                (ulong)sectionTable,
                sectionCount,
                PeSectionHeaderSize,
                PeSectionHeaderSize,
                out _,
                out _))
        {
            return null;
        }

        var segments = new List<NativeAddressSegment>(sectionCount);
        for (var i = 0; i < sectionCount; i++)
        {
            var entry = sectionTable + i * PeSectionHeaderSize;

            var virtualAddress = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(entry + 12)..]);
            var rawSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(entry + 16)..]);
            var rawOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(entry + 20)..]);
            if (rawSize == 0) continue;

            if (!NativeImageRange.TryGet(
                    bytes.Length,
                    rawOffset,
                    rawSize,
                    out var fileOffset,
                    out var fileSize)
                || !NativeImageRange.TryAdd(imageBase, virtualAddress, out var sectionAddress)
                || !NativeImageRange.TryAdd(sectionAddress, rawSize, out _))
            {
                return null;
            }

            segments.Add(new NativeAddressSegment(sectionAddress, fileOffset, fileSize));
        }

        return segments.Count > 0 ? new NativeAddressSpace(pointerSize, segments) : null;
    }

    private static NativeAddressSpace? CreateElf(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < Elf64HeaderSize || bytes[5] != 1) return null; // 64-bit little-endian only
        if (bytes[4] != 2) return null;

        var programHeaderOffset = BinaryPrimitives.ReadUInt64LittleEndian(bytes[0x20..]);
        var entrySize = BinaryPrimitives.ReadUInt16LittleEndian(bytes[0x36..]);
        var entryCount = BinaryPrimitives.ReadUInt16LittleEndian(bytes[0x38..]);
        if (programHeaderOffset == 0
            || entryCount == 0
            || !NativeImageRange.TryGetTable(
                bytes.Length,
                programHeaderOffset,
                entryCount,
                entrySize,
                Elf64ProgramHeaderSize,
                out var tableOffset,
                out _))
        {
            return null;
        }

        var segments = new List<NativeAddressSegment>(entryCount);
        for (var i = 0; i < entryCount; i++)
        {
            var entry = tableOffset + i * entrySize;

            var type = BinaryPrimitives.ReadUInt32LittleEndian(bytes[entry..]);
            if (type != 1) continue; // PT_LOAD

            var fileOffsetValue = BinaryPrimitives.ReadUInt64LittleEndian(bytes[(entry + 8)..]);
            var virtualAddress = BinaryPrimitives.ReadUInt64LittleEndian(bytes[(entry + 16)..]);
            var fileSizeValue = BinaryPrimitives.ReadUInt64LittleEndian(bytes[(entry + 32)..]);
            var memorySize = BinaryPrimitives.ReadUInt64LittleEndian(bytes[(entry + 40)..]);
            if (fileSizeValue > memorySize) return null;
            if (fileSizeValue == 0) continue;

            if (!NativeImageRange.TryGet(
                    bytes.Length,
                    fileOffsetValue,
                    fileSizeValue,
                    out var fileOffset,
                    out var fileSize)
                || !NativeImageRange.TryAdd(virtualAddress, fileSizeValue, out _))
            {
                return null;
            }

            segments.Add(new NativeAddressSegment(virtualAddress, fileOffset, fileSize));
        }

        return segments.Count > 0 ? new NativeAddressSpace(8, segments) : null;
    }

    private static NativeAddressSpace? CreateMachO(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < MachHeader64Size) return null;
        var commandCount = BinaryPrimitives.ReadUInt32LittleEndian(bytes[16..]);
        var commandsSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes[20..]);
        if (commandCount > 4096
            || !NativeImageRange.TryGet(
                bytes.Length,
                MachHeader64Size,
                commandsSize,
                out var command,
                out var commandsLength))
        {
            return null;
        }

        const uint lcSegment64 = 0x19;
        const uint lcDyldChainedFixups = 0x80000034;

        var commandsEnd = command + commandsLength;
        var segments = new List<NativeAddressSegment>();
        var chained = false;
        var imageBase = ulong.MaxValue;
        for (var i = 0; i < commandCount; i++)
        {
            if (!NativeImageRange.TryGet(
                    commandsEnd,
                    (ulong)command,
                    8,
                    out _,
                    out _))
            {
                return null;
            }

            var cmd = BinaryPrimitives.ReadUInt32LittleEndian(bytes[command..]);
            var cmdSizeValue = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(command + 4)..]);
            if (cmdSizeValue < 8
                || (cmdSizeValue & 7) != 0
                || !NativeImageRange.TryGet(
                    commandsEnd,
                    (ulong)command,
                    cmdSizeValue,
                    out _,
                    out var cmdSize))
            {
                return null;
            }

            if (cmd == lcDyldChainedFixups && cmdSize >= 16)
                chained = true;

            if (cmd == lcSegment64)
            {
                if (cmdSize < MachSegment64Size) return null;

                var virtualAddress = BinaryPrimitives.ReadUInt64LittleEndian(bytes[(command + 24)..]);
                var memorySize = BinaryPrimitives.ReadUInt64LittleEndian(bytes[(command + 32)..]);
                var fileOffsetValue = BinaryPrimitives.ReadUInt64LittleEndian(bytes[(command + 40)..]);
                var fileSizeValue = BinaryPrimitives.ReadUInt64LittleEndian(bytes[(command + 48)..]);
                if (fileSizeValue > memorySize) return null;
                if (fileSizeValue > 0)
                {
                    if (!NativeImageRange.TryGet(
                            bytes.Length,
                            fileOffsetValue,
                            fileSizeValue,
                            out var fileOffset,
                            out var fileSize)
                        || !NativeImageRange.TryAdd(virtualAddress, fileSizeValue, out _))
                    {
                        return null;
                    }

                    segments.Add(new NativeAddressSegment(virtualAddress, fileOffset, fileSize));
                    // The image base is the mach header's address — the lowest file-backed
                    // segment (__TEXT); chained rebase offsets are relative to it.
                    if (virtualAddress < imageBase) imageBase = virtualAddress;
                }
            }

            command += cmdSize;
        }

        if (command != commandsEnd) return null;

        return segments.Count > 0
            ? new NativeAddressSpace(8, segments, chained,
                imageBase == ulong.MaxValue ? 0 : imageBase)
            : null;
    }

}
