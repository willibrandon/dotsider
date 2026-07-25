using System.Buffers.Binary;

namespace Dotsider.Core.Analysis.NativePdb;

/// <summary>
/// Validates the fixed MSF 7.0 superblock and exposes its file-backed block layout.
/// </summary>
internal readonly struct MsfSuperBlock
{
    private const int HeaderSize = 56;
    private static readonly byte[] Magic =
        [.. "Microsoft C/C++ MSF 7.00\r\n"u8, 0x1A, .. "DS"u8, 0, 0, 0];

    private readonly long _fileLength;

    private MsfSuperBlock(
        int blockSize,
        uint blockCount,
        int directoryByteCount,
        int directoryBlockCount,
        uint blockMapAddress,
        long fileLength)
    {
        BlockSize = blockSize;
        BlockCount = blockCount;
        DirectoryByteCount = directoryByteCount;
        DirectoryBlockCount = directoryBlockCount;
        BlockMapAddress = blockMapAddress;
        _fileLength = fileLength;
    }

    /// <summary>Gets the byte size of one MSF block.</summary>
    internal int BlockSize { get; }

    /// <summary>Gets the number of blocks declared by the MSF file.</summary>
    internal uint BlockCount { get; }

    /// <summary>Gets the logical byte length of the stream directory.</summary>
    internal int DirectoryByteCount { get; }

    /// <summary>Gets the number of blocks occupied by the stream directory.</summary>
    internal int DirectoryBlockCount { get; }

    /// <summary>Gets the block containing the stream-directory block map.</summary>
    internal uint BlockMapAddress { get; }

    /// <summary>
    /// Parses and validates an MSF 7.0 superblock against the containing file length.
    /// </summary>
    /// <param name="header">The first 56 bytes of the MSF file.</param>
    /// <param name="fileLength">The complete file length.</param>
    /// <param name="superBlock">The validated superblock.</param>
    /// <returns><see langword="true"/> when every structural field is valid and file-backed.</returns>
    internal static bool TryRead(
        ReadOnlySpan<byte> header,
        long fileLength,
        out MsfSuperBlock superBlock)
    {
        superBlock = default;
        if (header.Length < HeaderSize
            || fileLength < HeaderSize
            || !header[..Magic.Length].SequenceEqual(Magic))
        {
            return false;
        }

        var blockSizeValue = BinaryPrimitives.ReadUInt32LittleEndian(header[32..]);
        if (blockSizeValue is not (512 or 1024 or 2048 or 4096 or 8192))
        {
            return false;
        }

        var blockSize = (int)blockSizeValue;
        var blockCount = BinaryPrimitives.ReadUInt32LittleEndian(header[40..]);
        var directoryByteCountValue = BinaryPrimitives.ReadUInt32LittleEndian(header[44..]);
        var blockMapAddress = BinaryPrimitives.ReadUInt32LittleEndian(header[52..]);
        if (blockCount == 0
            || directoryByteCountValue == 0
            || blockMapAddress == 0
            || blockMapAddress >= blockCount)
        {
            return false;
        }

        var declaredFileLength = (ulong)blockCount * blockSizeValue;
        if (declaredFileLength > (ulong)fileLength
            || directoryByteCountValue > declaredFileLength
            || !NativeImageRange.TryAlignUp(
                directoryByteCountValue,
                blockSizeValue,
                out var paddedDirectoryByteCount))
        {
            return false;
        }

        var directoryBlockCountValue = paddedDirectoryByteCount / blockSizeValue;
        if (directoryBlockCountValue == 0
            || directoryBlockCountValue > blockSizeValue / sizeof(uint)
            || directoryByteCountValue > int.MaxValue
            || directoryBlockCountValue > int.MaxValue)
        {
            return false;
        }

        var candidate = new MsfSuperBlock(
            blockSize,
            blockCount,
            (int)directoryByteCountValue,
            (int)directoryBlockCountValue,
            blockMapAddress,
            fileLength);
        if (!candidate.TryGetBlockOffset(blockMapAddress, out _))
        {
            return false;
        }

        superBlock = candidate;
        return true;
    }

    /// <summary>
    /// Resolves an MSF block index to a contained file offset.
    /// </summary>
    /// <param name="blockIndex">The declared block index.</param>
    /// <param name="fileOffset">The validated file offset.</param>
    /// <returns><see langword="true"/> when the complete block is present in the file.</returns>
    internal bool TryGetBlockOffset(uint blockIndex, out long fileOffset)
    {
        if (blockIndex >= BlockCount)
        {
            fileOffset = 0;
            return false;
        }

        var offset = (ulong)blockIndex * (uint)BlockSize;
        if (offset > (ulong)_fileLength
            || (ulong)BlockSize > (ulong)_fileLength - offset
            || offset > long.MaxValue)
        {
            fileOffset = 0;
            return false;
        }

        fileOffset = (long)offset;
        return true;
    }

    /// <summary>
    /// Calculates the number of MSF blocks required by a stream size.
    /// </summary>
    /// <param name="streamSize">The logical stream size.</param>
    /// <param name="streamBlockCount">The validated number of blocks.</param>
    /// <returns>
    /// <see langword="true"/> when the size is representable and cannot require more blocks than
    /// the containing file declares.
    /// </returns>
    internal bool TryGetStreamBlockCount(uint streamSize, out int streamBlockCount)
    {
        var count = streamSize == 0
            ? 0
            : ((ulong)streamSize - 1) / (uint)BlockSize + 1;
        if (count > BlockCount || count > int.MaxValue)
        {
            streamBlockCount = 0;
            return false;
        }

        streamBlockCount = (int)count;
        return true;
    }
}
