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
    // "Microsoft C/C++ MSF 7.00\r\n" + 0x1A + "DS" + three NULs. Built from explicit bytes so
    // the 0x1A control byte cannot be misread as part of a variable-length \x escape.
    private static readonly byte[] Magic =
        [.. "Microsoft C/C++ MSF 7.00\r\n"u8, 0x1A, .. "DS"u8, 0, 0, 0];

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
        try
        {
            var span = bytes.AsSpan();
            if (span.Length < 56 || !span[..Magic.Length].SequenceEqual(Magic)) return null;

            var blockSize = BinaryPrimitives.ReadInt32LittleEndian(span[32..]);
            if (blockSize is not (512 or 1024 or 2048 or 4096 or 8192)) return null;

            var numBlocks = BinaryPrimitives.ReadInt32LittleEndian(span[40..]);
            var numDirectoryBytes = BinaryPrimitives.ReadInt32LittleEndian(span[44..]);
            var blockMapAddr = BinaryPrimitives.ReadInt32LittleEndian(span[52..]);
            if (numBlocks <= 0 || (long)numBlocks * blockSize > bytes.Length) return null;
            if (numDirectoryBytes <= 0 || blockMapAddr <= 0 || blockMapAddr >= numBlocks) return null;

            bool InBounds(int block) => block >= 0 && block < numBlocks;
            Span<byte> Block(int i) => bytes.AsSpan(i * blockSize, blockSize);
            int BlockCount(int size) => (size + blockSize - 1) / blockSize;

            // The block map lists the directory's own blocks; concatenate them into the directory.
            var directoryBlockCount = BlockCount(numDirectoryBytes);
            var mapBlock = Block(blockMapAddr);
            if (directoryBlockCount * 4 > blockSize) return null; // map must fit one block
            var directory = new byte[directoryBlockCount * blockSize];
            for (var i = 0; i < directoryBlockCount; i++)
            {
                var block = BinaryPrimitives.ReadInt32LittleEndian(mapBlock[(i * 4)..]);
                if (!InBounds(block)) return null;
                Block(block).CopyTo(directory.AsSpan(i * blockSize));
            }

            // Directory: numStreams, each stream's size, then each stream's block list. Read from
            // the full block-padded buffer so a final entry landing on the numDirectoryBytes
            // boundary has slack; the entry counts bound the loops within the real content.
            var dir = directory.AsSpan();
            var p = 0;
            var numStreams = ReadI32(dir, ref p);
            if (numStreams is < 0 or > 1_000_000) return null;

            var sizes = new int[numStreams];
            for (var i = 0; i < numStreams; i++)
            {
                var size = ReadI32(dir, ref p);
                sizes[i] = size == unchecked((int)0xFFFFFFFF) ? 0 : size; // 0xFFFFFFFF = nil stream
                if (sizes[i] < 0) return null;
            }

            var streamBlocks = new int[numStreams][];
            for (var i = 0; i < numStreams; i++)
            {
                var count = BlockCount(sizes[i]);
                var blocks = new int[count];
                for (var j = 0; j < count; j++)
                {
                    var block = ReadI32(dir, ref p);
                    if (!InBounds(block)) return null;
                    blocks[j] = block;
                }

                streamBlocks[i] = blocks;
            }

            return new MsfFile(bytes, blockSize, sizes, streamBlocks);
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            return null;
        }
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

    private static int ReadI32(ReadOnlySpan<byte> span, ref int p)
    {
        var value = BinaryPrimitives.ReadInt32LittleEndian(span[p..]);
        p += 4;
        return value;
    }
}
