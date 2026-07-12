using System.Buffers.Binary;

namespace Dotsider.Core.Analysis.ReadyToRun;

/// <summary>
/// A byte reader over a ReadyToRun image with the compact variable-length integer codec the
/// NativeFormat structures use (<c>NativeReader</c> in the runtime's R2R reader). Offsets are file
/// offsets into the whole image; NativeFormat deltas are position-relative, so anchoring a
/// structure at its section's file offset keeps every internal offset correct. Reads past the end
/// throw <see cref="BadImageFormatException"/>, which callers translate into a diagnostic status.
/// </summary>
internal sealed class R2RNativeReader
{
    private readonly int _endOffset;
    private readonly ReadOnlyMemory<byte> _image;
    private readonly int _startOffset;

    /// <summary>Creates a reader over the complete <paramref name="image"/>.</summary>
    public R2RNativeReader(ReadOnlyMemory<byte> image)
        : this(image, 0, image.Length)
    {
    }

    private R2RNativeReader(ReadOnlyMemory<byte> image, int startOffset, int endOffset)
    {
        _endOffset = endOffset;
        _image = image;
        _startOffset = startOffset;
    }

    /// <summary>The total length of the backing image.</summary>
    public int Length => _image.Length;

    /// <summary>
    /// Creates a reader whose absolute offsets are restricted to the requested subrange of this
    /// reader. The returned reader continues to use image-relative offsets.
    /// </summary>
    /// <param name="offset">The first readable absolute image offset.</param>
    /// <param name="length">The number of readable bytes.</param>
    public R2RNativeReader Slice(int offset, int length)
    {
        if (length < 0 || offset < _startOffset || offset > _endOffset - length)
        {
            throw new BadImageFormatException("ReadyToRun data range lies outside its containing section.");
        }

        return new R2RNativeReader(_image, offset, offset + length);
    }

    /// <summary>Reads a byte at <paramref name="offset"/> and advances it by one.</summary>
    public byte ReadByte(ref int offset)
    {
        Require(offset, 1);
        return _image.Span[offset++];
    }

    /// <summary>Reads a byte at <paramref name="offset"/> without advancing.</summary>
    public byte PeekByte(int offset)
    {
        Require(offset, 1);
        return _image.Span[offset];
    }

    /// <summary>Reads a little-endian ushort at <paramref name="offset"/> and advances it by two.</summary>
    public ushort ReadUInt16(ref int offset)
    {
        Require(offset, 2);
        var value = BinaryPrimitives.ReadUInt16LittleEndian(_image.Span[offset..]);
        offset += 2;
        return value;
    }

    /// <summary>Reads a little-endian uint at <paramref name="offset"/> and advances it by four.</summary>
    public uint ReadUInt32(ref int offset)
    {
        Require(offset, 4);
        var value = BinaryPrimitives.ReadUInt32LittleEndian(_image.Span[offset..]);
        offset += 4;
        return value;
    }

    /// <summary>Reads a little-endian int at <paramref name="offset"/> and advances it by four.</summary>
    public int ReadInt32(ref int offset)
    {
        Require(offset, 4);
        var value = BinaryPrimitives.ReadInt32LittleEndian(_image.Span[offset..]);
        offset += 4;
        return value;
    }

    /// <summary>
    /// Decodes an unsigned NativeFormat integer at <paramref name="offset"/>, returning the offset
    /// immediately after it. The low bits of the first byte give the encoded length (1–5 bytes).
    /// </summary>
    public int DecodeUnsigned(int offset, out uint value)
    {
        var span = _image.Span;
        Require(offset, 1);
        uint val = span[offset];

        if ((val & 1) == 0)
        {
            value = val >> 1;
            return offset + 1;
        }

        if ((val & 2) == 0)
        {
            Require(offset, 2);
            value = (val >> 2) | ((uint)span[offset + 1] << 6);
            return offset + 2;
        }

        if ((val & 4) == 0)
        {
            Require(offset, 3);
            value = (val >> 3) | ((uint)span[offset + 1] << 5) | ((uint)span[offset + 2] << 13);
            return offset + 3;
        }

        if ((val & 8) == 0)
        {
            Require(offset, 4);
            value = (val >> 4) | ((uint)span[offset + 1] << 4) | ((uint)span[offset + 2] << 12)
                | ((uint)span[offset + 3] << 20);
            return offset + 4;
        }

        if ((val & 16) == 0)
        {
            Require(offset, 5);
            value = BinaryPrimitives.ReadUInt32LittleEndian(span[(offset + 1)..]);
            return offset + 5;
        }

        throw new BadImageFormatException("Invalid ReadyToRun unsigned var-int encoding.");
    }

