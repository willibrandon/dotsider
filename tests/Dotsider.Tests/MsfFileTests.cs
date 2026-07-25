using Dotsider.Core.Analysis.NativePdb;
using System.Buffers.Binary;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="MsfFile"/>, the MSF 7.0 container reader, using synthetic images so the
/// block and directory math is exercised on every platform.
/// </summary>
[TestClass]
public sealed class MsfFileTests
{
    /// <summary>
    /// Verifies a stream's bytes round-trip through the block directory, including a stream whose
    /// content spans more than one block.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void GetStream_MultiBlockStream_RoundTrips()
    {
        var payload = new byte[1500];
        for (var i = 0; i < payload.Length; i++) payload[i] = (byte)(i * 7);
        var image = SyntheticImageBuilders.BuildMsf(512, payload);

        var msf = MsfFile.TryOpen(image);

        Assert.IsNotNull(msf);
        Assert.AreSequenceEqual(payload, msf.GetStream(1));
    }

    /// <summary>
    /// Verifies a multi-block stream directory (more directory bytes than one block holds) is
    /// concatenated and read correctly.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TryOpen_MultiBlockDirectory_ResolvesAllStreams()
    {
        // Many small streams push the directory past one 512-byte block.
        var streams = new byte[]?[60];
        for (var i = 0; i < streams.Length; i++) streams[i] = [(byte)i, (byte)(i + 1)];
        var image = SyntheticImageBuilders.BuildMsf(512, streams);

        var msf = MsfFile.TryOpen(image);

        Assert.IsNotNull(msf);
        Assert.AreEqual(61, msf.StreamCount); // stream 0 + 60
        Assert.AreSequenceEqual(";<"u8.ToArray(), msf.GetStream(60));
    }

    /// <summary>
    /// Verifies a nil stream (size 0xFFFFFFFF) reports zero length and yields no bytes.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void GetStream_NilStream_IsEmpty()
    {
        var image = SyntheticImageBuilders.BuildMsf(512, [1, 2, 3], null);

        var msf = MsfFile.TryOpen(image);

        Assert.IsNotNull(msf);
        Assert.AreEqual(0, msf.StreamSize(2));
        Assert.IsEmpty(msf.GetStream(2));
    }

    /// <summary>
    /// Verifies non-MSF bytes return null rather than throwing.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TryOpen_NotMsf_ReturnsNull()
    {
        Assert.IsNull(MsfFile.TryOpen([0xDE, 0xAD, 0xBE, 0xEF]));
        Assert.IsNull(MsfFile.TryOpen(new byte[512]));
    }

    /// <summary>
    /// Verifies an out-of-range block index in the directory is rejected as null.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TryOpen_CorruptBlockIndex_ReturnsNull()
    {
        var image = SyntheticImageBuilders.BuildMsf(512, [1, 2, 3, 4]);
        // Corrupt the block-map block's first entry to point past the file. The block map sits in
        // the last block; its first 4 bytes are the directory's block index.
        var blockMapOffset = image.Length - 512;
        image[blockMapOffset] = 0xFF;
        image[blockMapOffset + 1] = 0xFF;

        Assert.IsNull(MsfFile.TryOpen(image));
    }

    /// <summary>Verifies a stream block index cannot escape the declared MSF container.</summary>
    [TestMethod]
    public void TryOpen_StreamBlockOutsideContainer_ReturnsNull()
    {
        var image = SyntheticImageBuilders.BuildMsf(512, [1, 2, 3, 4]);
        var directoryOffset = DirectoryOffset(image);
        BinaryPrimitives.WriteUInt32LittleEndian(
            image.AsSpan(directoryOffset + 3 * sizeof(uint)),
            uint.MaxValue);

        Assert.IsNull(MsfFile.TryOpen(image));
    }

    /// <summary>Verifies required superblock fields reject their invalid zero values.</summary>
    /// <param name="fieldOffset">The superblock field to clear.</param>
    [TestMethod]
    [DataRow(40)]
    [DataRow(44)]
    [DataRow(52)]
    public void TryOpen_RequiredSuperblockFieldIsZero_ReturnsNull(int fieldOffset)
    {
        var image = SyntheticImageBuilders.BuildMsf(512, [1, 2, 3, 4]);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(fieldOffset), 0);

