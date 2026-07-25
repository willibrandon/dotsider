using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Reads the small IL instruction subset used by an mstat data stream without advancing after a
/// truncated or unexpected instruction.
/// </summary>
internal struct IlCursor
{
    private readonly byte[] _il;
    private int _position;

    internal IlCursor(byte[] il)
    {
        _il = il;
    }

    /// <summary>Gets the number of unread bytes in the IL stream.</summary>
    internal int RemainingByteCount => _il.Length - _position;

    internal bool TryReadInt(out int value)
    {
        value = 0;
        if (_position >= _il.Length)
        {
            return false;
        }

        switch (_il[_position])
        {
            case 0x15:
                value = -1;
                _position++;
                return true;
            case >= 0x16 and <= 0x1E:
                value = _il[_position] - 0x16;
                _position++;
                return true;
            case 0x1F when _position + 1 < _il.Length:
                value = (sbyte)_il[_position + 1];
                _position += 2;
                return true;
            case 0x20 when _position + 4 < _il.Length:
                value = BitConverter.ToInt32(_il, _position + 1);
                _position += 5;
                return true;
            default:
                return false;
        }
    }

    internal bool TryReadToken(out int token)
    {
        token = 0;
        if (_position + 4 >= _il.Length || _il[_position] != 0xD0)
        {
            return false;
        }

        token = BitConverter.ToInt32(_il, _position + 1);
        _position += 5;
        return true;
    }

    internal bool TryReadUserString(MetadataReader reader, out string value)
    {
        value = string.Empty;
        if (_position + 4 >= _il.Length || _il[_position] != 0x72)
        {
            return false;
        }

        var token = BitConverter.ToInt32(_il, _position + 1);
        _position += 5;
        value = reader.GetUserString(MetadataTokens.UserStringHandle(token));
        return true;
    }
}
