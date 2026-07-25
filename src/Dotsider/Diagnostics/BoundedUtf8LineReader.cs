using System.Buffers;
using System.Text;

namespace Dotsider.Diagnostics;

internal static class BoundedUtf8LineReader
{
    private const int FramingAllowance = 4;
    private const int ReadBufferSize = 8_192;

    private static readonly UTF8Encoding s_strictUtf8 =
        new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    internal static async ValueTask<BoundedUtf8LineReadResult> ReadAsync(
        Stream stream,
        int maxPayloadBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPayloadBytes);

        var maxBufferedBytes = checked(maxPayloadBytes + FramingAllowance);
        var payload = ArrayPool<byte>.Shared.Rent(
            Math.Min(ReadBufferSize, maxBufferedBytes));
        var readBuffer = ArrayPool<byte>.Shared.Rent(ReadBufferSize);

        try
        {
            var payloadLength = 0;
            var tooLarge = false;

            while (true)
            {
                var bytesRead = await stream.ReadAsync(
                    readBuffer.AsMemory(0, ReadBufferSize),
                    cancellationToken);
                if (bytesRead == 0)
                {
                    if (payloadLength == 0 && !tooLarge)
                    {
                        return new(BoundedUtf8LineReadStatus.EndOfStream);
                    }

                    return Decode(payload, payloadLength, maxPayloadBytes, tooLarge);
                }

                for (var index = 0; index < bytesRead; index++)
                {
                    var value = readBuffer[index];
                    if (value == (byte)'\n')
                    {
                        return Decode(payload, payloadLength, maxPayloadBytes, tooLarge);
                    }

                    if (tooLarge)
                    {
                        continue;
                    }

                    if (payloadLength == maxBufferedBytes)
                    {
                        tooLarge = true;
                        continue;
                    }

                    if (payloadLength == payload.Length)
                    {
                        var expandedLength = Math.Min(
                            maxBufferedBytes,
                            checked(payloadLength * 2));
                        var expanded = ArrayPool<byte>.Shared.Rent(expandedLength);
                        payload.AsSpan(0, payloadLength).CopyTo(expanded);
                        ArrayPool<byte>.Shared.Return(payload);
                        payload = expanded;
                    }

                    payload[payloadLength++] = value;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(payload);
            ArrayPool<byte>.Shared.Return(readBuffer);
        }
    }

    private static BoundedUtf8LineReadResult Decode(
        byte[] payload,
        int payloadLength,
        int maxPayloadBytes,
        bool tooLarge)
    {
        if (tooLarge)
        {
            return new(BoundedUtf8LineReadStatus.TooLarge);
        }

        var offset = HasUtf8Bom(payload, payloadLength) ? 3 : 0;
        if (payloadLength > offset && payload[payloadLength - 1] == (byte)'\r')
        {
            payloadLength--;
        }

        var contentLength = payloadLength - offset;
        if (contentLength > maxPayloadBytes)
        {
            return new(BoundedUtf8LineReadStatus.TooLarge);
        }

        try
        {
            return new(
                BoundedUtf8LineReadStatus.Success,
                s_strictUtf8.GetString(payload, offset, contentLength));
        }
        catch (DecoderFallbackException)
        {
            return new(BoundedUtf8LineReadStatus.InvalidUtf8);
        }
    }

    private static bool HasUtf8Bom(byte[] payload, int payloadLength)
    {
        return payloadLength >= 3
            && payload[0] == 0xEF
            && payload[1] == 0xBB
            && payload[2] == 0xBF;
    }
}
