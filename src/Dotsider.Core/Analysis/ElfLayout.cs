using System.Buffers.Binary;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Holds validated ELF dynamic-table ranges and precomputed version attribution.
/// </summary>
internal readonly struct ElfLayout
{
    private const int DynamicEntrySize = 16;
    private const int DynamicSymbolSize = 24;
    private const int MaxVersionAuxiliaryRecords = 65_536;
    private const int MaxVersionRequirementRecords = 4_096;
    private const int VersionAuxiliarySize = 16;
    private const int VersionRequirementSize = 16;

    private const uint ShtDynamic = 6;
    private const uint ShtDynSym = 11;
    private const uint ShtGnuVersionNeed = 0x6FFF_FFFE;
    private const uint ShtGnuVersionSymbol = 0x6FFF_FFFF;
    private const uint ShtStringTable = 3;

    private readonly Dictionary<ushort, string>? _versionLibraries;
    private readonly int _versionOffset;
    private readonly int _versionSize;

    private ElfLayout(
        (int Offset, int Size) dynamicSymbols,
        (int Offset, int Size) dynamicStrings,
        int versionOffset,
        int versionSize,
        Dictionary<ushort, string>? versionLibraries,
        List<string> needed)
    {
        DynSym = dynamicSymbols;
        DynStr = dynamicStrings;
        _versionOffset = versionOffset;
        _versionSize = versionSize;
        _versionLibraries = versionLibraries;
        Needed = needed;
    }

    /// <summary>Gets the validated dynamic-string-table range.</summary>
    internal (int Offset, int Size) DynStr { get; }

    /// <summary>Gets the validated dynamic-symbol-table range.</summary>
    internal (int Offset, int Size) DynSym { get; }

    /// <summary>Gets the safely decoded needed-library names.</summary>
    internal List<string> Needed { get; }

    /// <summary>
    /// Parses the section headers, dynamic dependencies, and GNU version requirements.
    /// Invalid version metadata is discarded without invalidating otherwise readable imports.
    /// </summary>
    /// <param name="bytes">The complete ELF image.</param>
    /// <returns>The validated layout, or null when the required dynamic tables are invalid.</returns>
    internal static ElfLayout? Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 64 || bytes[5] != 1)
            return null;

        var sections = ElfImageReader.ReadSections(bytes);
        var dynamicSymbolIndex = FindSection(sections, ".dynsym");
        var dynamicStringIndex = FindSection(sections, ".dynstr");
        if (dynamicSymbolIndex < 0 || dynamicStringIndex < 0)
            return null;

        var dynamicSymbols = sections[dynamicSymbolIndex];
        var dynamicStrings = sections[dynamicStringIndex];
        if (dynamicSymbols.Type != ShtDynSym
            || dynamicSymbols.Link != (uint)dynamicStringIndex
            || dynamicStrings.Type != ShtStringTable
            || dynamicSymbols.Size % DynamicSymbolSize != 0
            || !ElfImageReader.TryGetFileRange(
                dynamicSymbols.FileOffset,
                dynamicSymbols.Size,
                bytes.Length)
            || !ElfImageReader.TryGetFileRange(
                dynamicStrings.FileOffset,
                dynamicStrings.Size,
                bytes.Length))
        {
            return null;
        }

        var needed = ReadNeeded(bytes, sections, dynamicStrings);
        var versionSymbolIndex = FindSection(sections, ".gnu.version");
        var versionNeedIndex = FindSection(sections, ".gnu.version_r");
        if (!TryReadVersionRequirements(
            bytes,
            sections,
            dynamicSymbolIndex,
            dynamicStringIndex,
            versionSymbolIndex,
            versionNeedIndex,
            dynamicSymbols,
            dynamicStrings,
            needed,
            out var versionOffset,
            out var versionSize,
            out var versionLibraries))
        {
            versionOffset = 0;
            versionSize = 0;
            versionLibraries = null;
        }

        return new ElfLayout(
            (dynamicSymbols.FileOffset, dynamicSymbols.Size),
            (dynamicStrings.FileOffset, dynamicStrings.Size),
            versionOffset,
            versionSize,
            versionLibraries,
            needed);
    }

    /// <summary>
    /// Maps a dynamic-symbol index to its required library using the precomputed GNU
    /// version-requirement map.
    /// </summary>
    /// <param name="bytes">The complete ELF image.</param>
    /// <param name="symbolIndex">The dynamic-symbol-table index.</param>
    /// <returns>The required library name, or null when no safe attribution is available.</returns>
    internal string? ResolveVersionLibrary(ReadOnlySpan<byte> bytes, int symbolIndex)
    {
        if (_versionLibraries is null || symbolIndex < 0)
            return null;

        var relativeOffset = (long)symbolIndex * sizeof(ushort);
        if (relativeOffset > _versionSize - sizeof(ushort))
            return null;

        var versionIndex = (ushort)(BinaryPrimitives.ReadUInt16LittleEndian(
            bytes[(_versionOffset + (int)relativeOffset)..]) & 0x7FFF);
        return versionIndex >= 2
            && _versionLibraries.TryGetValue(versionIndex, out var library)
                ? library
                : null;
    }

    private static int FindSection(IReadOnlyList<ElfSection> sections, string name)
    {
        for (var i = 0; i < sections.Count; i++)
        {
            if (sections[i].Name == name)
                return i;
        }

        return -1;
    }

    private static List<string> ReadNeeded(
        ReadOnlySpan<byte> bytes,
        IReadOnlyList<ElfSection> sections,
        ElfSection dynamicStrings)
    {
        var needed = new List<string>();
        for (var i = 0; i < sections.Count; i++)
        {
            var section = sections[i];
            if (section.Type != ShtDynamic)
                continue;

            if (!ElfImageReader.TryGetFileRange(
                section.FileOffset,
                section.Size,
                bytes.Length))
            {
                return needed;
            }

            var data = bytes.Slice(section.FileOffset, section.Size);
            for (var position = 0; position <= data.Length - DynamicEntrySize;
                position += DynamicEntrySize)
            {
                var entry = data[position..];
                var tag = (long)BinaryPrimitives.ReadUInt64LittleEndian(entry);
                var value = BinaryPrimitives.ReadUInt64LittleEndian(entry[8..]);
                if (tag == 0)
                    break;

                if (tag != 1 || value > uint.MaxValue)
                    continue;

                var name = ElfImageReader.ReadBoundedString(
                    bytes,
                    dynamicStrings.FileOffset,
                    dynamicStrings.Size,
                    (uint)value);
                if (!string.IsNullOrEmpty(name))
                    needed.Add(name);
            }

            break;
        }

        return needed;
    }

    private static bool TryAdvance(
        int current,
        uint relativeOffset,
        int recordSize,
        int limit,
        out int next)
    {
        next = 0;
        if (relativeOffset < recordSize || (relativeOffset & 3) != 0)
            return false;

        var candidate = (long)current + relativeOffset;
        if (candidate <= current || candidate > limit - recordSize)
            return false;

        next = (int)candidate;
        return true;
    }

    private static bool TryReadVersionRequirements(
        ReadOnlySpan<byte> bytes,
        IReadOnlyList<ElfSection> sections,
        int dynamicSymbolIndex,
        int dynamicStringIndex,
        int versionSymbolIndex,
        int versionNeedIndex,
        ElfSection dynamicSymbols,
        ElfSection dynamicStrings,
        List<string> needed,
        out int versionOffset,
        out int versionSize,
        out Dictionary<ushort, string>? versionLibraries)
    {
        versionOffset = 0;
        versionSize = 0;
        versionLibraries = null;

        if (versionSymbolIndex < 0 && versionNeedIndex < 0)
            return true;
        if (versionSymbolIndex < 0 || versionNeedIndex < 0)
            return false;

        var versionSymbols = sections[versionSymbolIndex];
        var versionNeeds = sections[versionNeedIndex];
        var dynamicSymbolCount = dynamicSymbols.Size / DynamicSymbolSize;
        if (versionSymbols.Type != ShtGnuVersionSymbol
            || versionSymbols.Link != (uint)dynamicSymbolIndex
            || versionNeeds.Type != ShtGnuVersionNeed
            || versionNeeds.Link != (uint)dynamicStringIndex
            || versionSymbols.Size != dynamicSymbolCount * sizeof(ushort)
            || !ElfImageReader.TryGetFileRange(
                versionSymbols.FileOffset,
                versionSymbols.Size,
                bytes.Length)
            || !ElfImageReader.TryGetFileRange(
                versionNeeds.FileOffset,
                versionNeeds.Size,
                bytes.Length)
            || versionNeeds.Info > MaxVersionRequirementRecords
            || versionNeeds.Info > (uint)(versionNeeds.Size / VersionRequirementSize))
        {
            return false;
        }

        if (versionNeeds.Info == 0)
        {
            if (versionNeeds.Size != 0)
                return false;

            versionOffset = versionSymbols.FileOffset;
            versionSize = versionSymbols.Size;
            return true;
        }

        var neededSet = new HashSet<string>(needed, StringComparer.Ordinal);
        if (neededSet.Count == 0)
            return false;

        var data = bytes.Slice(versionNeeds.FileOffset, versionNeeds.Size);
        Dictionary<ushort, string>? parsed = null;
        var needPosition = 0;
        var auxiliaryRecords = 0;
        for (var needIndex = 0; needIndex < versionNeeds.Info; needIndex++)
        {
            if (needPosition > data.Length - VersionRequirementSize)
                return false;

            var requirement = data[needPosition..];
            var formatVersion = BinaryPrimitives.ReadUInt16LittleEndian(requirement);
            var auxiliaryCount = BinaryPrimitives.ReadUInt16LittleEndian(requirement[2..]);
            var fileNameOffset = BinaryPrimitives.ReadUInt32LittleEndian(requirement[4..]);
            var auxiliaryOffset = BinaryPrimitives.ReadUInt32LittleEndian(requirement[8..]);
            var nextRequirementOffset = BinaryPrimitives.ReadUInt32LittleEndian(requirement[12..]);
            if (formatVersion != 1
                || auxiliaryCount == 0
                || auxiliaryRecords > MaxVersionAuxiliaryRecords - auxiliaryCount)
            {
                return false;
            }

            var library = ElfImageReader.ReadBoundedString(
                bytes,
                dynamicStrings.FileOffset,
                dynamicStrings.Size,
                fileNameOffset);
            if (string.IsNullOrEmpty(library) || !neededSet.Contains(library))
                return false;

            var lastRequirement = needIndex == versionNeeds.Info - 1;
            var requirementLimit = data.Length;
            var nextRequirement = 0;
            if (lastRequirement)
            {
                if (nextRequirementOffset != 0)
                    return false;
            }
            else if (!TryAdvance(
                needPosition,
                nextRequirementOffset,
                VersionRequirementSize,
                data.Length,
                out nextRequirement))
            {
                return false;
            }
            else
            {
                requirementLimit = nextRequirement;
            }

            if (!TryAdvance(
                needPosition,
                auxiliaryOffset,
                VersionAuxiliarySize,
                requirementLimit,
                out var auxiliaryPosition)
                || auxiliaryCount > (requirementLimit - auxiliaryPosition) / VersionAuxiliarySize)
            {
                return false;
            }

            auxiliaryRecords += auxiliaryCount;
            for (var auxiliaryIndex = 0; auxiliaryIndex < auxiliaryCount; auxiliaryIndex++)
            {
                if (auxiliaryPosition > requirementLimit - VersionAuxiliarySize)
                    return false;

                var auxiliary = data[auxiliaryPosition..];
                var rawVersionIndex = BinaryPrimitives.ReadUInt16LittleEndian(auxiliary[6..]);
                var nameOffset = BinaryPrimitives.ReadUInt32LittleEndian(auxiliary[8..]);
                var nextAuxiliaryOffset = BinaryPrimitives.ReadUInt32LittleEndian(auxiliary[12..]);
                var versionIndex = (ushort)(rawVersionIndex & 0x7FFF);
                if (versionIndex < 2
                    || string.IsNullOrEmpty(ElfImageReader.ReadBoundedString(
                        bytes,
                        dynamicStrings.FileOffset,
                        dynamicStrings.Size,
                        nameOffset)))
                {
                    return false;
                }

                parsed ??= [];
                if (parsed.TryGetValue(versionIndex, out var existingLibrary))
                {
                    if (!string.Equals(existingLibrary, library, StringComparison.Ordinal))
                        return false;
                }
                else
                {
                    parsed.Add(versionIndex, library);
                }

                var lastAuxiliary = auxiliaryIndex == auxiliaryCount - 1;
                if (lastAuxiliary)
                {
                    if (nextAuxiliaryOffset != 0)
                        return false;
                }
                else if (!TryAdvance(
                    auxiliaryPosition,
                    nextAuxiliaryOffset,
                    VersionAuxiliarySize,
                    requirementLimit,
                    out auxiliaryPosition))
                {
                    return false;
                }
            }

            if (!lastRequirement)
                needPosition = nextRequirement;
        }

        versionOffset = versionSymbols.FileOffset;
        versionSize = versionSymbols.Size;
        versionLibraries = parsed;
        return true;
    }
}
