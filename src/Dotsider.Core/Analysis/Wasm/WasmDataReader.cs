namespace Dotsider.Core.Analysis.Wasm;

/// <summary>
/// A forward-only reader whose cursor is confined to one WebAssembly module, section, subsection,
/// or function body.
/// </summary>
internal ref struct WasmDataReader
{
    private readonly ReadOnlySpan<byte> _bytes;
    private readonly int _end;
    private int _position;

    internal WasmDataReader(ReadOnlySpan<byte> bytes, int position = 0)
        : this(bytes, position, bytes.Length)
    {
    }

    private WasmDataReader(ReadOnlySpan<byte> bytes, int position, int end)
    {
        if ((uint)position > (uint)end || (uint)end > (uint)bytes.Length)
            throw new ArgumentOutOfRangeException(nameof(position));

        _bytes = bytes;
        _position = position;
        _end = end;
    }

    internal readonly bool AtEnd => _position == _end;

    internal readonly int Position => _position;

    internal readonly int Remaining => _end - _position;

    internal byte ReadByte()
    {
        if (_position >= _end)
            throw new InvalidDataException("Unexpected end of WebAssembly data.");

        return _bytes[_position++];
    }

    internal ReadOnlySpan<byte> ReadBytes(int count)
    {
        if (count < 0 || count > Remaining)
            throw new InvalidDataException("WebAssembly data extends past its containing region.");

        var result = _bytes.Slice(_position, count);
        _position += count;
        return result;
    }

    internal long ReadSignedLeb128()
    {
        long value = 0;
        var shift = 0;
        for (var index = 0; index < 10; index++)
        {
            var current = ReadByte();
            if (index == 9 && (current & 0x7F) is not (0x00 or 0x7F))
                throw new InvalidDataException("A signed WebAssembly LEB128 value is too large.");

            value |= (long)(current & 0x7F) << shift;
            if ((current & 0x80) == 0)
            {
                if (shift < 63 && (current & 0x40) != 0)
                    value |= -1L << (shift + 7);
                return value;
            }

            shift += 7;
        }

        throw new InvalidDataException("A signed WebAssembly LEB128 value is too large.");
    }

    internal WasmDataReader ReadSubReader(int length)
    {
        if (length < 0 || length > Remaining)
            throw new InvalidDataException("WebAssembly data extends past its containing region.");

        var start = _position;
        _position += length;
        return new WasmDataReader(_bytes, start, _position);
    }

    internal uint ReadUnsignedLeb12832()
    {
        uint value = 0;
        for (var shift = 0; shift < 35; shift += 7)
        {
            var current = ReadByte();
            if (shift == 28 && (current & 0xF0) != 0)
                throw new InvalidDataException("A WebAssembly u32 LEB128 value is too large.");

            value |= (uint)(current & 0x7F) << shift;
            if ((current & 0x80) == 0)
                return value;
        }

        throw new InvalidDataException("A WebAssembly u32 LEB128 value is too large.");
    }

    internal ulong ReadUnsignedLeb12864()
    {
        ulong value = 0;
        for (var shift = 0; shift < 70; shift += 7)
        {
            var current = ReadByte();
            if (shift == 63 && (current & 0xFE) != 0)
                throw new InvalidDataException("A WebAssembly u64 LEB128 value is too large.");

            value |= (ulong)(current & 0x7F) << shift;
            if ((current & 0x80) == 0)
                return value;
        }

        throw new InvalidDataException("A WebAssembly u64 LEB128 value is too large.");
    }
}
