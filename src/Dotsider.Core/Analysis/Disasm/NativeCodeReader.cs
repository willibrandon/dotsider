using System.Buffers.Binary;

namespace Dotsider.Core.Analysis.Disasm;

/// <summary>
/// A forward cursor over a native code window with the fixed-width little-endian reads the x64 and
/// A64 decoders are built from: unsigned and sign-extended integers, and a peek that does not
/// advance. Out-of-bounds reads throw <see cref="ArgumentOutOfRangeException"/>, which the decoders
/// catch to end a walk leniently, matching the DWARF/PDB readers' convention.
/// </summary>
internal ref struct NativeCodeReader(ReadOnlySpan<byte> data)
{
    private readonly ReadOnlySpan<byte> _data = data;

    /// <summary>The cursor position, in bytes from the start of the window.</summary>
    public int Position { get; set; }

    /// <summary>The bytes remaining ahead of the cursor.</summary>
    public readonly int Remaining => _data.Length - Position;

    /// <summary>Whether at least one byte remains to read.</summary>
    public readonly bool HasMore => Position < _data.Length;

    /// <summary>Returns the next byte without advancing.</summary>
    public readonly byte Peek() => _data[Position];

    /// <summary>Returns the byte <paramref name="ahead"/> positions ahead without advancing.</summary>
    /// <param name="ahead">The lookahead distance in bytes.</param>
    public readonly byte PeekAt(int ahead) => _data[Position + ahead];

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

    /// <summary>Reads a sign-extended little-endian i8.</summary>
    public long ReadI8() => (sbyte)_data[Position++];

    /// <summary>Reads a sign-extended little-endian i16.</summary>
    public long ReadI16() => BinaryPrimitives.ReadInt16LittleEndian(ReadBytes(2));

    /// <summary>Reads a sign-extended little-endian i32.</summary>
    public long ReadI32() => BinaryPrimitives.ReadInt32LittleEndian(ReadBytes(4));

    private ReadOnlySpan<byte> ReadBytes(int count)
    {
        var slice = _data.Slice(Position, count);
        Position += count;
        return slice;
    }
}
