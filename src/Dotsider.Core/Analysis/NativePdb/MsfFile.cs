using System.Buffers.Binary;

namespace Dotsider.Core.Analysis.NativePdb;

/// <summary>
/// Reads the Multi-Stream Format (MSF 7.0) container that a Windows PDB is stored in. An MSF
/// file is a block-addressed collection of streams: a superblock points at a block map, the
/// block map lists the blocks of the stream directory, and the directory lists the size and
/// blocks of every stream. This class resolves that indirection so callers can pull a stream's
/// bytes by index. Malformed containers surface as null from <see cref="TryOpen"/> rather than
/// throwing.
/// </summary>
internal sealed class MsfFile
{
    private readonly byte[] _bytes;
    private readonly int _blockSize;
    private readonly int[][] _streamBlocks;
    private readonly int[] _streamSizes;

    private MsfFile(byte[] bytes, int blockSize, int[] streamSizes, int[][] streamBlocks)
    {
        _bytes = bytes;
        _blockSize = blockSize;
        _streamSizes = streamSizes;
        _streamBlocks = streamBlocks;
    }

    /// <summary>The number of streams in the container.</summary>
    public int StreamCount => _streamSizes.Length;


    /// <summary>
    /// Opens an MSF container from its raw bytes, or returns null when the bytes are not a
    /// valid MSF 7.0 file or the directory cannot be resolved.
    /// </summary>
    /// <param name="bytes">The complete file content.</param>
    public static MsfFile? TryOpen(byte[] bytes)
    {
        var span = bytes.AsSpan();
        if (!MsfSuperBlock.TryRead(span, span.Length, out var superBlock))
        {
            return null;
        }

        if (!superBlock.TryGetBlockOffset(superBlock.BlockMapAddress, out var mapOffsetValue))
        {
            return null;
        }

        var mapOffset = (int)mapOffsetValue;
        var mapBlock = span.Slice(mapOffset, superBlock.BlockSize);
        var directory = new byte[superBlock.DirectoryByteCount];
        for (var i = 0; i < superBlock.DirectoryBlockCount; i++)
        {
            var block = BinaryPrimitives.ReadUInt32LittleEndian(mapBlock[(i * sizeof(uint))..]);
            if (!superBlock.TryGetBlockOffset(block, out var blockOffsetValue))
            {
                return null;
            }

            var destinationOffset = i * superBlock.BlockSize;
            var byteCount = Math.Min(
                superBlock.BlockSize,
                superBlock.DirectoryByteCount - destinationOffset);
            span.Slice((int)blockOffsetValue, byteCount)
                .CopyTo(directory.AsSpan(destinationOffset));
        }

        var dir = directory.AsSpan();
        var p = 0;
        if (!TryReadUInt32(dir, ref p, out var streamCountValue)
            || streamCountValue > int.MaxValue
            || !NativeImageRange.TryGetTable(
                dir.Length,
                (ulong)p,
                streamCountValue,
                sizeof(uint),
                sizeof(uint),
                out _,
                out _))
        {
            return null;
        }

        var streamCount = (int)streamCountValue;
        var sizes = new int[streamCount];
        var blockCounts = new int[streamCount];
        ulong totalStreamBlocks = 0;
        for (var i = 0; i < streamCount; i++)
        {
            _ = TryReadUInt32(dir, ref p, out var size);
            if (size == uint.MaxValue)
            {
                continue;
            }

            if (size > int.MaxValue
                || !superBlock.TryGetStreamBlockCount(size, out var blockCount)
                || !NativeImageRange.TryAdd(
                    totalStreamBlocks,
                    (ulong)blockCount,
                    out totalStreamBlocks))
            {
                return null;
            }

            sizes[i] = (int)size;
            blockCounts[i] = blockCount;
        }

        if (!NativeImageRange.TryGetTable(
            dir.Length,
            (ulong)p,
            totalStreamBlocks,
            sizeof(uint),
            sizeof(uint),
            out _,
            out _))
        {
            return null;
        }

        var streamBlocks = new int[streamCount][];
        for (var i = 0; i < streamCount; i++)
        {
            var count = blockCounts[i];
            if (count == 0)
            {
                streamBlocks[i] = [];
                continue;
            }

            var blocks = new int[count];
            for (var j = 0; j < count; j++)
            {
                _ = TryReadUInt32(dir, ref p, out var block);
                if (!superBlock.TryGetBlockOffset(block, out var blockOffset))
                {
                    return null;
                }

                blocks[j] = checked((int)(blockOffset / superBlock.BlockSize));
            }

            streamBlocks[i] = blocks;
        }

        return new MsfFile(bytes, superBlock.BlockSize, sizes, streamBlocks);
    }

    /// <summary>The byte size of the stream at <paramref name="index"/>, or 0 when out of range.</summary>
    public int StreamSize(int index) =>
        index >= 0 && index < _streamSizes.Length ? _streamSizes[index] : 0;

    /// <summary>
    /// Materializes the bytes of the stream at <paramref name="index"/> by concatenating its
    /// blocks, or an empty array when the index is out of range or nil.
    /// </summary>
    /// <param name="index">The stream index.</param>
    public byte[] GetStream(int index)
    {
        if (index < 0 || index >= _streamSizes.Length) return [];
        var size = _streamSizes[index];
        if (size == 0) return [];

        var buffer = new byte[size];
        var offset = 0;
        foreach (var block in _streamBlocks[index])
        {
            var count = Math.Min(_blockSize, size - offset);
            _bytes.AsSpan(block * _blockSize, count).CopyTo(buffer.AsSpan(offset));
            offset += count;
        }

        return buffer;
    }

    private static bool TryReadUInt32(ReadOnlySpan<byte> span, ref int offset, out uint value)
    {
        if (offset < 0 || offset > span.Length - sizeof(uint))
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadUInt32LittleEndian(span[offset..]);
        offset += sizeof(uint);
        return true;
    }
}
