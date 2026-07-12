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
    private const uint MaximumMetadataRowId = 0x00FF_FFFF;

    private readonly R2RNativeReader _reader;
    private readonly int _baseOffset;
    private readonly uint _count;
    private readonly int _endOffset;
    private readonly byte _entryIndexSize;
    private readonly int _treeStartOffset;

    /// <summary>
    /// Parses the array header at <paramref name="offset"/> and restricts every index, tree, and
    /// element cursor to the containing section ending at <paramref name="endOffset"/>.
    /// </summary>
    public R2RNativeArray(R2RNativeReader reader, int offset, int endOffset)
    {
        ArgumentNullException.ThrowIfNull(reader);
        if (endOffset < offset)
        {
            throw new BadImageFormatException("ReadyToRun NativeArray has an invalid section range.");
        }

        _reader = reader.Slice(offset, endOffset - offset);
        _endOffset = endOffset;
        _baseOffset = _reader.DecodeUnsigned(offset, out var header);
        _count = header >> 2;
        _entryIndexSize = (byte)(header & 3);

        if (_count > MaximumMetadataRowId)
        {
            throw new BadImageFormatException("ReadyToRun NativeArray exceeds the metadata row-id range.");
        }

        var blockCount = _count / BlockSize + (_count % BlockSize == 0 ? 0U : 1U);
        var indexByteCount = (long)blockCount * EntryIndexSizeStride();
        var treeStartOffset = (long)_baseOffset + indexByteCount;
        if (treeStartOffset > _endOffset)
        {
            throw new BadImageFormatException(
                "ReadyToRun NativeArray block index exceeds its containing section.");
        }

        _treeStartOffset = (int)treeStartOffset;
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
        {
            return false;
        }

        // Block index: the offset of the block's tree root, stored as a 1/2/4-byte entry.
        var blockIndexOffset = checked(
            _baseOffset + (int)(index / BlockSize) * EntryIndexSizeStride());
        uint offset = _entryIndexSize switch
        {
            0 => _reader.ReadByte(ref blockIndexOffset),
            1 => _reader.ReadUInt16(ref blockIndexOffset),
            _ => _reader.ReadUInt32(ref blockIndexOffset),
        };
        var nodeOffset = (long)_baseOffset + offset;
        if (nodeOffset < _treeStartOffset || nodeOffset >= _endOffset)
        {
            throw new BadImageFormatException(
                "ReadyToRun NativeArray tree root lies outside its containing section.");
        }

        var currentOffset = (int)nodeOffset;

        for (var bit = BlockSize >> 1; bit > 0; bit >>= 1)
        {
            var nextOffset = _reader.DecodeUnsigned(currentOffset, out var val);
            if ((index & (uint)bit) != 0)
            {
                if ((val & 2) != 0)
                {
                    var branchOffset = (long)currentOffset + (val >> 2);
                    if (branchOffset < nextOffset || branchOffset >= _endOffset)
                    {
                        throw new BadImageFormatException(
                            "ReadyToRun NativeArray branch lies outside its containing section.");
                    }

                    currentOffset = (int)branchOffset;
                    continue;
                }
            }
            else
            {
                if ((val & 1) != 0)
                {
                    if (nextOffset >= _endOffset)
                    {
                        throw new BadImageFormatException(
                            "ReadyToRun NativeArray branch ends at the section boundary.");
                    }

                    currentOffset = nextOffset;
                    continue;
                }
            }

            // Leaf: a matching special node ends the walk; anything else means the index is absent.
            if ((val & 3) == 0 && (val >> 2) == (index & (BlockSize - 1)))
            {
                if (nextOffset >= _endOffset)
                {
                    throw new BadImageFormatException(
                        "ReadyToRun NativeArray element lies outside its containing section.");
                }

                currentOffset = nextOffset;
                break;
            }

            return false;
        }

        elementOffset = currentOffset;
        return true;
    }

    private int EntryIndexSizeStride() => _entryIndexSize switch { 0 => 1, 1 => 2, _ => 4 };
}
