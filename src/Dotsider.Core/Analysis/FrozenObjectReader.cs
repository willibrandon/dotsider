using System.Buffers.Binary;
using System.Text;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Recovers frozen <see cref="string"/> literals from a Native AOT binary's frozen object
/// region (ReadyToRun section 206). The region is a sequence of GC objects, each laid out
/// as a zero sync block, a MethodTable pointer, and the instance fields. Objects are sized
/// by reading their MethodTable, so the walk steps over non-string objects to reach every
/// string. On Linux the region is a NOBITS segment the runtime fills at startup, so it has
/// no file backing and yields nothing here; the raw UTF-16 scan surfaces that text instead.
/// </summary>
internal static class FrozenObjectReader
{
    private const uint HasComponentSizeFlag = 0x8000_0000;
    private const int MaxStringLength = 1 << 20;
    private const int MaxObjects = 1 << 22;

    /// <summary>
    /// Reads the frozen string literals from the frozen object region.
    /// </summary>
    /// <param name="bytes">The raw image bytes.</param>
    /// <param name="sections">The ReadyToRun section table.</param>
    /// <param name="addressSpace">The image's virtual-address to file-offset map.</param>
    /// <returns>The recovered frozen strings, or an empty list.</returns>
    internal static IReadOnlyList<StringEntry> ReadStrings(
        ReadOnlySpan<byte> bytes,
        IReadOnlyList<RtrSection> sections,
        NativeAddressSpace addressSpace)
    {
        RtrSection? region = null;
        foreach (var section in sections)
        {
            if (section.SectionId == ReadyToRunReader.FrozenObjectRegion)
            {
                region = section;
                break;
            }
        }

        if (region is not { } frozen || ReadyToRunReader.FileRange(frozen) is not var (start, length))
            return [];

        var pointerSize = addressSpace.PointerSize;
        var stringBaseSize = (uint)(2 * pointerSize + 6); // sync + MethodTable + length + null char
        var end = Math.Min(start + length, bytes.Length);

        var results = new List<StringEntry>();
        var eeTypes = new Dictionary<ulong, (uint BaseSize, int ComponentSize)>();

        var pos = start;
        for (var guard = 0; guard < MaxObjects; guard++)
        {
            pos = Align(pos, pointerSize);
            if (pos + 2 * pointerSize > end) break;

            // [sync block][MethodTable*]; the object reference points at the MethodTable.
            var methodTable = ReadPointer(bytes, pos + pointerSize, pointerSize);
            if (methodTable == 0) break; // aligned null terminator ends the region

            if (!TryGetEEType(bytes, addressSpace, eeTypes, methodTable, out var baseSize, out var componentSize))
                break;

            var numComponents = 0;
            if (componentSize != 0)
            {
                var lengthOffset = pos + 2 * pointerSize;
                if (lengthOffset + 4 > end) break;
                numComponents = BinaryPrimitives.ReadInt32LittleEndian(bytes[lengthOffset..]);
                if (numComponents is < 0 or > MaxStringLength) break;
            }

            var objectSize = (long)baseSize + (long)numComponents * componentSize;
            objectSize = Math.Max(objectSize, 3 * pointerSize);
            objectSize = Align((int)objectSize, pointerSize);
            if (objectSize <= 0) break;

            // A frozen string: HasComponentSize with component size 2 and the String base size.
            if (componentSize == 2 && baseSize == stringBaseSize && numComponents > 0)
            {
                var charsOffset = pos + 2 * pointerSize + 4;
                var byteCount = numComponents * 2;
                if (charsOffset + byteCount <= end)
                {
                    var value = Encoding.Unicode.GetString(bytes.Slice(charsOffset, byteCount));
                    results.Add(new StringEntry(pos, value, StringSource.FrozenObject));
                }
            }

            pos += (int)objectSize;
        }

        return results;
    }

    private static bool TryGetEEType(
        ReadOnlySpan<byte> bytes,
        NativeAddressSpace addressSpace,
        Dictionary<ulong, (uint BaseSize, int ComponentSize)> cache,
        ulong methodTable,
        out uint baseSize,
        out int componentSize)
    {
        if (cache.TryGetValue(methodTable, out var cached))
        {
            baseSize = cached.BaseSize;
            componentSize = cached.ComponentSize;
            return true;
        }

        baseSize = 0;
        componentSize = 0;
        if (!addressSpace.TryGetFileOffset(methodTable, out var offset, out var available) || available < 8)
            return false;

        var flags = BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
        baseSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(offset + 4)..]);
        componentSize = (flags & HasComponentSizeFlag) != 0 ? (int)(flags & 0xFFFF) : 0;

        // A frozen object's base size never approaches the address range; reject nonsense
        // so a stray pointer does not drive a huge bogus step.
        if (baseSize is 0 or > (1 << 24)) return false;

        cache[methodTable] = (baseSize, componentSize);
        return true;
    }

    private static ulong ReadPointer(ReadOnlySpan<byte> bytes, int offset, int pointerSize) =>
        pointerSize == 8
            ? BinaryPrimitives.ReadUInt64LittleEndian(bytes[offset..])
            : BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);

    private static int Align(int value, int alignment) => (value + alignment - 1) & ~(alignment - 1);
}
