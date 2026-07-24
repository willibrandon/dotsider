using Dotsider.Core.Analysis;
using System.Buffers.Binary;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="ElfImageReader.ReadSections"/> and
/// <see cref="ElfImageReader.TryGetSection"/> — the section-header walk that hands DWARF,
/// symbol-table, and build-id readers their bytes — driven with synthetic ELF images.
/// </summary>
[TestClass]
public sealed class ElfSectionTests
{
    /// <summary>
    /// Verifies the walk returns every named section with its address, file offset, and size,
    /// alongside the null section and <c>.shstrtab</c> the builder always emits.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadSections_ReturnsNamedSectionsWithLocation()
    {
        var image = SyntheticImageBuilders.BuildElf(
            (".text", 0x401000, new byte[] { 1, 2, 3, 4 }),
            (".debug_info", 0, "\t\t"u8.ToArray()));

        var sections = ElfImageReader.ReadSections(image);

        Assert.HasCount(4, sections); // null + .text + .debug_info + .shstrtab
        var text = Assert.ContainsSingle(s => s.Name == ".text", sections);
        Assert.AreEqual(0x401000UL, text.Address);
        Assert.AreEqual(4, text.Size);
        Assert.AreEqual(1, image[text.FileOffset]);

        var info = Assert.ContainsSingle(s => s.Name == ".debug_info", sections);
        Assert.AreEqual(2, info.Size);
        Assert.AreEqual(9, image[info.FileOffset]);
    }

    /// <summary>Verifies the name lookup finds a present section and reports an absent one.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TryGetSection_FindsPresentAndReportsAbsent()
    {
        var image = SyntheticImageBuilders.BuildElf((".debug_str", 0, new byte[] { 0, (byte)'a', 0 }));

        Assert.IsTrue(ElfImageReader.TryGetSection(image, ".debug_str", out var section));
        Assert.AreEqual(3, section.Size);
        Assert.IsFalse(ElfImageReader.TryGetSection(image, ".debug_line", out _));
    }

    /// <summary>Verifies non-ELF bytes and truncated images yield no sections rather than throwing.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadSections_RejectsNonElfAndTruncated()
    {
        Assert.IsEmpty(ElfImageReader.ReadSections([0x4D, 0x5A, 0, 0]));

        var truncated = SyntheticImageBuilders.BuildElf((".text", 0, new byte[] { 1 }))[..70];
        Assert.IsEmpty(ElfImageReader.ReadSections(truncated));
    }

    /// <summary>
    /// Verifies section content materializes through <c>SHF_COMPRESSED</c>: a zlib payload
    /// inflates to the original bytes, plain sections pass through, and unsupported or
    /// malformed compression reads as absent rather than as garbage.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadSectionBytes_InflatesCompressedSections()
    {
        byte[] payload = [.. Enumerable.Range(0, 512).Select(i => (byte)(i * 7))];
        var image = SyntheticImageBuilders.BuildElf(
            (".debug_info", 0, SyntheticImageBuilders.CompressDebugSection(payload), 1u, 0u, 0x800UL),
            (".debug_abbrev", 0, payload, 1u, 0u, 0UL));
        var sections = ElfImageReader.ReadSections(image);

        var compressed = sections.Single(s => s.Name == ".debug_info");
        Assert.AreSequenceEqual(payload, ElfImageReader.ReadSectionBytes(
            image, compressed, NativeImageDataLimits.MaxMaterializedBytes));

        var plain = sections.Single(s => s.Name == ".debug_abbrev");
        Assert.AreSequenceEqual(payload, ElfImageReader.ReadSectionBytes(
            image, plain, NativeImageDataLimits.MaxMaterializedBytes));

        // Unsupported compression type (zstd = 2): absent, not misread.
        var zstd = SyntheticImageBuilders.CompressDebugSection(payload);
        zstd[0] = 2;
        var zstdImage = SyntheticImageBuilders.BuildElf((".debug_info", 0, zstd, 1u, 0u, 0x800UL));
        Assert.IsNull(ElfImageReader.ReadSectionBytes(
            zstdImage,
            ElfImageReader.ReadSections(zstdImage).Single(s => s.Name == ".debug_info"),
            NativeImageDataLimits.MaxMaterializedBytes));

        // Truncated header: absent.
        var tiny = SyntheticImageBuilders.BuildElf((".debug_info", 0, new byte[8], 1u, 0u, 0x800UL));
        Assert.IsNull(ElfImageReader.ReadSectionBytes(
            tiny,
            ElfImageReader.ReadSections(tiny).Single(s => s.Name == ".debug_info"),
            NativeImageDataLimits.MaxMaterializedBytes));
    }

