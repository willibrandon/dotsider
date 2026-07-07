using Dotsider.Core.Analysis.Models;
using System.Numerics;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Extracts strings from .NET assemblies across three sources:
/// the #US heap (user string literals), the #Strings heap (metadata identifiers),
/// and raw printable character sequences from the binary.
/// </summary>
public sealed class StringExtractor(AssemblyAnalyzer analyzer)
{

    /// <summary>Number of malformed entries skipped during the last <see cref="ExtractUserStrings"/> call.</summary>
    public int SkippedUserStringCount { get; private set; }

    /// <summary>Number of malformed entries skipped during the last <see cref="ExtractMetadataStrings"/> call.</summary>
    public int SkippedMetadataStringCount { get; private set; }

    /// <summary>
    /// Extracts all user string literals from the #US metadata heap.
    /// These are the string constants used in IL code via <c>ldstr</c>.
    /// </summary>
    /// <returns>A list of string entries from the user strings heap.</returns>
    public IReadOnlyList<StringEntry> ExtractUserStrings()
    {
        var reader = analyzer.GetMetadataReader();
        if (reader is null) return [];

        SkippedUserStringCount = 0;
        var results = new List<StringEntry>();

        if (reader.GetHeapSize(HeapIndex.UserString) == 0) return results;

        var handle = MetadataTokens.UserStringHandle(1);

        while (!handle.IsNil)
        {
            var offset = MetadataTokens.GetHeapOffset(handle);
            try
            {
                var value = reader.GetUserString(handle);
                if (!string.IsNullOrEmpty(value))
                {
                    results.Add(new StringEntry(offset, value, StringSource.UserStrings));
                }
            }
            catch
            {
                SkippedUserStringCount++;
            }

            try
            {
                handle = reader.GetNextHandle(handle);
            }
            catch (BadImageFormatException)
            {
                break;
            }
        }

        return results;
    }

    /// <summary>
    /// Extracts all identifier strings from the #Strings metadata heap.
    /// These are type names, method names, namespace names, and other metadata identifiers.
    /// </summary>
    /// <returns>A list of string entries from the metadata strings heap.</returns>
    public IReadOnlyList<StringEntry> ExtractMetadataStrings()
    {
        var reader = analyzer.GetMetadataReader();
        if (reader is null) return [];

        SkippedMetadataStringCount = 0;
        var results = new List<StringEntry>();

        if (reader.GetHeapSize(HeapIndex.String) == 0) return results;

        var handle = MetadataTokens.StringHandle(1);

        while (!handle.IsNil)
        {
            var offset = MetadataTokens.GetHeapOffset(handle);
            try
            {
                var value = reader.GetString(handle);
                if (!string.IsNullOrEmpty(value))
                {
                    results.Add(new StringEntry(offset, value, StringSource.MetadataStrings));
                }
            }
            catch
            {
                SkippedMetadataStringCount++;
            }

            try
            {
                handle = reader.GetNextHandle(handle);
            }
            catch (BadImageFormatException)
            {
                break;
            }
        }

        return results;
    }

    /// <summary>
    /// Extracts raw printable character sequences from the binary file.
    /// Scans for consecutive ASCII printable characters (0x20-0x7E) of at least <paramref name="minLength"/> bytes.
    /// </summary>
    /// <param name="minLength">The minimum number of consecutive printable characters to consider a string.</param>
    /// <returns>A list of string entries extracted from the raw binary.</returns>
    public IReadOnlyList<StringEntry> ExtractRawStrings(int minLength = 4)
    {
        var bytes = analyzer.RawBytes.Span;
        var results = new List<StringEntry>();

        var runStart = -1;
        var position = 0;

        void EmitRun(ReadOnlySpan<byte> data, int start, int end)
        {
            if (end - start < minLength) return;
            results.Add(new StringEntry(
                start, Encoding.ASCII.GetString(data[start..end]), StringSource.RawBinary));
        }

        // Printable bytes make up over a third of typical machine code, so runs start
        // every few bytes and per-run vectorized searches pay their setup cost without
        // covering ground. Classifying 64 bytes at a time into a bitmask keeps the
        // SIMD work fixed per block, and run boundaries fall out of bit scans.
        if (Vector128.IsHardwareAccelerated)
        {
            for (; position + 64 <= bytes.Length; position += 64)
            {
                var mask = PrintableMask64(bytes, position);
                var bit = 0;
                while (bit < 64)
                {
                    if (runStart < 0)
                    {
                        var remaining = mask >> bit;
                        if (remaining == 0) break;
                        bit += BitOperations.TrailingZeroCount(remaining);
                        runStart = position + bit;
                    }
                    else
                    {
                        var remaining = ~mask >> bit;
                        if (remaining == 0) break;
                        bit += BitOperations.TrailingZeroCount(remaining);
                        EmitRun(bytes, runStart, position + bit);
                        runStart = -1;
                    }
                }
            }
        }

        // Scalar tail; also the whole scan when SIMD is unavailable.
        for (; position < bytes.Length; position++)
        {
            if (bytes[position] is >= 0x20 and <= 0x7E)
            {
                if (runStart < 0) runStart = position;
            }
            else if (runStart >= 0)
            {
                EmitRun(bytes, runStart, position);
                runStart = -1;
            }
        }

        if (runStart >= 0) EmitRun(bytes, runStart, bytes.Length);

        return results;
    }

