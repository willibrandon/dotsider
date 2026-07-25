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
    private readonly int _bucketCount;
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
        if (numberOfBucketsShift >= 31)
        {
            throw new BadImageFormatException("ReadyToRun hashtable has too many buckets.");
        }

        var bucketCount = 1U << numberOfBucketsShift;
        _bucketCount = (int)bucketCount;

        _entryIndexSize = (byte)(header & 3);
        if (_entryIndexSize > 2)
        {
            throw new BadImageFormatException("ReadyToRun hashtable has an invalid entry index size.");
        }

        _endOffset = endOffset;
        var bucketIndexByteCount = ((long)_bucketCount + 1) * EntryIndexSizeStride();
        var bucketDataOffset = (long)_baseOffset + bucketIndexByteCount;
        if (bucketDataOffset > _endOffset)
        {
            throw new BadImageFormatException(
                "ReadyToRun NativeHashtable bucket index exceeds its containing section.");
        }

        _bucketDataOffset = (int)bucketDataOffset;

        var first = ReadBucketIndex(0);
        if ((long)_baseOffset + first < _bucketDataOffset)
        {
            throw new BadImageFormatException(
                "ReadyToRun NativeHashtable bucket data overlaps its bucket index.");
        }

        var final = ReadBucketIndex(_bucketCount);
        var payloadDataOffset = (long)_baseOffset + final;
        if (final < first
            || payloadDataOffset < _bucketDataOffset
            || payloadDataOffset > _endOffset)
        {
            throw new BadImageFormatException(
                "ReadyToRun NativeHashtable bucket data has an invalid containing-section extent.");
        }

        _payloadDataOffset = (int)payloadDataOffset;
    }

    /// <summary>The number of buckets encoded by the validated hashtable header.</summary>
    public int BucketCount => _bucketCount;

    /// <summary>Enumerates the file offset of every entry's payload across all buckets.</summary>
    public IEnumerable<int> AllEntryOffsets()
    {
        for (var bucket = 0; bucket < _bucketCount; bucket++)
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
        }
    }

    private int BucketStart(int bucket, out int endOffset)
    {
        var start = ReadBucketIndex(bucket);
        var end = ReadBucketIndex(bucket + 1);

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

    private uint ReadBucketIndex(int index)
    {
        var relativeOffset = (long)index * EntryIndexSizeStride();
        var absoluteOffset = (long)_baseOffset + relativeOffset;
        if (index < 0
            || index > _bucketCount
            || absoluteOffset < _baseOffset
            || absoluteOffset > _bucketDataOffset - EntryIndexSizeStride())
        {
            throw new BadImageFormatException(
                "ReadyToRun NativeHashtable bucket index lies outside its boundary table.");
        }

        var cursor = (int)absoluteOffset;
        return _entryIndexSize switch
        {
            0 => _reader.ReadByte(ref cursor),
            1 => _reader.ReadUInt16(ref cursor),
            _ => _reader.ReadUInt32(ref cursor),
        };
    }
}
