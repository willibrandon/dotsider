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
}
