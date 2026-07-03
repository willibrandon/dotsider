using System.Buffers.Binary;
using System.Text;

namespace Dotsider.Core.Analysis.Dwarf;

/// <summary>
/// A forward cursor over a DWARF section with the primitive decoders the format is built from:
/// fixed-width little-endian integers, LEB128 variable-length integers, NUL-terminated strings,
/// and the initial-length field whose <c>0xFFFFFFFF</c> escape switches a unit to DWARF64
/// (64-bit lengths and section offsets). Out-of-bounds reads throw
/// <see cref="ArgumentOutOfRangeException"/>, which the readers catch to end a walk leniently.
/// </summary>
internal ref struct DwarfDataReader(ReadOnlySpan<byte> data)
{
    private readonly ReadOnlySpan<byte> _data = data;

    /// <summary>The cursor position, in bytes from the start of the section.</summary>
    public int Position { get; set; }

    /// <summary>The bytes remaining ahead of the cursor.</summary>
    public readonly int Remaining => _data.Length - Position;

    /// <summary>Reads one byte.</summary>
    public byte ReadU8() => _data[Position++];

    /// <summary>Reads a little-endian u16.</summary>
    public ushort ReadU16()
    {
        var v = BinaryPrimitives.ReadUInt16LittleEndian(_data[Position..]);
        Position += 2;
        return v;
    }

    /// <summary>Reads a little-endian u32.</summary>
    public uint ReadU32()
    {
        var v = BinaryPrimitives.ReadUInt32LittleEndian(_data[Position..]);
        Position += 4;
        return v;
    }

    /// <summary>Reads a little-endian u64.</summary>
    public ulong ReadU64()
    {
        var v = BinaryPrimitives.ReadUInt64LittleEndian(_data[Position..]);
        Position += 8;
        return v;
    }

    /// <summary>Reads an unsigned LEB128 integer.</summary>
    public ulong ReadULeb128()
    {
        ulong result = 0;
        var shift = 0;
        while (true)
        {
            var b = _data[Position++];
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) return result;
            shift += 7;
            if (shift > 63) throw new ArgumentOutOfRangeException(nameof(shift), "LEB128 too long");
        }
    }

    /// <summary>Reads a signed LEB128 integer.</summary>
    public long ReadSLeb128()
    {
        long result = 0;
        var shift = 0;
        byte b;
        do
        {
            b = _data[Position++];
            result |= (long)(b & 0x7F) << shift;
            shift += 7;
            if (shift > 70) throw new ArgumentOutOfRangeException(nameof(shift), "LEB128 too long");
        }
        while ((b & 0x80) != 0);

        if (shift < 64 && (b & 0x40) != 0)
            result |= -1L << shift;
        return result;
    }

    /// <summary>
    /// Reads a unit's initial length: a u32, or the <c>0xFFFFFFFF</c> escape followed by a u64
    /// for DWARF64.
    /// </summary>
    /// <param name="is64">Set when the unit is DWARF64.</param>
    public ulong ReadInitialLength(out bool is64)
    {
        var first = ReadU32();
        if (first == 0xFFFF_FFFF)
        {
            is64 = true;
            return ReadU64();
        }

        is64 = false;
        return first;
    }

    /// <summary>Reads a section offset: u32 in DWARF32, u64 in DWARF64.</summary>
    /// <param name="is64">Whether the containing unit is DWARF64.</param>
    public ulong ReadSectionOffset(bool is64) => is64 ? ReadU64() : ReadU32();

    /// <summary>Reads an address of the unit's address size (4 or 8 bytes).</summary>
    /// <param name="addressSize">The unit's address size.</param>
    public ulong ReadAddress(int addressSize) => addressSize == 8 ? ReadU64() : ReadU32();

    /// <summary>Reads a NUL-terminated UTF-8 string.</summary>
    public string ReadCString()
    {
        var slice = _data[Position..];
        var end = slice.IndexOf((byte)0);
        if (end < 0) end = slice.Length;
        var s = Encoding.UTF8.GetString(slice[..end]);
        Position += end + 1;
        return s;
    }

    /// <summary>Advances the cursor by <paramref name="count"/> bytes.</summary>
    /// <param name="count">The byte count to skip.</param>
    public void Skip(int count)
    {
        if (count < 0 || Position + count > _data.Length)
            throw new ArgumentOutOfRangeException(nameof(count));
        Position += count;
    }

    /// <summary>Reads a NUL-terminated UTF-8 string at an absolute offset without moving the cursor.</summary>
    /// <param name="offset">The absolute byte offset.</param>
    public readonly string? ReadCStringAt(long offset)
    {
        if (offset < 0 || offset >= _data.Length) return null;
        var slice = _data[(int)offset..];
        var end = slice.IndexOf((byte)0);
        if (end < 0) end = slice.Length;
        return Encoding.UTF8.GetString(slice[..end]);
    }
}
