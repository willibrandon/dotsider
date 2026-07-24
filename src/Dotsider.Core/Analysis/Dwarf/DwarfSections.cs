namespace Dotsider.Core.Analysis.Dwarf;

/// <summary>
/// The raw bytes of the DWARF sections a function walk needs. Sections absent from the image are
/// empty arrays — the form decoders then resolve their indirections to null rather than failing —
/// so ELF <c>.debug_*</c> sections and Mach-O <c>__DWARF</c> sections feed the same reader.
/// </summary>
/// <param name="Info"><c>.debug_info</c> — the DIE tree.</param>
/// <param name="Abbrev"><c>.debug_abbrev</c> — abbreviation declarations.</param>
/// <param name="Str"><c>.debug_str</c> — strings referenced by <c>strp</c>.</param>
/// <param name="LineStr"><c>.debug_line_str</c> — strings referenced by <c>line_strp</c>.</param>
/// <param name="StrOffsets"><c>.debug_str_offsets</c> — the v5 string-index table behind <c>strx</c>.</param>
/// <param name="Addr"><c>.debug_addr</c> — the v5 address-index table behind <c>addrx</c>.</param>
/// <param name="Line"><c>.debug_line</c> — line-number programs.</param>
/// <param name="Ranges"><c>.debug_ranges</c> — v4 address range lists.</param>
/// <param name="RngLists"><c>.debug_rnglists</c> — v5 range lists.</param>
internal sealed record DwarfSections(
    byte[] Info,
    byte[] Abbrev,
    byte[] Str,
    byte[] LineStr,
    byte[] StrOffsets,
    byte[] Addr,
    byte[] Line,
    byte[] Ranges,
    byte[] RngLists)
{
    /// <summary>
    /// Collects the DWARF sections through a name lookup, tolerating absent sections. Names are
    /// passed without their platform prefix; the lookup applies <c>.debug_*</c> (ELF) or
    /// <c>__debug_*</c> (Mach-O) itself.
    /// </summary>
    /// <param name="lookup">
    /// Resolves a DWARF section's bytes by base name (e.g. <c>info</c>) and remaining budget,
    /// or null.
    /// </param>
    /// <param name="maximumTotalBytes">The maximum total bytes returned across all sections.</param>
    public static DwarfSections Collect(
        Func<string, int, byte[]?> lookup,
        int maximumTotalBytes)
    {
        ArgumentNullException.ThrowIfNull(lookup);
        ArgumentOutOfRangeException.ThrowIfNegative(maximumTotalBytes);

        var remainingBytes = maximumTotalBytes;

        byte[] Read(string name)
        {
            if (remainingBytes == 0)
                return [];

            byte[]? section = lookup(name, remainingBytes);
            if (section is null || section.Length > remainingBytes)
                return [];

            remainingBytes -= section.Length;
            return section;
        }

        byte[] info = Read("info");
        if (info.Length == 0)
            return new DwarfSections([], [], [], [], [], [], [], [], []);

        byte[] abbrev = Read("abbrev");
        if (abbrev.Length == 0)
            return new DwarfSections(info, [], [], [], [], [], [], [], []);

        return new DwarfSections(
            info,
            abbrev,
            Read("str"),
            Read("line_str"),
            Read("str_offsets"),
            Read("addr"),
            Read("line"),
            Read("ranges"),
            Read("rnglists"));
    }

    /// <summary>Whether the image carries any DIE data at all.</summary>
    public bool HasInfo => Info.Length > 0 && Abbrev.Length > 0;
}
