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
