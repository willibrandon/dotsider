namespace Dotsider.Core.Analysis.ReadyToRun;

/// <summary>
/// A NativeFormat hashtable (the runtime's <c>NativeHashtable</c>), used by
/// <c>InstanceMethodEntryPoints</c>. Entries are enumerated across all buckets; each yields the
/// file offset of its payload (a method signature followed by an encoded runtime-function index).
/// </summary>
internal sealed class R2RNativeHashtable
{
    private readonly R2RNativeReader _reader;
    private readonly int _baseOffset;
    private readonly uint _bucketMask;
    private readonly int _bucketDataOffset;
    private readonly byte _entryIndexSize;
    private readonly int _endOffset;
    private readonly int _payloadDataOffset;

    /// <summary>Parses the hashtable header at <paramref name="offset"/>, bounded by <paramref name="endOffset"/>.</summary>
    public R2RNativeHashtable(R2RNativeReader reader, int offset, int endOffset)
    {
        ArgumentNullException.ThrowIfNull(reader);
        if (endOffset < offset)
        {
            throw new BadImageFormatException("ReadyToRun NativeHashtable has an invalid section range.");
        }

        _reader = reader.Slice(offset, endOffset - offset);
        var cursor = offset;
        var header = _reader.ReadByte(ref cursor);
        _baseOffset = cursor;

        var numberOfBucketsShift = header >> 2;
        if (numberOfBucketsShift > 31)
        {
            throw new BadImageFormatException("ReadyToRun hashtable has too many buckets.");
        }

        _bucketMask = (1U << numberOfBucketsShift) - 1;

        _entryIndexSize = (byte)(header & 3);
        if (_entryIndexSize > 2)
        {
            throw new BadImageFormatException("ReadyToRun hashtable has an invalid entry index size.");
        }

        _endOffset = endOffset;
        var bucketCount = (long)_bucketMask + 1;
        var bucketIndexByteCount = (bucketCount + 1) * EntryIndexSizeStride();
        var bucketDataOffset = (long)_baseOffset + bucketIndexByteCount;
        if (bucketDataOffset > _endOffset)
        {
            throw new BadImageFormatException(
                "ReadyToRun NativeHashtable bucket index exceeds its containing section.");
        }

        _bucketDataOffset = (int)bucketDataOffset;

        var previous = ReadBucketIndex(0);
        if ((long)_baseOffset + previous < _bucketDataOffset)
        {
            throw new BadImageFormatException(
                "ReadyToRun NativeHashtable bucket data overlaps its bucket index.");
        }

        var bucketCountAsInt = checked((int)bucketCount);
        for (var index = 1; index <= bucketCountAsInt; index++)
        {
            var current = ReadBucketIndex((uint)index);
            if (current < previous)
            {
                throw new BadImageFormatException(
                    "ReadyToRun NativeHashtable bucket ranges are not monotonic.");
            }

            previous = current;
        }

        var payloadDataOffset = (long)_baseOffset + previous;
        if (payloadDataOffset > _endOffset)
        {
            throw new BadImageFormatException(
                "ReadyToRun NativeHashtable bucket data exceeds its containing section.");
        }

        _payloadDataOffset = (int)payloadDataOffset;
    }

    /// <summary>Enumerates the file offset of every entry's payload across all buckets.</summary>
    public IEnumerable<int> AllEntryOffsets()
    {
        for (uint bucket = 0; ; bucket++)
        {
            var cursor = BucketStart(bucket, out var end);
            var bucketReader = _reader.Slice(cursor, end - cursor);
            while (cursor < end)
            {
                // Each entry is a low-hashcode byte then a NativeReader signed relative offset; the
                // payload (signature + runtime-function index) sits at pos + delta.
                bucketReader.ReadByte(ref cursor);
                var pos = cursor;
                cursor = bucketReader.DecodeSigned(pos, out var delta);
                var payloadOffset = (long)pos + delta;
                if (payloadOffset < _payloadDataOffset || payloadOffset >= _endOffset)
                {
                    throw new BadImageFormatException(
                        "ReadyToRun NativeHashtable payload lies outside its containing section.");
                }

                yield return (int)payloadOffset;
            }

            if (bucket >= _bucketMask)
            {
                yield break;
            }
        }
    }

    private int BucketStart(uint bucket, out int endOffset)
    {
        int cursor;
        uint start;
        uint end;
        if (_entryIndexSize == 0)
        {
            cursor = _baseOffset + (int)bucket;
            start = _reader.ReadByte(ref cursor);
            end = _reader.ReadByte(ref cursor);
        }
        else if (_entryIndexSize == 1)
        {
            cursor = _baseOffset + 2 * (int)bucket;
            start = _reader.ReadUInt16(ref cursor);
            end = _reader.ReadUInt16(ref cursor);
        }
        else
        {
            cursor = _baseOffset + 4 * (int)bucket;
            start = _reader.ReadUInt32(ref cursor);
            end = _reader.ReadUInt32(ref cursor);
        }

        var startOffset = (long)start + _baseOffset;
        var bucketEndOffset = (long)end + _baseOffset;
        if (startOffset < _bucketDataOffset || startOffset > bucketEndOffset
            || bucketEndOffset > _endOffset)
        {
            throw new BadImageFormatException(
                "ReadyToRun NativeHashtable bucket range lies outside its containing section.");
        }

        endOffset = (int)bucketEndOffset;
        return (int)startOffset;
    }

    private int EntryIndexSizeStride() => 1 << _entryIndexSize;

    private uint ReadBucketIndex(uint index)
    {
        var cursor = checked(_baseOffset + (int)index * EntryIndexSizeStride());
        return _entryIndexSize switch
        {
            0 => _reader.ReadByte(ref cursor),
            1 => _reader.ReadUInt16(ref cursor),
            _ => _reader.ReadUInt32(ref cursor),
        };
    }
}
