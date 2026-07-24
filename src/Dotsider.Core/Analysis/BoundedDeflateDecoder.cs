using System.Buffers;
using System.IO.Compression;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Decodes an exact amount of deflate output without trusting the declared length for allocation.
/// </summary>
internal static class BoundedDeflateDecoder
{
    private const int BufferSize = 64 * 1024;

    internal static bool TryDecode(
        byte[] source,
        int offset,
        int compressedLength,
        int expectedLength,
        int maximumCompressedLength,
        int maximumDecompressedLength,
        out byte[] decoded)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(compressedLength);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCompressedLength);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDecompressedLength);
        if (offset > source.Length || compressedLength > source.Length - offset)
            throw new ArgumentOutOfRangeException(nameof(compressedLength));

        decoded = [];
        if (compressedLength > maximumCompressedLength
            || expectedLength <= 0
            || expectedLength > maximumDecompressedLength)
        {
            return false;
        }

        using MemoryStream input = new(
            source,
            offset,
            compressedLength,
            writable: false,
            publiclyVisible: false);
        using DeflateStream deflate = new(input, CompressionMode.Decompress);
        using MemoryStream output = new(Math.Min(expectedLength, BufferSize));
        byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);

        try
        {
            var total = 0;
            while (true)
            {
                int remainingWithSentinel = expectedLength - total + 1;
                int bytesRead = deflate.Read(
                    buffer,
                    0,
                    Math.Min(buffer.Length, remainingWithSentinel));
                if (bytesRead == 0)
                    break;
                if (bytesRead > expectedLength - total)
                    return false;

                output.Write(buffer, 0, bytesRead);
                total += bytesRead;
            }

            if (total != expectedLength)
                return false;

            if (output.TryGetBuffer(out ArraySegment<byte> segment)
                && segment.Offset == 0
                && segment.Count == expectedLength
                && segment.Array?.Length == expectedLength)
            {
                decoded = segment.Array;
            }
            else
            {
                decoded = output.ToArray();
            }

            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
