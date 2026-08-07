using System.Buffers.Binary;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Decodes an exact amount of compressed output within caller-provided allocation limits.
/// </summary>
internal static class BoundedCompressionDecoder
{
    /// <summary>
    /// Decodes a raw deflate stream within the supplied compressed and decompressed byte limits.
    /// </summary>
    /// <param name="source">The memory containing the compressed stream.</param>
    /// <param name="offset">The compressed stream's offset within <paramref name="source"/>.</param>
    /// <param name="compressedLength">The compressed stream's byte length.</param>
    /// <param name="expectedLength">The exact decompressed byte length.</param>
    /// <param name="maximumCompressedLength">The maximum accepted compressed byte length.</param>
    /// <param name="maximumDecompressedLength">The maximum accepted decompressed byte length.</param>
    /// <param name="decoded">The exact decompressed bytes on success; otherwise an empty array.</param>
    /// <returns><see langword="true"/> when the stream is valid and has the expected length.</returns>
    internal static bool TryDecodeDeflate(
        ReadOnlyMemory<byte> source,
        int offset,
        int compressedLength,
        int expectedLength,
        int maximumCompressedLength,
        int maximumDecompressedLength,
        out byte[] decoded) =>
        TryDecode(
            source,
            offset,
            compressedLength,
            expectedLength,
            maximumCompressedLength,
            maximumDecompressedLength,
            useZLibWrapper: false,
            out decoded);

    /// <summary>
    /// Decodes a zlib-wrapped deflate stream within the supplied compressed and decompressed byte limits.
    /// </summary>
    /// <param name="source">The memory containing the compressed stream.</param>
    /// <param name="offset">The compressed stream's offset within <paramref name="source"/>.</param>
    /// <param name="compressedLength">The compressed stream's byte length.</param>
    /// <param name="expectedLength">The exact decompressed byte length.</param>
    /// <param name="maximumCompressedLength">The maximum accepted compressed byte length.</param>
    /// <param name="maximumDecompressedLength">The maximum accepted decompressed byte length.</param>
    /// <param name="decoded">The exact decompressed bytes on success; otherwise an empty array.</param>
    /// <returns><see langword="true"/> when the stream is valid and has the expected length.</returns>
    internal static bool TryDecodeZLib(
        ReadOnlyMemory<byte> source,
        int offset,
        int compressedLength,
        int expectedLength,
        int maximumCompressedLength,
        int maximumDecompressedLength,
        out byte[] decoded) =>
        TryDecode(
            source,
            offset,
            compressedLength,
            expectedLength,
            maximumCompressedLength,
            maximumDecompressedLength,
            useZLibWrapper: true,
            out decoded);

    private static bool TryDecode(
        ReadOnlyMemory<byte> source,
        int offset,
        int compressedLength,
        int expectedLength,
        int maximumCompressedLength,
        int maximumDecompressedLength,
        bool useZLibWrapper,
        out byte[] decoded)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(compressedLength);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCompressedLength);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDecompressedLength);
        if (offset > source.Length || compressedLength > source.Length - offset)
            throw new ArgumentOutOfRangeException(nameof(compressedLength));

        decoded = [];
        if (compressedLength > maximumCompressedLength
            || expectedLength <= 0
            || expectedLength > maximumDecompressedLength
            || (useZLibWrapper && compressedLength < 6))
        {
            return false;
        }

        MemoryStream input;
        if (MemoryMarshal.TryGetArray(source, out ArraySegment<byte> sourceSegment)
            && sourceSegment.Array is not null)
        {
            input = new MemoryStream(
                sourceSegment.Array,
                sourceSegment.Offset + offset,
                compressedLength,
                writable: false,
                publiclyVisible: false);
        }
        else
        {
            byte[] copiedSource = source.Slice(offset, compressedLength).ToArray();
            input = new MemoryStream(copiedSource, writable: false);
        }

        using MemoryStream inputStream = input;
        using Stream decompressor = useZLibWrapper
            ? new ZLibStream(inputStream, CompressionMode.Decompress)
            : new DeflateStream(inputStream, CompressionMode.Decompress);
        byte[] output = GC.AllocateUninitializedArray<byte>(expectedLength);

        try
        {
            var total = 0;
            uint adlerA = 1;
            uint adlerB = 0;
            while (total < output.Length)
            {
                int bytesRead = decompressor.Read(output, total, output.Length - total);
                if (bytesRead == 0)
                    return false;

                if (useZLibWrapper)
                    UpdateAdler32(output.AsSpan(total, bytesRead), ref adlerA, ref adlerB);
                total += bytesRead;
            }

            if (decompressor.ReadByte() != -1)
                return false;

            if (useZLibWrapper)
            {
                uint expectedAdler = BinaryPrimitives.ReadUInt32BigEndian(
                    source.Span[(offset + compressedLength - sizeof(uint))..]);
                uint actualAdler = adlerB << 16 | adlerA;
                if (actualAdler != expectedAdler)
                    return false;
            }

            decoded = output;
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static void UpdateAdler32(ReadOnlySpan<byte> bytes, ref uint a, ref uint b)
    {
        const uint modulus = 65_521;
        const int maximumChunkLength = 5_552;

        while (!bytes.IsEmpty)
        {
            int chunkLength = Math.Min(bytes.Length, maximumChunkLength);
            foreach (byte value in bytes[..chunkLength])
            {
                a += value;
                b += a;
            }

            a %= modulus;
            b %= modulus;
            bytes = bytes[chunkLength..];
        }
    }
}