    /// <summary>
    /// Verifies hostile declared output sizes are rejected before decompression or output allocation.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadSectionBytes_DeclaredLengthOutsideLimit_ReturnsNull()
    {
        ulong[] declaredLengths =
        [
            (ulong)NativeImageDataLimits.MaxMaterializedBytes + 1,
            int.MaxValue,
            ulong.MaxValue,
        ];

        foreach (ulong declaredLength in declaredLengths)
        {
            byte[] section = SyntheticImageBuilders.CompressDebugSection([0x2A]);
            BinaryPrimitives.WriteUInt64LittleEndian(section.AsSpan(8), declaredLength);
            byte[] image = BuildCompressedElf(section);

            Assert.IsNull(ReadDebugInfo(image));
        }
    }

    /// <summary>
    /// Verifies the ratio guard accepts a legitimate highly-compressible zlib stream but rejects
    /// a declaration beyond the format's configured expansion ceiling.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadSectionBytes_CompressionRatioBoundary_FailsClosed()
    {
        byte[] highlyCompressible = new byte[1024 * 1024];
        byte[] validImage = BuildCompressedElf(
            SyntheticImageBuilders.CompressDebugSection(highlyCompressible));

        Assert.AreSequenceEqual(highlyCompressible, ReadDebugInfo(validImage));

        byte[] excessive = SyntheticImageBuilders.CompressDebugSection([0x2A]);
        ulong excessiveLength = (ulong)(excessive.Length - 24)
            * NativeImageDataLimits.MaxCompressionRatio + 1;
        BinaryPrimitives.WriteUInt64LittleEndian(excessive.AsSpan(8), excessiveLength);

        Assert.IsNull(ReadDebugInfo(BuildCompressedElf(excessive)));
    }

    /// <summary>
    /// Verifies the caller's remaining materialization budget applies identically to plain and
    /// compressed sections.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadSectionBytes_CallerBudget_AppliesToEverySection()
    {
        byte[] payload = [.. Enumerable.Range(0, 64).Select(static value => (byte)value)];
        byte[] image = SyntheticImageBuilders.BuildElf(
            (".debug_info", 0, SyntheticImageBuilders.CompressDebugSection(payload), 1u, 0u, 0x800UL),
            (".debug_abbrev", 0, payload, 1u, 0u, 0UL));
        var sections = ElfImageReader.ReadSections(image);
        var compressed = sections.Single(section => section.Name == ".debug_info");
        var plain = sections.Single(section => section.Name == ".debug_abbrev");

        Assert.IsNull(ElfImageReader.ReadSectionBytes(image, compressed, payload.Length - 1));
        Assert.IsNull(ElfImageReader.ReadSectionBytes(image, plain, payload.Length - 1));
        Assert.AreSequenceEqual(
            payload,
            ElfImageReader.ReadSectionBytes(image, compressed, payload.Length));
        Assert.AreSequenceEqual(
            payload,
            ElfImageReader.ReadSectionBytes(image, plain, payload.Length));
    }

