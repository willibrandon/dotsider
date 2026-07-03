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
    /// <param name="lookup">Resolves a DWARF section's bytes by base name (e.g. <c>info</c>), or null.</param>
    public static DwarfSections Collect(Func<string, byte[]?> lookup) => new(
        lookup("info") ?? [],
        lookup("abbrev") ?? [],
        lookup("str") ?? [],
        lookup("line_str") ?? [],
        lookup("str_offsets") ?? [],
        lookup("addr") ?? [],
        lookup("line") ?? [],
        lookup("ranges") ?? [],
        lookup("rnglists") ?? []);

    /// <summary>Whether the image carries any DIE data at all.</summary>
    public bool HasInfo => Info.Length > 0 && Abbrev.Length > 0;
}
