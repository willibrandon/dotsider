using Dotsider.Core.Analysis;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="ElfImageReader.ReadSections"/> and
/// <see cref="ElfImageReader.TryGetSection"/> — the section-header walk that hands DWARF,
/// symbol-table, and build-id readers their bytes — driven with synthetic ELF images.
/// </summary>
public class ElfSectionTests
{
    /// <summary>
    /// Verifies the walk returns every named section with its address, file offset, and size,
    /// alongside the null section and <c>.shstrtab</c> the builder always emits.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ReadSections_ReturnsNamedSectionsWithLocation()
    {
        var image = SyntheticImageBuilders.BuildElf(
            (".text", 0x401000, new byte[] { 1, 2, 3, 4 }),
            (".debug_info", 0, "\t\t"u8.ToArray()));

        var sections = ElfImageReader.ReadSections(image);

        Assert.Equal(4, sections.Count); // null + .text + .debug_info + .shstrtab
        var text = Assert.Single(sections, s => s.Name == ".text");
        Assert.Equal(0x401000UL, text.Address);
        Assert.Equal(4, text.Size);
        Assert.Equal(1, image[text.FileOffset]);

        var info = Assert.Single(sections, s => s.Name == ".debug_info");
        Assert.Equal(2, info.Size);
        Assert.Equal(9, image[info.FileOffset]);
    }

    /// <summary>Verifies the name lookup finds a present section and reports an absent one.</summary>
    [Fact(Timeout = 30_000)]
    public void TryGetSection_FindsPresentAndReportsAbsent()
    {
        var image = SyntheticImageBuilders.BuildElf((".debug_str", 0, new byte[] { 0, (byte)'a', 0 }));

        Assert.True(ElfImageReader.TryGetSection(image, ".debug_str", out var section));
        Assert.Equal(3, section.Size);
        Assert.False(ElfImageReader.TryGetSection(image, ".debug_line", out _));
    }

    /// <summary>Verifies non-ELF bytes and truncated images yield no sections rather than throwing.</summary>
    [Fact(Timeout = 30_000)]
    public void ReadSections_RejectsNonElfAndTruncated()
    {
        Assert.Empty(ElfImageReader.ReadSections([0x4D, 0x5A, 0, 0]));

        var truncated = SyntheticImageBuilders.BuildElf((".text", 0, new byte[] { 1 }))[..70];
        Assert.Empty(ElfImageReader.ReadSections(truncated));
    }

    /// <summary>
    /// Verifies section content materializes through <c>SHF_COMPRESSED</c>: a zlib payload
    /// inflates to the original bytes, plain sections pass through, and unsupported or
    /// malformed compression reads as absent rather than as garbage.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ReadSectionBytes_InflatesCompressedSections()
    {
        byte[] payload = [.. Enumerable.Range(0, 512).Select(i => (byte)(i * 7))];
        var image = SyntheticImageBuilders.BuildElf(
            (".debug_info", 0, SyntheticImageBuilders.CompressDebugSection(payload), 1u, 0u, 0x800UL),
            (".debug_abbrev", 0, payload, 1u, 0u, 0UL));
        var sections = ElfImageReader.ReadSections(image);

        var compressed = sections.Single(s => s.Name == ".debug_info");
        Assert.Equal(payload, ElfImageReader.ReadSectionBytes(image, compressed));

        var plain = sections.Single(s => s.Name == ".debug_abbrev");
        Assert.Equal(payload, ElfImageReader.ReadSectionBytes(image, plain));

        // Unsupported compression type (zstd = 2): absent, not misread.
        var zstd = SyntheticImageBuilders.CompressDebugSection(payload);
        zstd[0] = 2;
        var zstdImage = SyntheticImageBuilders.BuildElf((".debug_info", 0, zstd, 1u, 0u, 0x800UL));
        Assert.Null(ElfImageReader.ReadSectionBytes(
            zstdImage, ElfImageReader.ReadSections(zstdImage).Single(s => s.Name == ".debug_info")));

        // Truncated header: absent.
        var tiny = SyntheticImageBuilders.BuildElf((".debug_info", 0, new byte[8], 1u, 0u, 0x800UL));
        Assert.Null(ElfImageReader.ReadSectionBytes(
            tiny, ElfImageReader.ReadSections(tiny).Single(s => s.Name == ".debug_info")));
    }
}