    /// <summary>
    /// Verifies a compressed stream cannot exceed the caller's remaining output budget by more
    /// than the configured bounded format overhead.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadSectionBytes_CompressedPayloadOutsideLimit_ReturnsNull()
    {
        const int outputBudget = 1;
        byte[] section = new byte[
            24 + outputBudget + NativeImageDataLimits.MaxCompressedOverheadBytes + 1];
        BinaryPrimitives.WriteUInt32LittleEndian(section, 1);
        BinaryPrimitives.WriteUInt64LittleEndian(section.AsSpan(8), outputBudget);
        BinaryPrimitives.WriteUInt64LittleEndian(section.AsSpan(16), 1);
        byte[] image = BuildCompressedElf(section);

        Assert.IsNull(ElfImageReader.ReadSectionBytes(
            image,
            ElfImageReader.ReadSections(image).Single(item => item.Name == ".debug_info"),
            outputBudget));
    }

    /// <summary>
    /// Verifies reserved fields, invalid alignment, allocatable sections, and no-bits sections
    /// are rejected as illegal compressed-section grammars.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadSectionBytes_InvalidCompressionHeaderGrammar_ReturnsNull()
    {
        var cases = new[]
        {
            (Reserved: 1u, Alignment: 1UL, Type: 1u, Flags: 0x800UL),
            (Reserved: 0u, Alignment: 3UL, Type: 1u, Flags: 0x800UL),
            (Reserved: 0u, Alignment: 1UL, Type: 1u, Flags: 0x802UL),
            (Reserved: 0u, Alignment: 1UL, Type: 8u, Flags: 0x800UL),
        };

        foreach (var testCase in cases)
        {
            byte[] section = SyntheticImageBuilders.CompressDebugSection([0x2A]);
            BinaryPrimitives.WriteUInt32LittleEndian(section.AsSpan(4), testCase.Reserved);
            BinaryPrimitives.WriteUInt64LittleEndian(section.AsSpan(16), testCase.Alignment);
            byte[] image = SyntheticImageBuilders.BuildElf(
                (".debug_info", 0, section, testCase.Type, 0u, testCase.Flags));

            Assert.IsNull(ReadDebugInfo(image));
        }
    }

    /// <summary>
    /// Verifies zlib output must match <c>ch_size</c> exactly and corrupted streams return null.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadSectionBytes_OutputLengthMismatchOrCorruption_ReturnsNull()
    {
        byte[] content = [.. Enumerable.Range(0, 64).Select(static value => (byte)value)];
        byte[] compressed = SyntheticImageBuilders.CompressDebugSection(content);

        foreach (ulong declaredLength in new ulong[] { 63, 65 })
        {
            byte[] mismatch = [.. compressed];
            BinaryPrimitives.WriteUInt64LittleEndian(mismatch.AsSpan(8), declaredLength);
            Assert.IsNull(ReadDebugInfo(BuildCompressedElf(mismatch)));
        }

        byte[] corrupted = [.. compressed];
        corrupted[24] = 0xFF;
        Assert.IsNull(ReadDebugInfo(BuildCompressedElf(corrupted)));

        Assert.IsNull(ReadDebugInfo(BuildCompressedElf(compressed[..^1])));
    }

    /// <summary>
    /// Verifies overflowing section extents read as absent rather than reaching span slicing.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadSectionBytes_OverflowingExtent_ReturnsNull()
    {
        byte[] image = new byte[64];
        var overflowingOffset = new ElfImageReader.ElfSection(
            ".debug_info", 1, 0, int.MaxValue, 1, 0, 0, 0);
        var overflowingSize = new ElfImageReader.ElfSection(
            ".debug_info", 1, 0, 1, int.MaxValue, 0, 0, 0);

        Assert.IsNull(ElfImageReader.ReadSectionBytes(
            image, overflowingOffset, NativeImageDataLimits.MaxMaterializedBytes));
        Assert.IsNull(ElfImageReader.ReadSectionBytes(
            image, overflowingSize, NativeImageDataLimits.MaxMaterializedBytes));
    }

    private static byte[] BuildCompressedElf(byte[] section) =>
        SyntheticImageBuilders.BuildElf((".debug_info", 0, section, 1u, 0u, 0x800UL));

    private static byte[]? ReadDebugInfo(byte[] image) =>
        ElfImageReader.ReadSectionBytes(
            image,
            ElfImageReader.ReadSections(image).Single(section => section.Name == ".debug_info"),
            NativeImageDataLimits.MaxMaterializedBytes);
}
