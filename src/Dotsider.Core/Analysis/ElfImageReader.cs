using Dotsider.Core.Analysis.Models;
using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Reads the dependency libraries and dynamic symbols of a 64-bit ELF image — the
/// import/export analog of the PE data directories for Linux Native AOT output.
/// Imports are the needed shared objects (each with the undefined symbols attributed
/// to it via GNU version requirements); exports are the defined global symbols.
/// Malformed base tables yield empty results rather than throwing. Malformed GNU
/// version requirements retain readable imports without unsafe library attribution.
/// </summary>
internal static class ElfImageReader
{
    private const int Elf64HeaderSize = 64;
    private const int SectionHeaderSize = 64;
    private const int SymbolSize = 24;
    private const int MaxSymbols = 262_144;
    private const int MaxStringLength = 4_096;

    private const uint ShtDynSym = 11;
    private const uint ShtNoBits = 8;
    private const int StbGlobal = 1;
    private const int StbWeak = 2;
    private const ushort ShnUndef = 0;
    private const ulong ShfAlloc = 0x2;
    private const ulong ShfCompressed = 0x800;
    private const uint ElfCompressZlib = 1;

    /// <summary>Returns true if the bytes are a 64-bit ELF image.</summary>
    internal static bool IsElf(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= Elf64HeaderSize
        && bytes[0] == 0x7F && bytes[1] == (byte)'E' && bytes[2] == (byte)'L' && bytes[3] == (byte)'F'
        && bytes[4] == 2; // ELFCLASS64

    /// <summary>
    /// Walks the section headers into named sections, or an empty list when the image is not a
    /// little-endian 64-bit ELF or carries no section headers.
    /// </summary>
    /// <param name="bytes">The raw image bytes.</param>
    internal static IReadOnlyList<ElfSection> ReadSections(ReadOnlySpan<byte> bytes)
    {
        try
        {
            if (!IsElf(bytes) || bytes[5] != 1) return [];

            var sectionOffset = (long)BinaryPrimitives.ReadUInt64LittleEndian(bytes[40..]);
            var sectionEntrySize = BinaryPrimitives.ReadUInt16LittleEndian(bytes[58..]);
            var sectionCount = BinaryPrimitives.ReadUInt16LittleEndian(bytes[60..]);
            var stringSectionIndex = BinaryPrimitives.ReadUInt16LittleEndian(bytes[62..]);
            if (sectionOffset <= 0 || sectionEntrySize < SectionHeaderSize || sectionCount == 0)
                return [];

            var shStrTableOffset = (long)BinaryPrimitives.ReadUInt64LittleEndian(
                bytes[(int)(sectionOffset + (long)stringSectionIndex * sectionEntrySize + 24)..]);

            var sections = new List<ElfSection>(sectionCount);
            for (var i = 0; i < sectionCount; i++)
            {
                var header = sectionOffset + (long)i * sectionEntrySize;
                if (header + SectionHeaderSize > bytes.Length) break;
                var nameOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(int)header..]);
                var type = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(int)(header + 4)..]);
                var flags = BinaryPrimitives.ReadUInt64LittleEndian(bytes[(int)(header + 8)..]);
                var address = BinaryPrimitives.ReadUInt64LittleEndian(bytes[(int)(header + 16)..]);
                var offset = (int)BinaryPrimitives.ReadUInt64LittleEndian(bytes[(int)(header + 24)..]);
                var size = (int)BinaryPrimitives.ReadUInt64LittleEndian(bytes[(int)(header + 32)..]);
                var link = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(int)(header + 40)..]);
                var info = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(int)(header + 44)..]);
                var name = ReadString(bytes, shStrTableOffset, nameOffset) ?? "";
                sections.Add(new ElfSection(name, type, address, offset, size, link, info, flags));
            }

            return sections;
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            return [];
        }
    }

    /// <summary>Finds a section by name.</summary>
    /// <param name="bytes">The raw image bytes.</param>
    /// <param name="name">The section name (e.g. <c>.debug_info</c>).</param>
    /// <param name="section">The section when found.</param>
    internal static bool TryGetSection(ReadOnlySpan<byte> bytes, string name, out ElfSection section)
    {
        foreach (var candidate in ReadSections(bytes))
        {
            if (candidate.Name == name)
            {
                section = candidate;
                return true;
            }
        }

        section = default;
        return false;
    }

    /// <summary>
    /// Reads the needed shared libraries and the undefined symbols imported from each.
    /// </summary>
    internal static IReadOnlyList<ImportedModuleInfo> ReadImports(ReadOnlySpan<byte> bytes)
    {
        try
        {
            if (!IsElf(bytes)) return [];
            var elf = ElfLayout.Parse(bytes);
            if (elf is not { } layout) return [];

            var modules = new Dictionary<string, List<ImportedFunctionInfo>>(StringComparer.Ordinal);

            // Seed with the declared dependencies so libraries with no attributed
            // symbols still appear.
            foreach (var needed in layout.Needed)
                modules.TryAdd(needed, []);

            var symbolCount = layout.DynSym.Size / SymbolSize;
            for (var i = 0; i < symbolCount && i < MaxSymbols; i++)
            {
                var symbolOffset = layout.DynSym.Offset + i * SymbolSize;
                var nameOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes[symbolOffset..]);
                var sectionIndex = BinaryPrimitives.ReadUInt16LittleEndian(bytes[(symbolOffset + 6)..]);
                if (sectionIndex != ShnUndef) continue;

                var name = ReadBoundedString(
                    bytes,
                    layout.DynStr.Offset,
                    layout.DynStr.Size,
                    nameOffset);
                if (string.IsNullOrEmpty(name)) continue;

                var library = layout.ResolveVersionLibrary(bytes, i) ?? "(unversioned)";
                if (!modules.TryGetValue(library, out var functions))
                {
                    functions = [];
                    modules[library] = functions;
                }

                functions.Add(new ImportedFunctionInfo(name, Ordinal: null, Hint: null));
            }

            return [.. modules.Select(m => new ImportedModuleInfo(m.Key, m.Value))];
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            return [];
        }
    }

    /// <summary>Reads the defined global and weak dynamic symbols (the exported symbols).</summary>
    internal static IReadOnlyList<ExportedFunctionInfo> ReadExports(ReadOnlySpan<byte> bytes)
    {
        try
        {
            if (!IsElf(bytes)) return [];
            var elf = ElfLayout.Parse(bytes);
            if (elf is not { } layout) return [];

            var exports = new List<ExportedFunctionInfo>();
            var symbolCount = layout.DynSym.Size / SymbolSize;
            for (var i = 0; i < symbolCount && i < MaxSymbols; i++)
            {
                var symbolOffset = layout.DynSym.Offset + i * SymbolSize;
                var nameOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes[symbolOffset..]);
                var info = bytes[symbolOffset + 4];
                var sectionIndex = BinaryPrimitives.ReadUInt16LittleEndian(bytes[(symbolOffset + 6)..]);
                var value = BinaryPrimitives.ReadUInt64LittleEndian(bytes[(symbolOffset + 8)..]);
                if (sectionIndex == ShnUndef) continue;

                var bind = info >> 4;
                if (bind != StbGlobal && bind != StbWeak) continue;

                var name = ReadBoundedString(
                    bytes,
                    layout.DynStr.Offset,
                    layout.DynStr.Size,
                    nameOffset);
                if (string.IsNullOrEmpty(name)) continue;

                exports.Add(new ExportedFunctionInfo(
                    Ordinal: i, Name: name, Rva: (int)value, ForwardedTo: null));
            }

            return exports;
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            return [];
        }
    }

    /// <summary>
    /// Materializes a section's content, transparently inflating <c>SHF_COMPRESSED</c> zlib
    /// payloads — GNU toolchains compress debug sections by default, prefixing them with an
    /// <c>Elf64_Chdr</c>. Unsupported compression (zstd) and malformed payloads yield null so
    /// the section reads as absent rather than as garbage.
    /// </summary>
    /// <param name="bytes">The raw image bytes.</param>
    /// <param name="section">The section to read.</param>
    /// <param name="maximumMaterializedLength">
    /// The remaining byte budget for materializing this and any sibling sections.
    /// </param>
    internal static byte[]? ReadSectionBytes(
        ReadOnlyMemory<byte> bytes,
        ElfSection section,
        int maximumMaterializedLength)
    {
        maximumMaterializedLength = Math.Min(
            maximumMaterializedLength,
            NativeImageDataLimits.MaxMaterializedBytes);
        ReadOnlySpan<byte> span = bytes.Span;
        if (section.Size <= 0 || section.FileOffset < 0
            || maximumMaterializedLength <= 0
            || section.FileOffset > span.Length
            || section.Size > span.Length - section.FileOffset)
        {
            return null;
        }

        ReadOnlySpan<byte> raw = span.Slice(section.FileOffset, section.Size);
        if ((section.Flags & ShfCompressed) == 0)
            return raw.Length <= maximumMaterializedLength ? raw.ToArray() : null;

        // Elf64_Chdr: ch_type u32, ch_reserved u32, ch_size u64, ch_addralign u64.
        if (raw.Length <= 24
            || section.Type == ShtNoBits
            || (section.Flags & ShfAlloc) != 0)
        {
            return null;
        }

        var compressionType = BinaryPrimitives.ReadUInt32LittleEndian(raw);
        var reserved = BinaryPrimitives.ReadUInt32LittleEndian(raw[4..]);
        var uncompressedSize = BinaryPrimitives.ReadUInt64LittleEndian(raw[8..]);
        var uncompressedAlignment = BinaryPrimitives.ReadUInt64LittleEndian(raw[16..]);
        var compressedLength = raw.Length - 24;
        int maximumCompressedLength = Math.Min(
            NativeImageDataLimits.MaxMaterializedBytes + NativeImageDataLimits.MaxCompressedOverheadBytes,
            maximumMaterializedLength + NativeImageDataLimits.MaxCompressedOverheadBytes);
        if (compressionType != ElfCompressZlib
            || reserved != 0
            || uncompressedSize == 0
            || uncompressedSize > (ulong)maximumMaterializedLength
            || uncompressedSize > (ulong)compressedLength * NativeImageDataLimits.MaxCompressionRatio
            || (uncompressedAlignment != 0 && !BitOperations.IsPow2(uncompressedAlignment))
            || compressedLength > maximumCompressedLength)
        {
            return null;
        }

        return BoundedCompressionDecoder.TryDecodeZLib(
            bytes,
            section.FileOffset + 24,
            compressedLength,
            (int)uncompressedSize,
            maximumCompressedLength,
            maximumMaterializedLength,
            out byte[] inflated)
            ? inflated
            : null;
    }

    /// <summary>
    /// Maps a virtual address to its containing section's name and the corresponding file
    /// offset, or false when no mapped section covers it.
    /// </summary>
    /// <param name="sections">The image's sections.</param>
    /// <param name="va">The virtual address to map.</param>
    /// <param name="sectionName">The containing section's name.</param>
    /// <param name="fileOffset">The file offset the address maps to.</param>
    internal static bool TryMapAddress(
        IReadOnlyList<ElfSection> sections, ulong va, out string sectionName, out long fileOffset)
    {
        foreach (var section in sections)
        {
            if (section.Address != 0 && va >= section.Address && va < section.Address + (ulong)section.Size)
            {
                sectionName = section.Name;
                fileOffset = section.FileOffset + (long)(va - section.Address);
                return true;
            }
        }

        sectionName = "";
        fileOffset = 0;
        return false;
    }

    /// <summary>
    /// Reads the GNU build id from the <c>.note.gnu.build-id</c> section — the note whose owner
    /// is <c>GNU</c> and type is 3 — walking past any other notes sharing the section.
    /// </summary>
    /// <param name="bytes">The raw image bytes.</param>
    /// <param name="buildId">The build id payload when found.</param>
    internal static bool TryReadBuildId(ReadOnlySpan<byte> bytes, out byte[] buildId)
    {
        buildId = [];
        if (!TryGetSection(bytes, ".note.gnu.build-id", out var section)) return false;
        if (section.FileOffset < 0 || section.FileOffset + section.Size > bytes.Length) return false;

        var data = bytes.Slice(section.FileOffset, section.Size);
        var position = 0;
        while (position + 12 <= data.Length)
        {
            var nameSize = BinaryPrimitives.ReadUInt32LittleEndian(data[position..]);
            var descSize = BinaryPrimitives.ReadUInt32LittleEndian(data[(position + 4)..]);
            var type = BinaryPrimitives.ReadUInt32LittleEndian(data[(position + 8)..]);
            var namePosition = position + 12;
            var descPosition = namePosition + (int)((nameSize + 3) & ~3u);
            var next = descPosition + (int)((descSize + 3) & ~3u);
            if (nameSize > (uint)data.Length || descSize > (uint)data.Length || next > data.Length) return false;

            if (type == 3 && nameSize == 4 && data.Slice(namePosition, 4).SequenceEqual("GNU\0"u8))
            {
                buildId = data.Slice(descPosition, (int)descSize).ToArray();
                return buildId.Length > 0;
            }

            position = next;
        }

        return false;
    }

    /// <summary>
    /// Reads the <c>.gnu_debuglink</c> section: the sidecar's file name, then — 4-aligned — the
    /// CRC-32 of the entire sidecar file.
    /// </summary>
    /// <param name="bytes">The raw image bytes.</param>
    /// <param name="fileName">The named sidecar file.</param>
    /// <param name="crc">The expected CRC-32 of the sidecar's bytes.</param>
    internal static bool TryReadDebugLink(ReadOnlySpan<byte> bytes, out string fileName, out uint crc)
    {
        fileName = "";
        crc = 0;
        if (!TryGetSection(bytes, ".gnu_debuglink", out var section)) return false;
        if (section.FileOffset < 0 || section.FileOffset + section.Size > bytes.Length) return false;

        var data = bytes.Slice(section.FileOffset, section.Size);
        var end = data.IndexOf((byte)0);
        if (end <= 0) return false;

        var crcOffset = (end + 1 + 3) & ~3;
        if (crcOffset + 4 > data.Length) return false;

        fileName = Encoding.UTF8.GetString(data[..end]);
        crc = BinaryPrimitives.ReadUInt32LittleEndian(data[crcOffset..]);
        return true;
    }

    private static string? ReadString(ReadOnlySpan<byte> bytes, long tableOffset, uint offset)
    {
        var start = tableOffset + offset;
        if (start < 0 || start >= bytes.Length) return null;

        var slice = bytes[(int)start..];
        var end = slice.IndexOf((byte)0);
        if (end < 0) end = Math.Min(slice.Length, MaxStringLength);
        if (end > MaxStringLength) return null;

        return Encoding.UTF8.GetString(slice[..end]);
    }

    /// <summary>
    /// Reads a NUL-terminated string wholly contained in an ELF string table.
    /// </summary>
    /// <param name="bytes">The complete ELF image.</param>
    /// <param name="tableOffset">The string table's file offset.</param>
    /// <param name="tableSize">The string table's byte size.</param>
    /// <param name="offset">The string's table-relative offset.</param>
    /// <returns>The decoded string, or null when its range or terminator is invalid.</returns>
    internal static string? ReadBoundedString(
        ReadOnlySpan<byte> bytes,
        int tableOffset,
        int tableSize,
        uint offset)
    {
        if (!TryGetFileRange(tableOffset, tableSize, bytes.Length)
            || offset >= (uint)tableSize)
        {
            return null;
        }

        var relativeOffset = (int)offset;
        var available = tableSize - relativeOffset;
        var slice = bytes.Slice(
            tableOffset + relativeOffset,
            Math.Min(available, MaxStringLength + 1));
        var end = slice.IndexOf((byte)0);
        if (end < 0 || end > MaxStringLength)
            return null;

        return Encoding.UTF8.GetString(slice[..end]);
    }

    /// <summary>
    /// Determines whether a non-negative file range is wholly contained in an image.
    /// </summary>
    /// <param name="offset">The range's file offset.</param>
    /// <param name="size">The range's byte size.</param>
    /// <param name="imageLength">The complete image length.</param>
    /// <returns>True when the range is safe to slice.</returns>
    internal static bool TryGetFileRange(int offset, int size, int imageLength) =>
        offset >= 0
        && size >= 0
        && offset <= imageLength
        && size <= imageLength - offset;
}