    /// <summary>
    /// Extracts printable UTF-16LE character sequences from the binary file. Scans for
    /// runs of little-endian code units in the printable ASCII range (0x20–0x7E stored
    /// as two bytes), which is how managed string literals freeze in Native AOT images.
    /// The file is scanned once per byte parity, so runs at odd offsets are found too.
    /// Accepting the full BMP instead would drown the results in noise — most random
    /// 16-bit values decode to a printable character.
    /// </summary>
    /// <param name="minLength">The minimum number of consecutive printable characters to consider a string.</param>
    /// <returns>A list of string entries extracted from the raw binary.</returns>
    public IReadOnlyList<StringEntry> ExtractRawUtf16Strings(int minLength = 4)
    {
        var bytes = analyzer.RawBytes.Span;
        var even = ScanUtf16Runs(bytes, parity: 0, minLength);
        var odd = ScanUtf16Runs(bytes, parity: 1, minLength);

        // Qualifying runs of opposite parity never overlap — a code unit's zero high
        // byte cannot also be another unit's printable low byte — so merging the two
        // sorted passes yields entries in ascending offset order.
        var results = new List<StringEntry>(even.Count + odd.Count);
        int e = 0, o = 0;
        while (e < even.Count || o < odd.Count)
        {
            if (o >= odd.Count || (e < even.Count && even[e].Offset < odd[o].Offset))
                results.Add(even[e++]);
            else
                results.Add(odd[o++]);
        }

        return results;
    }

    /// <summary>
    /// Finds maximal runs of printable UTF-16LE code units starting at the given byte
    /// parity. Reinterpreting the bytes as 16-bit units makes the printable-range test
    /// a single comparison: a little-endian code unit in 0x20–0x7E has a printable low
    /// byte and a zero high byte by construction.
    /// </summary>
    private static List<StringEntry> ScanUtf16Runs(ReadOnlySpan<byte> bytes, int parity, int minLength)
    {
        if (bytes.Length <= parity) return [];

        var chars = MemoryMarshal.Cast<byte, char>(bytes[parity..]);
        var units = MemoryMarshal.Cast<byte, ushort>(bytes[parity..]);
        var results = new List<StringEntry>();

        var runStart = -1;
        var position = 0;

        void EmitRun(ReadOnlySpan<char> data, int start, int end)
        {
            if (end - start < minLength) return;
            results.Add(new StringEntry(
                parity + start * 2, new string(data[start..end]), StringSource.RawBinaryUtf16));
        }

        if (Vector128.IsHardwareAccelerated)
        {
            for (; position + 64 <= units.Length; position += 64)
            {
                var mask = PrintableMask64(units, position);
                var bit = 0;
                while (bit < 64)
                {
                    if (runStart < 0)
                    {
                        var remaining = mask >> bit;
                        if (remaining == 0) break;
                        bit += BitOperations.TrailingZeroCount(remaining);
                        runStart = position + bit;
                    }
                    else
                    {
                        var remaining = ~mask >> bit;
                        if (remaining == 0) break;
                        bit += BitOperations.TrailingZeroCount(remaining);
                        EmitRun(chars, runStart, position + bit);
                        runStart = -1;
                    }
                }
            }
        }

        // Scalar tail; also the whole scan when SIMD is unavailable.
        for (; position < units.Length; position++)
        {
            if (units[position] is >= 0x20 and <= 0x7E)
            {
                if (runStart < 0) runStart = position;
            }
            else if (runStart >= 0)
            {
                EmitRun(chars, runStart, position);
                runStart = -1;
            }
        }

        if (runStart >= 0) EmitRun(chars, runStart, units.Length);

        return results;
    }

    /// <summary>
    /// Classifies the 64 bytes at <paramref name="offset"/> into a bitmask where bit
    /// <c>i</c> is set when byte <c>offset + i</c> is printable ASCII. The caller must
    /// guarantee 64 bytes are available; the unsigned wraparound compare folds the
    /// range test into one instruction.
    /// </summary>
    private static ulong PrintableMask64(ReadOnlySpan<byte> bytes, int offset)
    {
        var floor = Vector128.Create((byte)0x20);
        var range = Vector128.Create((byte)(0x7E - 0x20));
        var mask = 0UL;
        for (var i = 0; i < 4; i++)
        {
            var block = Vector128.LoadUnsafe(in bytes[offset], (nuint)(i * 16));
            var printable = Vector128.LessThanOrEqual(block - floor, range);
            mask |= (ulong)printable.ExtractMostSignificantBits() << (i * 16);
        }

        return mask;
    }

    /// <summary>
    /// Classifies the 64 UTF-16 code units at <paramref name="offset"/> into a bitmask
    /// where bit <c>i</c> is set when unit <c>offset + i</c> is printable ASCII. The
    /// caller must guarantee 64 units are available.
    /// </summary>
    private static ulong PrintableMask64(ReadOnlySpan<ushort> units, int offset)
    {
        var floor = Vector128.Create((ushort)0x20);
        var range = Vector128.Create((ushort)(0x7E - 0x20));
        var mask = 0UL;
        for (var i = 0; i < 8; i++)
        {
            var block = Vector128.LoadUnsafe(in units[offset], (nuint)(i * 8));
            var printable = Vector128.LessThanOrEqual(block - floor, range);
            mask |= (ulong)printable.ExtractMostSignificantBits() << (i * 8);
        }

        return mask;
    }
}