        Assert.IsNull(MsfFile.TryOpen(image));
    }

    /// <summary>Verifies unsupported MSF block sizes are rejected before block arithmetic.</summary>
    [TestMethod]
    public void TryOpen_UnsupportedBlockSize_ReturnsNull()
    {
        var image = SyntheticImageBuilders.BuildMsf(512, [1, 2, 3, 4]);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(32), 256);

        Assert.IsNull(MsfFile.TryOpen(image));
    }

    /// <summary>Verifies directory block-count rounding cannot overflow signed arithmetic.</summary>
    [TestMethod]
    public void TryOpen_DirectoryByteCountRoundingOverflow_ReturnsNull()
    {
        var image = SyntheticImageBuilders.BuildMsf(512, [1, 2, 3, 4]);
        BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(44), int.MaxValue);

        Assert.IsNull(MsfFile.TryOpen(image));
    }

    /// <summary>Verifies the directory block list must fit in its single block-map block.</summary>
    [TestMethod]
    public void TryOpen_DirectoryMapCapacityExceeded_ReturnsNull()
    {
        const int blockSize = 512;
        var image = SyntheticImageBuilders.BuildMsf(blockSize, [1, 2, 3, 4]);
        var mapCapacity = blockSize / sizeof(int);
        var declaredBlockCount = mapCapacity + 2;
        Array.Resize(ref image, declaredBlockCount * blockSize);
        BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(40), declaredBlockCount);
        BinaryPrimitives.WriteInt32LittleEndian(
            image.AsSpan(44),
            checked((mapCapacity + 1) * blockSize));

        Assert.IsNull(MsfFile.TryOpen(image));
    }

    /// <summary>Verifies a directory may use every entry in its one-block block map.</summary>
    [TestMethod]
    public void TryOpen_DirectoryAtMapCapacity_IsAccepted()
    {
        const int blockSize = 512;
        var directoryByteCapacity = blockSize * (blockSize / sizeof(uint));
        var totalStreamCount = (directoryByteCapacity - sizeof(uint)) / sizeof(uint);
        var streamCount = totalStreamCount - 1;
        var image = SyntheticImageBuilders.BuildMsf(
            blockSize,
            new byte[]?[streamCount]);

        var msf = MsfFile.TryOpen(image);

        Assert.IsNotNull(msf);
        Assert.AreEqual(streamCount + 1, msf.StreamCount);
    }

    /// <summary>Verifies the declared block count cannot describe bytes absent from the file.</summary>
    [TestMethod]
    public void TryOpen_DeclaredBlockCountBeyondFile_ReturnsNull()
    {
        const int blockSize = 512;
        var image = SyntheticImageBuilders.BuildMsf(blockSize, [1, 2, 3, 4]);
        BinaryPrimitives.WriteInt32LittleEndian(
            image.AsSpan(40),
            image.Length / blockSize + 1);

        Assert.IsNull(MsfFile.TryOpen(image));
    }

    /// <summary>Verifies the stream-size table must fit in the declared directory bytes.</summary>
    [TestMethod]
    public void TryOpen_StreamSizeTableBeyondDeclaredDirectory_ReturnsNull()
    {
        var image = SyntheticImageBuilders.BuildMsf(512, [1, 2, 3, 4]);
        BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(44), sizeof(int));

        Assert.IsNull(MsfFile.TryOpen(image));
    }

    /// <summary>Verifies stream block lists must fit in the declared directory bytes.</summary>
    [TestMethod]
    public void TryOpen_StreamBlockTableBeyondDeclaredDirectory_ReturnsNull()
    {
        var image = SyntheticImageBuilders.BuildMsf(512, [1, 2, 3, 4]);
        BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(44), 3 * sizeof(int));

        Assert.IsNull(MsfFile.TryOpen(image));
    }

    /// <summary>Verifies stream block-count rounding cannot overflow signed arithmetic.</summary>
    [TestMethod]
    public void TryOpen_StreamBlockCountRoundingOverflow_ReturnsNull()
    {
        var image = SyntheticImageBuilders.BuildMsf(512, [1, 2, 3, 4]);
        var directoryOffset = DirectoryOffset(image);
        BinaryPrimitives.WriteInt32LittleEndian(
            image.AsSpan(directoryOffset + 2 * sizeof(int)),
            int.MaxValue);

        Assert.IsNull(MsfFile.TryOpen(image));
    }

    private static int DirectoryOffset(byte[] image)
    {
        var blockSize = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(32));
        var blockMap = BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(52));
        var directoryBlock = BinaryPrimitives.ReadInt32LittleEndian(
            image.AsSpan(blockMap * blockSize));
        return directoryBlock * blockSize;
    }
}
