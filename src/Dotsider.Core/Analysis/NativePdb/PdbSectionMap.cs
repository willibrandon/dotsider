using System.Buffers.Binary;

namespace Dotsider.Core.Analysis.NativePdb;

/// <summary>
/// Maps a CodeView <c>(segment, offset)</c> address to a PE relative virtual address and the
/// containing section's name. The mapping comes from the section-header dump stream the DBI's
/// optional debug header points at; when that stream is absent it is rebuilt from the PE image's
/// own section headers, which carry the same virtual addresses.
/// </summary>
internal sealed class PdbSectionMap
{
    private readonly (uint VirtualAddress, uint VirtualSize, uint Characteristics, string Name)[] _sections;

    private PdbSectionMap((uint, uint, uint, string)[] sections) => _sections = sections;

    /// <summary>
    /// Builds a section map from a 40-byte-per-entry <c>IMAGE_SECTION_HEADER</c> block — the
    /// layout of both the PDB section-header dump and the PE section table.
    /// </summary>
    /// <param name="headers">The section-header bytes.</param>
    public static PdbSectionMap FromSectionHeaders(ReadOnlySpan<byte> headers)
    {
        const int entrySize = 40;
        var count = headers.Length / entrySize;
        var sections = new (uint, uint, uint, string)[count];
        for (var i = 0; i < count; i++)
        {
            var e = headers.Slice(i * entrySize, entrySize);
            var name = System.Text.Encoding.ASCII.GetString(e[..8]).TrimEnd('\0');
            var virtualSize = BinaryPrimitives.ReadUInt32LittleEndian(e[8..]);
            var virtualAddress = BinaryPrimitives.ReadUInt32LittleEndian(e[12..]);
            var characteristics = BinaryPrimitives.ReadUInt32LittleEndian(e[36..]);
            sections[i] = (virtualAddress, virtualSize, characteristics, name);
        }

        return new PdbSectionMap(sections);
    }

    /// <summary>The number of sections in the map.</summary>
    public int Count => _sections.Length;

    /// <summary>
    /// Resolves a one-based CodeView segment and offset to an RVA, or null when the segment is
    /// out of range.
    /// </summary>
    /// <param name="segment">The one-based section index.</param>
    /// <param name="offset">The offset within the section.</param>
    public uint? ToRva(int segment, uint offset)
    {
        if (segment < 1 || segment > _sections.Length) return null;
        return _sections[segment - 1].VirtualAddress + offset;
    }

    /// <summary>The name of a one-based segment, or null when out of range.</summary>
    /// <param name="segment">The one-based section index.</param>
    public string? SectionName(int segment) =>
        segment >= 1 && segment <= _sections.Length ? _sections[segment - 1].Name : null;

    /// <summary>Whether the given one-based segment is executable (IMAGE_SCN_MEM_EXECUTE / CNT_CODE).</summary>
    /// <param name="segment">The one-based section index.</param>
    public bool IsExecutable(int segment)
    {
        if (segment < 1 || segment > _sections.Length) return false;
        // IMAGE_SCN_CNT_CODE (0x20) | IMAGE_SCN_MEM_EXECUTE (0x20000000)
        return (_sections[segment - 1].Characteristics & 0x2000_0020) != 0;
    }

    /// <summary>The end RVA of a one-based segment (address + virtual size), for sizing by containment.</summary>
    /// <param name="segment">The one-based section index.</param>
    public uint SectionEndRva(int segment) =>
        segment >= 1 && segment <= _sections.Length
            ? _sections[segment - 1].VirtualAddress + _sections[segment - 1].VirtualSize
            : 0;
}
