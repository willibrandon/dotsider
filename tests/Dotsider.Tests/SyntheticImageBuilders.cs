using System.Buffers.Binary;

namespace Dotsider.Tests;

/// <summary>
/// Builds minimal synthetic binary images in memory so the native-symbol readers can be
/// exercised on every platform, not only the CI leg whose real format matches. Each builder
/// produces the smallest well-formed structure that drives the code path under test.
/// </summary>
internal static class SyntheticImageBuilders
{
    private static readonly byte[] MsfMagic =
        [.. "Microsoft C/C++ MSF 7.00\r\n"u8, 0x1A, .. "DS"u8, 0, 0, 0];

    /// <summary>
    /// Builds a minimal 64-bit PE image (one section) with an optional exception directory, so the
    /// <c>.pdata</c> reader can be exercised for x64 and ARM64 on any platform. The section holds
    /// <paramref name="sectionData"/> at RVA 0x1000 mapped to a matching file offset; the exception
    /// directory points at <paramref name="exceptionRva"/> for <paramref name="exceptionSize"/> bytes.
    /// </summary>
    /// <param name="machine">The COFF machine (0x8664 for x64, 0xAA64 for ARM64).</param>
    /// <param name="sectionData">The bytes of the single section, placed at RVA 0x1000.</param>
    /// <param name="exceptionRva">The exception directory RVA (within the section), or 0 for none.</param>
    /// <param name="exceptionSize">The exception directory size in bytes.</param>
    /// <param name="imageBase">The image base.</param>
    public static byte[] BuildPe(
        ushort machine, byte[] sectionData, uint exceptionRva, uint exceptionSize, ulong imageBase = 0x140000000)
    {
        const int sectionRva = 0x1000;
        const int sectionFileOffset = 0x400;
        var peHeaderOffset = 0x80;

        var image = new byte[sectionFileOffset + Math.Max(0x200, sectionData.Length + 0x200)];
        image[0] = (byte)'M';
        image[1] = (byte)'Z';
        BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(0x3C), peHeaderOffset);

