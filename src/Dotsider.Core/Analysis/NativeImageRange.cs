namespace Dotsider.Core.Analysis;

/// <summary>
/// Validates file ranges from untrusted native-image fields before they are narrowed to the
/// signed offsets accepted by span and memory APIs.
/// </summary>
internal static class NativeImageRange
{
    /// <summary>
    /// Validates a file range without adding its attacker-controlled offset and length.
    /// </summary>
    /// <param name="imageLength">The complete image length.</param>
    /// <param name="offset">The unsigned file offset from the image.</param>
    /// <param name="length">The unsigned byte length of the range.</param>
    /// <param name="fileOffset">The validated signed file offset.</param>
    /// <param name="byteLength">The validated signed byte length.</param>
    /// <returns>True when the complete range is representable and contained in the image.</returns>
    internal static bool TryGet(
        int imageLength,
        ulong offset,
        ulong length,
        out int fileOffset,
        out int byteLength)
    {
        if (imageLength < 0
            || offset > (ulong)imageLength
            || length > (ulong)imageLength - offset)
        {
            fileOffset = 0;
            byteLength = 0;
            return false;
        }

        fileOffset = (int)offset;
        byteLength = (int)length;
        return true;
    }

    /// <summary>
    /// Validates a signed file range without adding its offset and length.
    /// </summary>
    /// <param name="imageLength">The complete image length.</param>
    /// <param name="offset">The file offset from the image.</param>
    /// <param name="length">The byte length of the range.</param>
    /// <param name="fileOffset">The validated file offset.</param>
    /// <param name="byteLength">The validated byte length.</param>
    /// <returns>True when the values are non-negative and the complete range is contained.</returns>
    internal static bool TryGet(
        int imageLength,
        int offset,
        int length,
        out int fileOffset,
        out int byteLength)
    {
        if (offset < 0 || length < 0)
        {
            fileOffset = 0;
            byteLength = 0;
            return false;
        }

        return TryGet(imageLength, (ulong)offset, (ulong)length, out fileOffset, out byteLength);
    }

    /// <summary>
    /// Validates a fixed-stride table before any row is read or a count-sized collection is
    /// allocated.
    /// </summary>
    /// <param name="imageLength">The complete image length.</param>
    /// <param name="offset">The table's unsigned file offset.</param>
    /// <param name="count">The number of declared rows.</param>
    /// <param name="stride">The declared byte stride between rows.</param>
    /// <param name="minimumStride">The minimum number of bytes read from each row.</param>
    /// <param name="fileOffset">The validated signed table offset.</param>
    /// <param name="byteLength">The validated signed table length.</param>
    /// <returns>True when the stride is sufficient and the complete table is contained.</returns>
    internal static bool TryGetTable(
        int imageLength,
        ulong offset,
        ulong count,
        ulong stride,
        ulong minimumStride,
        out int fileOffset,
        out int byteLength)
    {
        if (stride < minimumStride || (count != 0 && stride > ulong.MaxValue / count))
        {
            fileOffset = 0;
            byteLength = 0;
            return false;
        }

        return TryGet(imageLength, offset, count * stride, out fileOffset, out byteLength);
    }

    /// <summary>
    /// Adds two unsigned native-image values only when the sum is representable.
    /// </summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <param name="sum">The sum when it is representable.</param>
    /// <returns>True when the addition does not overflow.</returns>
    internal static bool TryAdd(ulong left, ulong right, out ulong sum)
    {
        if (right > ulong.MaxValue - left)
        {
            sum = 0;
            return false;
        }

        sum = left + right;
        return true;
    }

    /// <summary>
    /// Rounds an unsigned native-image value up to a power-of-two alignment without overflow.
    /// </summary>
    /// <param name="value">The value to align.</param>
    /// <param name="alignment">The non-zero power-of-two alignment.</param>
    /// <param name="aligned">The aligned value when it is representable.</param>
    /// <returns>True when the alignment is valid and the result does not overflow.</returns>
    internal static bool TryAlignUp(ulong value, ulong alignment, out ulong aligned)
    {
        if (alignment == 0
            || (alignment & (alignment - 1)) != 0
            || !TryAdd(value, alignment - 1, out var rounded))
        {
            aligned = 0;
            return false;
        }

        aligned = rounded & ~(alignment - 1);
        return true;
    }
}
