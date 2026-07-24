using Dotsider.Core.Analysis;
using System.IO.Compression;

namespace Dotsider.Tests;

/// <summary>
/// Verifies exact-length, allocation-bounded deflate decoding.
/// </summary>
[TestClass]
public sealed class BoundedDeflateDecoderTests
{
    /// <summary>
    /// Exact output at the configured boundary is accepted byte-for-byte.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TryDecode_ExactBoundary_ReturnsBytes()
    {
        byte[] expected = [.. Enumerable.Range(0, 64).Select(static value => (byte)value)];
        byte[] compressed = Compress(expected);

        bool success = BoundedDeflateDecoder.TryDecode(
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
    public void TryDecode_OutputLengthMismatch_ReturnsFalse(int declaredLength)
    {
        byte[] content = [.. Enumerable.Range(0, 64).Select(static value => (byte)value)];
        byte[] compressed = Compress(content);

        bool success = BoundedDeflateDecoder.TryDecode(
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
    public void TryDecode_InvalidOrTruncatedPayload_ReturnsFalse()
    {
        byte[] content = [.. Enumerable.Range(0, 256).Select(static value => (byte)value)];
        byte[] compressed = Compress(content);
        byte[] truncated = compressed[..^1];

        Assert.IsFalse(BoundedDeflateDecoder.TryDecode(
            [0xFF, 0xFF, 0xFF],
            offset: 0,
            compressedLength: 3,
            expectedLength: content.Length,
            maximumCompressedLength: 3,
            maximumDecompressedLength: content.Length,
            out byte[] invalid));
        Assert.IsEmpty(invalid);

        Assert.IsFalse(BoundedDeflateDecoder.TryDecode(
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
    public void TryDecode_LengthOutsideLimits_ReturnsFalse()
    {
        byte[] compressed = Compress([0x2A]);

        foreach (int declaredLength in new[] { -1, 0, 65, int.MaxValue })
        {
            Assert.IsFalse(BoundedDeflateDecoder.TryDecode(
                compressed,
                offset: 0,
                compressed.Length,
                declaredLength,
                compressed.Length,
                maximumDecompressedLength: 64,
                out byte[] decoded));
            Assert.IsEmpty(decoded);
        }

        Assert.IsFalse(BoundedDeflateDecoder.TryDecode(
            compressed,
            offset: 0,
            compressed.Length,
            expectedLength: 1,
            maximumCompressedLength: compressed.Length - 1,
            maximumDecompressedLength: 64,
            out byte[] oversizedInput));
        Assert.IsEmpty(oversizedInput);
    }

    private static byte[] Compress(byte[] content)
    {
        using MemoryStream output = new();
        using (DeflateStream deflate = new(output, CompressionLevel.SmallestSize, leaveOpen: true))
            deflate.Write(content);
        return output.ToArray();
    }
}
