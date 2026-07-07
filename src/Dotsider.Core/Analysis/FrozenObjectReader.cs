using Dotsider.Core.Analysis.Models;
using System.Buffers.Binary;
using System.Text;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Recovers frozen <see cref="string"/> literals from a Native AOT binary's frozen object
/// region (ReadyToRun section 206). Each frozen string is a self-describing object — a
/// 32-bit length followed by that many UTF-16 code units and a null terminator — so the
/// region is scanned for that shape rather than walked by object pointers, which sidesteps
/// the platform-specific MethodTable pointer encodings. On Windows and macOS the region is
/// file-backed and scanned in place; on Linux (and zero-fill Mach-O layouts) it is filled
/// at startup from the dehydrated data, which is rehydrated first.
/// </summary>
internal static class FrozenObjectReader
{
    private const int MaxStringLength = 1 << 20;

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
        RtrSection? frozenRegion = null;
        RtrSection? dehydrated = null;
        foreach (var section in sections)
        {
            if (section.SectionId == ReadyToRunReader.FrozenObjectRegion) frozenRegion = section;
            else if (section.SectionId == ReadyToRunReader.DehydratedData) dehydrated = section;
        }

        // A dehydrated data section means the frozen region is filled at startup rather than
        // stored on disk (ELF, and zero-fill Mach-O layouts); rehydrate it and scan the
        // result. Its presence is the definitive signal, so it takes precedence.
        if (dehydrated is { } dehydratedSection && frozenRegion is { } targetRegion)
        {
            var rebuilt = DehydratedDataReader.Rehydrate(bytes, dehydratedSection, targetRegion, addressSpace);
            if (rebuilt is not null)
                return ScanStrings(rebuilt, regionBase: 0);
        }

        // Otherwise the region is file-backed (Windows); scan it in place.
        if (frozenRegion is { } frozen && ReadyToRunReader.FileRange(frozen) is { } fileRange)
        {
            var (start, length) = fileRange;
            var end = Math.Min(start + length, bytes.Length);
            if (end > start)
                return ScanStrings(bytes[start..end], start);
        }

        return [];
    }

    /// <summary>
    /// Scans a frozen object region for length-prefixed UTF-16 string literals: a 32-bit
    /// length, that many code units, and a null terminator. Offsets are reported relative to
    /// <paramref name="regionBase"/>.
    /// </summary>
    private static List<StringEntry> ScanStrings(ReadOnlySpan<byte> region, int regionBase)
    {
        var results = new List<StringEntry>();
        var pos = 0;
        while (pos + 6 <= region.Length)
        {
            var length = BinaryPrimitives.ReadInt32LittleEndian(region[pos..]);
            if (length is < 1 or > MaxStringLength)
            {
                pos += 1;
                continue;
            }

            var charsStart = pos + 4;
            var terminator = charsStart + length * 2;
            if (terminator + 2 > region.Length
                || BinaryPrimitives.ReadUInt16LittleEndian(region[terminator..]) != 0
                || !IsFrozenString(region.Slice(charsStart, length * 2)))
            {
                pos += 1;
                continue;
            }

            var value = Encoding.Unicode.GetString(region.Slice(charsStart, length * 2));
            results.Add(new StringEntry(regionBase + charsStart, value, StringSource.FrozenObject));
            pos = terminator + 2;
        }

        return results;
    }

    /// <summary>
    /// Returns true when the code units look like a real string literal: every unit is
    /// printable or common whitespace, none is a NUL.
    /// </summary>
    private static bool IsFrozenString(ReadOnlySpan<byte> chars)
    {
        for (var i = 0; i + 1 < chars.Length; i += 2)
        {
            var c = (char)(chars[i] | (chars[i + 1] << 8));
            if (c == '\0') return false;
            if (c < 0x20 && c is not ('\t' or '\n' or '\r')) return false;
        }

        return true;
    }
}
