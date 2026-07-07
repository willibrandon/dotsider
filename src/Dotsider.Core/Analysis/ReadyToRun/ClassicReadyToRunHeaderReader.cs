using Dotsider.Core.Analysis.Models;
using System.Buffers.Binary;

namespace Dotsider.Core.Analysis.ReadyToRun;

/// <summary>
/// Parses a crossgen2 image's <c>READYTORUN_HEADER</c> / <c>READYTORUN_CORE_HEADER</c> and its
/// 12-byte <c>{Type, RVA, Size}</c> section table. The full header (single-file / composite global)
/// carries a signature and version prefix; a component's per-assembly core header carries only the
/// core (flags + section table).
/// </summary>
internal static class ClassicReadyToRunHeaderReader
{
    /// <summary>The <c>READYTORUN_SIGNATURE</c> dword — the ASCII bytes <c>'R','T','R','\0'</c>.</summary>
    internal const uint Signature = 0x0052_5452;

    /// <summary>The parsed prefix of a full <c>READYTORUN_HEADER</c>.</summary>
    internal readonly record struct FullHeader(
        uint Signature, int MajorVersion, int MinorVersion, uint Flags, int SectionCount,
        IReadOnlyList<ReadyToRunSectionEntry> Sections);

    /// <summary>The parsed prefix of a <c>READYTORUN_CORE_HEADER</c> (no signature/version).</summary>
    internal readonly record struct CoreHeader(
        uint Flags, int SectionCount, IReadOnlyList<ReadyToRunSectionEntry> Sections);

    /// <summary>
    /// Reads a full header at <paramref name="fileOffset"/>: signature (u32), major/minor (u16),
    /// then a core header. Returns null when the bytes do not fit.
    /// </summary>
    internal static FullHeader? ReadFullHeader(
        ReadOnlySpan<byte> raw, int fileOffset, ulong imageBase, NativeAddressSpace? addressSpace)
    {
        if (fileOffset < 0 || fileOffset + 16 > raw.Length) return null;
        var signature = BinaryPrimitives.ReadUInt32LittleEndian(raw[fileOffset..]);
        var major = BinaryPrimitives.ReadUInt16LittleEndian(raw[(fileOffset + 4)..]);
        var minor = BinaryPrimitives.ReadUInt16LittleEndian(raw[(fileOffset + 6)..]);
        var core = ReadCoreHeader(raw, fileOffset + 8, imageBase, addressSpace);
        if (core is not { } c) return null;
        return new FullHeader(signature, major, minor, c.Flags, c.SectionCount, c.Sections);
    }

    /// <summary>
    /// Reads a core header at <paramref name="fileOffset"/>: flags (u32), section count (u32), then
    /// the 12-byte section rows. Returns null when the fixed prefix does not fit; a truncated
    /// section table simply yields the rows that fit.
    /// </summary>
    internal static CoreHeader? ReadCoreHeader(
        ReadOnlySpan<byte> raw, int fileOffset, ulong imageBase, NativeAddressSpace? addressSpace)
    {
        if (fileOffset < 0 || fileOffset + 8 > raw.Length) return null;
        var flags = BinaryPrimitives.ReadUInt32LittleEndian(raw[fileOffset..]);
        var count = BinaryPrimitives.ReadInt32LittleEndian(raw[(fileOffset + 4)..]);
        if (count is < 0 or > 4096) return null; // malformed count guard

        var sections = new List<ReadyToRunSectionEntry>(count);
        var rowsStart = fileOffset + 8;
        for (var i = 0; i < count; i++)
        {
            var row = rowsStart + i * 12;
            if (row + 12 > raw.Length) break;
            var type = BinaryPrimitives.ReadInt32LittleEndian(raw[row..]);
            var rva = BinaryPrimitives.ReadInt32LittleEndian(raw[(row + 4)..]);
            var size = BinaryPrimitives.ReadInt32LittleEndian(raw[(row + 8)..]);
            int? offset = addressSpace is not null
                && addressSpace.TryGetFileOffset(imageBase + (uint)rva, out var fo, out _)
                ? fo
                : null;
            sections.Add(new ReadyToRunSectionEntry(type, SectionName(type), rva, size, offset));
        }

        return new CoreHeader(flags, count, sections);
    }

    /// <summary>Finds a section by its <see cref="ReadyToRunSectionType"/> id.</summary>
    internal static ReadyToRunSectionEntry? Section(
        IReadOnlyList<ReadyToRunSectionEntry> sections, ReadyToRunSectionType type)
    {
        foreach (var s in sections)
            if (s.Type == (int)type)
                return s;
        return null;
    }

    private static string SectionName(int type) =>
        Enum.IsDefined(typeof(ReadyToRunSectionType), type)
            ? ((ReadyToRunSectionType)type).ToString()
            : $"Section {type}";
}