    /// <summary>
    /// Decodes a signed NativeFormat integer at <paramref name="offset"/>, returning the offset
    /// immediately after it. Same length prefix as <see cref="DecodeUnsigned"/>, sign-extended.
    /// </summary>
    public int DecodeSigned(int offset, out int value)
    {
        var span = _image.Span;
        Require(offset, 1);
        int val = span[offset];

        if ((val & 1) == 0)
        {
            value = val >> 1;
            return offset + 1;
        }

        if ((val & 2) == 0)
        {
            Require(offset, 2);
            value = (val >> 2) | (span[offset + 1] << 6);
            return offset + 2;
        }

        if ((val & 4) == 0)
        {
            Require(offset, 3);
            value = (val >> 3) | (span[offset + 1] << 5) | (span[offset + 2] << 13);
            return offset + 3;
        }

        if ((val & 8) == 0)
        {
            Require(offset, 4);
            value = (val >> 4) | (span[offset + 1] << 4) | (span[offset + 2] << 12)
                | (span[offset + 3] << 20);
            return offset + 4;
        }

        if ((val & 16) == 0)
        {
            Require(offset, 5);
            value = BinaryPrimitives.ReadInt32LittleEndian(span[(offset + 1)..]);
            return offset + 5;
        }

        throw new BadImageFormatException("Invalid ReadyToRun signed var-int encoding.");
    }

    /// <summary>
    /// Decodes the compact unsigned integer codec used by ReadyToRun GC and unwind payloads.
    /// Each byte contributes seven value bits, with bit 7 indicating continuation.
    /// The method advances <paramref name="offset"/> past the encoded integer.
    /// </summary>
    /// <param name="offset">The current image offset, advanced past the encoded value.</param>
    public uint DecodeUnsignedGc(ref int offset)
    {
        var data = ReadByte(ref offset);
        uint value = (uint)data & 0x7F;
        while ((data & 0x80) != 0)
        {
            data = ReadByte(ref offset);
            value <<= 7;
            value += (uint)data & 0x7F;
        }

        return value;
    }

    /// <summary>
    /// Reads an ECMA-335 compressed unsigned integer at <paramref name="offset"/> (the codec R2R
    /// signatures use), advancing it. The high bits of the first byte select a 1-, 2-, or 4-byte form.
    /// </summary>
    public uint ReadCompressedUInt(ref int offset)
    {
        var b0 = ReadByte(ref offset);
        if ((b0 & 0x80) == 0)
            return b0;
        if ((b0 & 0xC0) == 0x80)
            return ((uint)(b0 & 0x3F) << 8) | ReadByte(ref offset);
        return ((uint)(b0 & 0x1F) << 24)
            | ((uint)ReadByte(ref offset) << 16)
            | ((uint)ReadByte(ref offset) << 8)
            | ReadByte(ref offset);
    }

    /// <summary>Reads an ECMA-335 compressed signed integer at <paramref name="offset"/>, advancing it.</summary>
    public int ReadCompressedInt(ref int offset)
    {
        var raw = ReadCompressedUInt(ref offset);
        var value = (int)(raw >> 1);
        return (raw & 1) == 0 ? value : -value;
    }

    private void Require(int offset, int count)
    {
        if (count < 0 || offset < _startOffset || offset > _endOffset - count)
        {
            throw new BadImageFormatException("ReadyToRun read outside its containing data range.");
        }
    }
}
