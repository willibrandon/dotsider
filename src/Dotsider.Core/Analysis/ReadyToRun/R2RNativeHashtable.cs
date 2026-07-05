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
    private readonly byte _entryIndexSize;
    private readonly int _endOffset;

    /// <summary>Parses the hashtable header at <paramref name="offset"/>, bounded by <paramref name="endOffset"/>.</summary>
    public R2RNativeHashtable(R2RNativeReader reader, int offset, int endOffset)
    {
        _reader = reader;
        var cursor = offset;
        var header = reader.ReadByte(ref cursor);
        _baseOffset = cursor;

        var numberOfBucketsShift = header >> 2;
        if (numberOfBucketsShift > 31)
            throw new BadImageFormatException("ReadyToRun hashtable has too many buckets.");
        _bucketMask = (uint)((1 << numberOfBucketsShift) - 1);

        _entryIndexSize = (byte)(header & 3);
        if (_entryIndexSize > 2)
            throw new BadImageFormatException("ReadyToRun hashtable has an invalid entry index size.");

        _endOffset = endOffset;
    }

    /// <summary>Enumerates the file offset of every entry's payload across all buckets.</summary>
    public IEnumerable<int> AllEntryOffsets()
    {
        for (uint bucket = 0; ; bucket++)
        {
            var cursor = BucketStart(bucket, out var end);
            while (cursor < end)
            {
                // Each entry is a low-hashcode byte then a NativeReader signed relative offset; the
                // payload (signature + runtime-function index) sits at pos + delta.
                _reader.ReadByte(ref cursor);
                var pos = cursor;
                cursor = _reader.DecodeSigned(pos, out var delta);
                yield return pos + delta;
            }

            if (bucket >= _bucketMask)
                yield break;
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

        endOffset = (int)end + _baseOffset;
        return (int)start + _baseOffset;
    }
}
