namespace Dotsider.Core.Analysis.ReadyToRun;

/// <summary>
/// A NativeFormat sparse array (the runtime's <c>NativeArray</c>) — a compact, binary-searchable
/// map from a dense index to an encoded element offset. Used by <c>MethodDefEntryPoints</c>, keyed
/// by <c>MethodDef rid - 1</c>. Each lookup walks a per-block four-level bit tree, so absent
/// indices cost no storage.
/// </summary>
internal sealed class R2RNativeArray
{
    private const int BlockSize = 16;

    private readonly R2RNativeReader _reader;
    private readonly int _baseOffset;
    private readonly uint _count;
    private readonly byte _entryIndexSize;

    /// <summary>Parses the array header at <paramref name="offset"/> (its section's file offset).</summary>
    public R2RNativeArray(R2RNativeReader reader, int offset)
    {
        _reader = reader;
        _baseOffset = reader.DecodeUnsigned(offset, out var header);
        _count = header >> 2;
        _entryIndexSize = (byte)(header & 3);
    }

    /// <summary>The number of index slots the array spans (including absent ones).</summary>
    public uint Count => _count;

    /// <summary>
    /// Finds the encoded-element file offset for <paramref name="index"/>. Returns false when the
    /// index is out of range or absent from the tree.
    /// </summary>
    public bool TryGetAt(uint index, out int elementOffset)
    {
        elementOffset = 0;
        if (index >= _count)
            return false;

        // Block index: the offset of the block's tree root, stored as a 1/2/4-byte entry.
        int blockIndexOffset = _baseOffset + (int)((index / BlockSize) * EntryIndexSizeStride());
        uint offset = _entryIndexSize switch
        {
            0 => _reader.ReadByte(ref blockIndexOffset),
            1 => _reader.ReadUInt16(ref blockIndexOffset),
            _ => _reader.ReadUInt32(ref blockIndexOffset),
        };
        offset += (uint)_baseOffset;

        for (var bit = BlockSize >> 1; bit > 0; bit >>= 1)
        {
            var offset2 = (uint)_reader.DecodeUnsigned((int)offset, out var val);
            if ((index & (uint)bit) != 0)
            {
                if ((val & 2) != 0)
                {
                    offset += val >> 2;
                    continue;
                }
            }
            else
            {
                if ((val & 1) != 0)
                {
                    offset = offset2;
                    continue;
                }
            }

            // Leaf: a matching special node ends the walk; anything else means the index is absent.
            if ((val & 3) == 0 && (val >> 2) == (index & (BlockSize - 1)))
            {
                offset = offset2;
                break;
            }

            return false;
        }

        elementOffset = (int)offset;
        return true;
    }

    private int EntryIndexSizeStride() => _entryIndexSize switch { 0 => 1, 1 => 2, _ => 4 };
}
