namespace Dotsider.Tests;

/// <summary>
/// A little-endian byte composer for hand-building DWARF section blobs in tests: fixed-width
/// integers, LEB128 variable-length integers, and NUL-terminated strings, with fluent chaining
/// so a DIE reads as one expression.
/// </summary>
internal sealed class DwarfBlob
{
    private readonly List<byte> _bytes = [];

    /// <summary>The byte count written so far.</summary>
    public int Length => _bytes.Count;

    /// <summary>Writes one byte.</summary>
    public DwarfBlob U8(byte value)
    {
        _bytes.Add(value);
        return this;
    }

    /// <summary>Writes a little-endian u16.</summary>
    public DwarfBlob U16(ushort value)
    {
        _bytes.Add((byte)value);
        _bytes.Add((byte)(value >> 8));
        return this;
    }

    /// <summary>Writes a little-endian u32.</summary>
    public DwarfBlob U32(uint value)
    {
        for (var i = 0; i < 4; i++) _bytes.Add((byte)(value >> (8 * i)));
        return this;
    }

    /// <summary>Writes a little-endian u64.</summary>
    public DwarfBlob U64(ulong value)
    {
        for (var i = 0; i < 8; i++) _bytes.Add((byte)(value >> (8 * i)));
        return this;
    }

    /// <summary>Writes an unsigned LEB128 integer.</summary>
    public DwarfBlob ULeb(ulong value)
    {
        do
        {
            var b = (byte)(value & 0x7F);
            value >>= 7;
            if (value != 0) b |= 0x80;
            _bytes.Add(b);
        }
        while (value != 0);
        return this;
    }

    /// <summary>Writes a signed LEB128 integer.</summary>
    public DwarfBlob SLeb(long value)
    {
        while (true)
        {
            var b = (byte)(value & 0x7F);
            value >>= 7;
            var done = (value == 0 && (b & 0x40) == 0) || (value == -1 && (b & 0x40) != 0);
            if (!done) b |= 0x80;
            _bytes.Add(b);
            if (done) return this;
        }
    }

    /// <summary>Writes a NUL-terminated UTF-8 string.</summary>
    public DwarfBlob CStr(string value)
    {
        _bytes.AddRange(System.Text.Encoding.UTF8.GetBytes(value));
        _bytes.Add(0);
        return this;
    }

    /// <summary>Appends raw bytes.</summary>
    public DwarfBlob Bytes(byte[] data)
    {
        _bytes.AddRange(data);
        return this;
    }

    /// <summary>Materializes the blob.</summary>
    public byte[] ToArray() => [.. _bytes];
}
