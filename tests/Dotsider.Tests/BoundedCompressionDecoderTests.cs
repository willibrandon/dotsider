using Dotsider.Core.Analysis;
using System.IO.Compression;

namespace Dotsider.Tests;

/// <summary>
/// Verifies exact-length, allocation-bounded raw-deflate and zlib decoding.
/// </summary>
[TestClass]
public sealed class BoundedCompressionDecoderTests
{
    /// <summary>
    /// Exact output at the configured boundary is accepted byte-for-byte.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TryDecodeDeflate_ExactBoundary_ReturnsBytes()
    {
        byte[] expected = [.. Enumerable.Range(0, 64).Select(static value => (byte)value)];
        byte[] compressed = CompressDeflate(expected);

        bool success = BoundedCompressionDecoder.TryDecodeDeflate(
            compressed,
            offset: 0,
            compressed.Length,
            expected.Length,
            compressed.Length,
            expected.Length,
            out byte[] actual);

        Assert.IsTrue(success);
        Assert.AreSequenceEqual(expected, actual);
    }

    /// <summary>
    /// Output shorter or longer than the declared size is rejected.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(63)]
    [DataRow(65)]
    public void TryDecodeDeflate_OutputLengthMismatch_ReturnsFalse(int declaredLength)
    {
        byte[] content = [.. Enumerable.Range(0, 64).Select(static value => (byte)value)];
        byte[] compressed = CompressDeflate(content);

        bool success = BoundedCompressionDecoder.TryDecodeDeflate(
            compressed,
            offset: 0,
            compressed.Length,
            declaredLength,
            compressed.Length,
            maximumDecompressedLength: 128,
            out byte[] decoded);

        Assert.IsFalse(success);
        Assert.IsEmpty(decoded);
    }

    /// <summary>
    /// Invalid or truncated deflate payloads fail closed.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TryDecodeDeflate_InvalidOrTruncatedPayload_ReturnsFalse()
    {
        byte[] content = [.. Enumerable.Range(0, 256).Select(static value => (byte)value)];
        byte[] compressed = CompressDeflate(content);
        byte[] truncated = compressed[..^1];

        Assert.IsFalse(BoundedCompressionDecoder.TryDecodeDeflate(
            new byte[] { 0xFF, 0xFF, 0xFF },
            offset: 0,
            compressedLength: 3,
            expectedLength: content.Length,
            maximumCompressedLength: 3,
            maximumDecompressedLength: content.Length,
            out byte[] invalid));
        Assert.IsEmpty(invalid);

        Assert.IsFalse(BoundedCompressionDecoder.TryDecodeDeflate(
            truncated,
            offset: 0,
            truncated.Length,
            content.Length,
            truncated.Length,
            content.Length,
            out byte[] shortOutput));
        Assert.IsEmpty(shortOutput);
    }

    /// <summary>
    /// File-controlled compressed and decompressed lengths are checked before decoding.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TryDecodeDeflate_LengthOutsideLimits_ReturnsFalse()
    {
        byte[] compressed = CompressDeflate([0x2A]);

        foreach (int declaredLength in new[] { -1, 0, 65, int.MaxValue })
        {
            Assert.IsFalse(BoundedCompressionDecoder.TryDecodeDeflate(
                compressed,
                offset: 0,
                compressed.Length,
                declaredLength,
                compressed.Length,
                maximumDecompressedLength: 64,
                out byte[] decoded));
            Assert.IsEmpty(decoded);
        }

        Assert.IsFalse(BoundedCompressionDecoder.TryDecodeDeflate(
            compressed,
            offset: 0,
            compressed.Length,
            expectedLength: 1,
            maximumCompressedLength: compressed.Length - 1,
            maximumDecompressedLength: 64,
            out byte[] oversizedInput));
        Assert.IsEmpty(oversizedInput);
    }

    /// <summary>
    /// A zlib stream is decoded without copying its array-backed source and must match the
    /// declared output length exactly.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TryDecodeZLib_ExactBoundary_ReturnsBytes()
    {
        byte[] expected = [.. Enumerable.Range(0, 256).Select(static value => (byte)(value * 7))];
        byte[] compressed = CompressZLib(expected);

        bool success = BoundedCompressionDecoder.TryDecodeZLib(
            compressed,
            offset: 0,
            compressed.Length,
            expected.Length,
            compressed.Length,
            expected.Length,
            out byte[] actual);

        Assert.IsTrue(success);
        Assert.AreSequenceEqual(expected, actual);
    }

    /// <summary>
    /// Zlib output shorter or longer than its declared size is rejected.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(255)]
    [DataRow(257)]
    public void TryDecodeZLib_OutputLengthMismatch_ReturnsFalse(int declaredLength)
    {
        byte[] content = [.. Enumerable.Range(0, 256).Select(static value => (byte)value)];
        byte[] compressed = CompressZLib(content);

        bool success = BoundedCompressionDecoder.TryDecodeZLib(
            compressed,
            offset: 0,
            compressed.Length,
            declaredLength,
            compressed.Length,
            maximumDecompressedLength: 512,
            out byte[] decoded);

        Assert.IsFalse(success);
        Assert.IsEmpty(decoded);
    }

    /// <summary>
    /// Invalid and truncated zlib streams fail closed without returning partial output.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TryDecodeZLib_InvalidOrTruncatedPayload_ReturnsFalse()
    {
        byte[] content = [.. Enumerable.Range(0, 256).Select(static value => (byte)value)];
        byte[] compressed = CompressZLib(content);
        byte[] truncated = compressed[..^1];

        Assert.IsFalse(BoundedCompressionDecoder.TryDecodeZLib(
            new byte[] { 0xFF, 0xFF, 0xFF },
            offset: 0,
            compressedLength: 3,
            expectedLength: content.Length,
            maximumCompressedLength: 3,
            maximumDecompressedLength: content.Length,
            out byte[] invalid));
        Assert.IsEmpty(invalid);

        Assert.IsFalse(BoundedCompressionDecoder.TryDecodeZLib(
            truncated,
            offset: 0,
            truncated.Length,
            content.Length,
            truncated.Length,
            content.Length,
            out byte[] shortOutput));
        Assert.IsEmpty(shortOutput);
    }

    private static byte[] CompressDeflate(byte[] content)
    {
        using MemoryStream output = new();
        using (DeflateStream deflate = new(output, CompressionLevel.SmallestSize, leaveOpen: true))
            deflate.Write(content);
        return output.ToArray();
    }

    private static byte[] CompressZLib(byte[] content)
    {
        using MemoryStream output = new();
        using (ZLibStream zlib = new(output, CompressionLevel.SmallestSize, leaveOpen: true))
            zlib.Write(content);
        return output.ToArray();
    }
}