        var p = peHeaderOffset;
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(p), 0x0000_4550); // "PE\0\0"
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(p + 4), machine);
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(p + 6), 1); // NumberOfSections
        const int optionalSize = 240; // PE32+ optional header with 16 data directories
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(p + 20), optionalSize);

        var optional = p + 24;
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(optional), 0x20B); // PE32+
        BinaryPrimitives.WriteUInt64LittleEndian(image.AsSpan(optional + 24), imageBase);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(optional + 108), 16); // NumberOfRvaAndSizes
        // Data directory index 3 = Exception.
        var directories = optional + 112;
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(directories + 3 * 8), exceptionRva);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(directories + 3 * 8 + 4), exceptionSize);

        // One section covering the data, with VA 0x1000 mapped to file offset 0x400.
        var sectionTable = optional + optionalSize;
        System.Text.Encoding.ASCII.GetBytes(".text").CopyTo(image.AsSpan(sectionTable));
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(sectionTable + 8), (uint)Math.Max(sectionData.Length, 0x200)); // VirtualSize
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(sectionTable + 12), sectionRva);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(sectionTable + 16), (uint)Math.Max(sectionData.Length, 0x200)); // SizeOfRawData
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(sectionTable + 20), sectionFileOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(sectionTable + 36), 0x6000_0020); // code | execute | read

        sectionData.CopyTo(image.AsSpan(sectionFileOffset));
        return image;
    }

    /// <summary>Packs an x64 RUNTIME_FUNCTION (begin, end, unwind-info RVAs).</summary>
    public static byte[] Amd64RuntimeFunction(uint beginRva, uint endRva, uint unwindInfoRva)
    {
        var b = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(b, beginRva);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(4), endRva);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(8), unwindInfoRva);
        return b;
    }

    /// <summary>
    /// Builds a minimal MSF 7.0 container with a multi-block stream directory and the given
    /// stream contents. Stream 0 is empty; the supplied streams follow as streams 1..n. A nil
    /// stream is represented by a null entry in <paramref name="streams"/>.
    /// </summary>
    /// <param name="blockSize">The MSF block size (must be a valid MSF block size).</param>
    /// <param name="streams">The content of streams 1..n; a null entry produces a nil stream.</param>
    /// <returns>The complete MSF file bytes.</returns>
    public static byte[] BuildMsf(int blockSize, params byte[]?[] streams)
    {
        var allStreams = new List<byte[]?> { Array.Empty<byte>() }; // stream 0 is always present and empty
        allStreams.AddRange(streams);

        int BlockCount(int size) => (size + blockSize - 1) / blockSize;

        // Lay out data blocks for each stream starting after a reserved header region:
        // block 0 = superblock, block 1 = FPM, blocks 2.. = stream data, then directory, then map.
        var nextBlock = 2;
        var streamBlockLists = new List<int[]>();
        foreach (var s in allStreams)
        {
            if (s is null) { streamBlockLists.Add([]); continue; }
            var count = BlockCount(s.Length);
            var list = new int[count];
            for (var i = 0; i < count; i++) list[i] = nextBlock++;
            streamBlockLists.Add(list);
        }

        // Directory bytes: numStreams, sizes, then per-stream block lists.
        var dir = new List<byte>();
        void PutI32(List<byte> b, int v) { Span<byte> t = stackalloc byte[4]; BinaryPrimitives.WriteInt32LittleEndian(t, v); b.AddRange(t); }
        PutI32(dir, allStreams.Count);
        for (var i = 0; i < allStreams.Count; i++)
            PutI32(dir, allStreams[i] is null ? unchecked((int)0xFFFFFFFF) : allStreams[i]!.Length);
        for (var i = 0; i < allStreams.Count; i++)
            foreach (var block in streamBlockLists[i]) PutI32(dir, block);

        var directoryBytes = dir.ToArray();
        var directoryBlockCount = BlockCount(directoryBytes.Length);
        var directoryFirstBlock = nextBlock;
        nextBlock += directoryBlockCount;
        var blockMapBlock = nextBlock++;

        var totalBlocks = nextBlock;
        var image = new byte[totalBlocks * blockSize];

        // Superblock.
        MsfMagic.CopyTo(image, 0);
        BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(32), blockSize);
        BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(36), 1); // free block map
        BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(40), totalBlocks);
        BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(44), directoryBytes.Length);
        BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(52), blockMapBlock);

        // Stream data.
        for (var i = 0; i < allStreams.Count; i++)
        {
            var s = allStreams[i];
            if (s is null) continue;
            for (var j = 0; j < streamBlockLists[i].Length; j++)
            {
                var off = j * blockSize;
                var n = Math.Min(blockSize, s.Length - off);
                s.AsSpan(off, n).CopyTo(image.AsSpan(streamBlockLists[i][j] * blockSize));
            }
        }

        // Directory blocks.
        for (var i = 0; i < directoryBlockCount; i++)
        {
            var off = i * blockSize;
            var n = Math.Min(blockSize, directoryBytes.Length - off);
            directoryBytes.AsSpan(off, n).CopyTo(image.AsSpan((directoryFirstBlock + i) * blockSize));
        }

        // Block map: the list of directory block indices.
        for (var i = 0; i < directoryBlockCount; i++)
            BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(blockMapBlock * blockSize + i * 4), directoryFirstBlock + i);

        return image;
    }

    /// <summary>
    /// Builds a minimal 64-bit little-endian ELF image whose section header table carries the
    /// given named sections (plus the standard null section and a trailing <c>.shstrtab</c>),
    /// so section-driven readers — DWARF, symtab, build-id — run on every platform.
    /// </summary>
    /// <param name="sections">Each section's name, virtual address, and content bytes.</param>
    public static byte[] BuildElf(params (string Name, ulong Address, byte[] Content)[] sections)
    {
        const int headerSize = 64;
        const int sectionHeaderSize = 64;

        // Section-name string table: NUL, then each name, then ".shstrtab".
        var names = new List<byte> { 0 };
        var nameOffsets = new uint[sections.Length + 1];
        for (var i = 0; i < sections.Length; i++)
        {
            nameOffsets[i] = (uint)names.Count;
            names.AddRange(System.Text.Encoding.UTF8.GetBytes(sections[i].Name));
            names.Add(0);
        }

        nameOffsets[^1] = (uint)names.Count;
        names.AddRange(".shstrtab"u8.ToArray());
        names.Add(0);
        var shStrTab = names.ToArray();

        // Layout: header | section contents | .shstrtab | section header table.
        var contentOffsets = new int[sections.Length];
        var offset = headerSize;
        for (var i = 0; i < sections.Length; i++)
        {
            contentOffsets[i] = offset;
            offset += sections[i].Content.Length;
        }

        var shStrTabOffset = offset;
        offset += shStrTab.Length;
        var tableOffset = offset;
        var sectionCount = sections.Length + 2; // null section + user sections + .shstrtab
        var image = new byte[tableOffset + sectionCount * sectionHeaderSize];

        image[0] = 0x7F;
        image[1] = (byte)'E';
        image[2] = (byte)'L';
        image[3] = (byte)'F';
        image[4] = 2; // ELFCLASS64
        image[5] = 1; // little-endian
        image[6] = 1; // EV_CURRENT
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(16), 2); // ET_EXEC
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(18), 0x3E); // EM_X86_64
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(20), 1);
        BinaryPrimitives.WriteUInt64LittleEndian(image.AsSpan(40), (ulong)tableOffset); // e_shoff
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(52), headerSize); // e_ehsize
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(58), sectionHeaderSize); // e_shentsize
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(60), (ushort)sectionCount);
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(62), (ushort)(sectionCount - 1)); // e_shstrndx

        void WriteHeader(int index, uint nameOffset, uint type, ulong address, long fileOffset, long size)
        {
            var h = tableOffset + index * sectionHeaderSize;
            BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(h), nameOffset);
            BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(h + 4), type);
            BinaryPrimitives.WriteUInt64LittleEndian(image.AsSpan(h + 16), address);
            BinaryPrimitives.WriteUInt64LittleEndian(image.AsSpan(h + 24), (ulong)fileOffset);
            BinaryPrimitives.WriteUInt64LittleEndian(image.AsSpan(h + 32), (ulong)size);
        }

        for (var i = 0; i < sections.Length; i++)
        {
            sections[i].Content.CopyTo(image.AsSpan(contentOffsets[i]));
            WriteHeader(i + 1, nameOffsets[i], 1 /* SHT_PROGBITS */, sections[i].Address,
                contentOffsets[i], sections[i].Content.Length);
        }

        shStrTab.CopyTo(image.AsSpan(shStrTabOffset));
        WriteHeader(sectionCount - 1, nameOffsets[^1], 3 /* SHT_STRTAB */, 0, shStrTabOffset, shStrTab.Length);
        return image;
    }

    /// <summary>
    /// Builds a GNU build-id note (owner <c>GNU</c>, type 3) for a <c>.note.gnu.build-id</c>
    /// section, optionally preceded by an unrelated note the reader must walk past.
    /// </summary>
    /// <param name="id">The build id payload.</param>
    /// <param name="precedeWithForeignNote">Whether to prepend a non-GNU note entry.</param>
    public static byte[] GnuBuildIdNote(byte[] id, bool precedeWithForeignNote = false)
    {
        var note = new List<byte>();
        void U32(uint v) { Span<byte> t = stackalloc byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(t, v); note.AddRange(t); }
        void Padded(byte[] data)
        {
            note.AddRange(data);
            for (var i = data.Length; (i & 3) != 0; i++) note.Add(0);
        }

        if (precedeWithForeignNote)
        {
            U32(4); U32(2); U32(1); // namesz, descsz, type
            Padded("XYZ\0"u8.ToArray());
            Padded([0xAA, 0xBB]);
        }

        U32(4); U32((uint)id.Length); U32(3);
        Padded("GNU\0"u8.ToArray());
        Padded(id);
        return [.. note];
    }

    /// <summary>
    /// Builds <c>.gnu_debuglink</c> content: the sidecar file name, 4-aligned, then the CRC-32
    /// of the sidecar's bytes.
    /// </summary>
    /// <param name="fileName">The sidecar file name.</param>
    /// <param name="crc">The CRC-32 of the entire sidecar file.</param>
    public static byte[] GnuDebugLink(string fileName, uint crc)
    {
        var content = new List<byte>();
        content.AddRange(System.Text.Encoding.UTF8.GetBytes(fileName));
        content.Add(0);
        while ((content.Count & 3) != 0) content.Add(0);
        Span<byte> t = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(t, crc);
        content.AddRange(t);
        return [.. content];
    }
}
