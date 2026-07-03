using Dotsider.Core.Analysis.NativePdb;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="MsfFile"/>, the MSF 7.0 container reader, using synthetic images so the
/// block and directory math is exercised on every platform.
/// </summary>
public class MsfFileTests
{
    /// <summary>
    /// Verifies a stream's bytes round-trip through the block directory, including a stream whose
    /// content spans more than one block.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void GetStream_MultiBlockStream_RoundTrips()
    {
        var payload = new byte[1500];
        for (var i = 0; i < payload.Length; i++) payload[i] = (byte)(i * 7);
        var image = SyntheticImageBuilders.BuildMsf(512, payload);

        var msf = MsfFile.TryOpen(image);

        Assert.NotNull(msf);
        Assert.Equal(payload, msf.GetStream(1));
    }

    /// <summary>
    /// Verifies a multi-block stream directory (more directory bytes than one block holds) is
    /// concatenated and read correctly.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void TryOpen_MultiBlockDirectory_ResolvesAllStreams()
    {
        // Many small streams push the directory past one 512-byte block.
        var streams = new byte[]?[60];
        for (var i = 0; i < streams.Length; i++) streams[i] = [(byte)i, (byte)(i + 1)];
        var image = SyntheticImageBuilders.BuildMsf(512, streams);

        var msf = MsfFile.TryOpen(image);

        Assert.NotNull(msf);
        Assert.Equal(61, msf.StreamCount); // stream 0 + 60
        Assert.Equal([59, 60], msf.GetStream(60));
    }

    /// <summary>
    /// Verifies a nil stream (size 0xFFFFFFFF) reports zero length and yields no bytes.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void GetStream_NilStream_IsEmpty()
    {
        var image = SyntheticImageBuilders.BuildMsf(512, [1, 2, 3], null);

        var msf = MsfFile.TryOpen(image);

        Assert.NotNull(msf);
        Assert.Equal(0, msf.StreamSize(2));
        Assert.Empty(msf.GetStream(2));
    }

    /// <summary>
    /// Verifies non-MSF bytes return null rather than throwing.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void TryOpen_NotMsf_ReturnsNull()
    {
        Assert.Null(MsfFile.TryOpen([0xDE, 0xAD, 0xBE, 0xEF]));
        Assert.Null(MsfFile.TryOpen(new byte[512]));
    }

    /// <summary>
    /// Verifies an out-of-range block index in the directory is rejected as null.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void TryOpen_CorruptBlockIndex_ReturnsNull()
    {
        var image = SyntheticImageBuilders.BuildMsf(512, [1, 2, 3, 4]);
        // Corrupt the block-map block's first entry to point past the file. The block map sits in
        // the last block; its first 4 bytes are the directory's block index.
        var blockMapOffset = image.Length - 512;
        image[blockMapOffset] = 0xFF;
        image[blockMapOffset + 1] = 0xFF;

        Assert.Null(MsfFile.TryOpen(image));
    }
}
