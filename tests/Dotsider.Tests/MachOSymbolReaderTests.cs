using Dotsider.Core.Analysis;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="MachOSymbolReader"/> and the <see cref="MachOImageReader"/> additions
/// behind it — the executable-section function filter, the recognized-data pass, <c>N_FUN</c>
/// stab sizes, per-section clamping, <c>LC_FUNCTION_STARTS</c> deltas against the <c>__TEXT</c>
/// base, UUIDs, and fat archives — driven with synthetic images on every platform.
/// </summary>
[TestClass]
public class MachOSymbolReaderTests
{
    private const uint ExecFlags = 0x8000_0400; // pure + some instructions
    private const byte SectType = 0x0E;         // N_SECT
    private const byte FunStab = 0x24;          // N_FUN

    private static readonly IlcNameDemangler EmptyDemangler = new([]);
    private static readonly int[] ExpectedSectionOrdinals = [1, 2, 3];

    private static byte[] Image(
        (string Name, byte Type, byte Ordinal, ulong Value)[] symbols,
        byte[]? functionStarts = null) =>
        SyntheticImageBuilders.BuildMachO(
            [
                ("__TEXT", 0x1_0000_0000, new[]
                {
                    ("__text", 0x1_0000_1000UL, ExecFlags, new byte[0x100]),
                    ("__managedcode", 0x1_0000_2000UL, ExecFlags, new byte[0x80]),
                }),
                ("__DATA", 0x1_0000_4000, new[]
                {
                    ("__const", 0x1_0000_4000UL, 0u, new byte[0x100]),
                }),
            ],
            symbols, functionStarts: functionStarts);

    /// <summary>
    /// Verifies the section-driven split: symbols in any instruction-flagged section are
    /// functions (sized by next start, clamped to their section), a recognized data node in a
    /// data section is kept with the leading underscore stripped, and an unrecognized data-
    /// section label is dropped.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadSymbols_SplitsFunctionsAndRecognizedData()
    {
        var image = Image(
        [
            ("_frost_main", SectType, 1, 0x1_0000_1010),
            ("_helper", SectType, 1, 0x1_0000_1050),
            ("_managed_fn", SectType, 2, 0x1_0000_2010),
            ("__ZTV6Widget", SectType, 3, 0x1_0000_4010),
            ("_randomLabel", SectType, 3, 0x1_0000_4020),
        ]);

        var symbols = MachOSymbolReader.ReadSymbols(image, EmptyDemangler);

        Assert.HasCount(4, symbols); // three functions + one recognized data node

        var main = Assert.ContainsSingle(s => s.Name == "frost_main", symbols);
        Assert.AreEqual(0x40, main.Size); // next start
        Assert.AreEqual("__text", main.Section);
        Assert.IsFalse(main.IsData);
        Assert.IsNotNull(main.FileOffset);

        var helper = Assert.ContainsSingle(s => s.Name == "helper", symbols);
        Assert.AreEqual(0xB0, helper.Size); // clamped to __text's end, not __managedcode's start

        var managed = Assert.ContainsSingle(s => s.Name == "managed_fn", symbols);
        Assert.AreEqual("__managedcode", managed.Section);
        Assert.AreEqual(0x70, managed.Size); // clamped to its own section's end

        var data = Assert.ContainsSingle(s => s.Name == "_ZTV6Widget", symbols);
        Assert.IsTrue(data.IsData);
        Assert.AreEqual("__const", data.Section);
        Assert.DoesNotContain(s => s.Name.Contains("randomLabel"), symbols);
    }

    /// <summary>
    /// Verifies <c>N_FUN</c> stab pairs: the end entry's <c>n_value</c> is the explicit size,
    /// preferred over nearest-next sizing.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadSymbols_NFunPairs_CarryExplicitSizes()
    {
        var image = Image(
        [
            ("_dsym_fn", FunStab, 1, 0x1_0000_1010),
            ("", FunStab, 1, 0x18),                      // end entry: size
            ("_neighbor", SectType, 1, 0x1_0000_1020),   // nearest-next would say 0x10
        ]);

        var symbols = MachOSymbolReader.ReadSymbols(image, EmptyDemangler);

        var stabbed = Assert.ContainsSingle(s => s.Name == "dsym_fn", symbols);
        Assert.AreEqual(0x18, stabbed.Size);
    }

    /// <summary>
    /// Verifies <c>LC_FUNCTION_STARTS</c>: deltas are relative to the <c>__TEXT</c> segment's
    /// address even when another segment comes first, sizes come from successive starts, the
    /// last range clamps to its executable section's end, and a zero delta terminates.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadFunctionStartBoundaries_RelativeToTextSegment()
    {
        var starts = new DwarfBlob().ULeb(0x1010).ULeb(0x40).ULeb(0).ULeb(0x99).ToArray();
        var image = SyntheticImageBuilders.BuildMachO(
            [
                ("__DATA", 0x1_0000_4000, new[] { ("__const", 0x1_0000_4000UL, 0u, new byte[8]) }),
                ("__TEXT", 0x1_0000_0000, new[] { ("__text", 0x1_0000_1000UL, ExecFlags, new byte[0x100]) }),
            ],
            functionStarts: starts);

        var boundaries = MachOSymbolReader.ReadFunctionStartBoundaries(image);

        Assert.HasCount(2, boundaries); // the zero delta ends the stream
        Assert.AreEqual(0x1_0000_1010UL, boundaries[0].VirtualAddress);
        Assert.AreEqual(0x40, boundaries[0].Size);
        Assert.IsTrue(boundaries[0].IsBoundary);
        Assert.AreEqual(0x1_0000_1050UL, boundaries[1].VirtualAddress);
        Assert.AreEqual(0xB0, boundaries[1].Size); // clamped to __text's end
        TestAssert.All(boundaries, b => Assert.StartsWith("sub_", b.Name));
    }

    /// <summary>Verifies the UUID load command round-trips and its absence reports false.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TryReadUuid_RoundTripsAndReportsAbsence()
    {
        byte[] id = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
        var image = SyntheticImageBuilders.BuildMachO(
            [("__TEXT", 0x1_0000_0000, new[] { ("__text", 0x1_0000_1000UL, ExecFlags, new byte[8]) })],
            uuid: id);

        Assert.IsTrue(MachOImageReader.TryReadUuid(image, out var uuid));
        Assert.AreSequenceEqual(id, uuid);

        var plain = SyntheticImageBuilders.BuildMachO(
            [("__TEXT", 0x1_0000_0000, new[] { ("__text", 0x1_0000_1000UL, ExecFlags, new byte[8]) })]);
        Assert.IsFalse(MachOImageReader.TryReadUuid(plain, out _));
    }

    /// <summary>
    /// Verifies fat archives enumerate their slices — big-endian headers, per-arch offsets and
    /// sizes — and each slice parses as a thin image.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadFatSlices_EnumeratesAndSlicesParse()
    {
        var arm = SyntheticImageBuilders.BuildMachO(
            [("__TEXT", 0x1_0000_0000, new[] { ("__text", 0x1_0000_1000UL, ExecFlags, new byte[8]) })]);
        var x64 = SyntheticImageBuilders.BuildMachO(
            [("__TEXT", 0x1_4000_0000, new[] { ("__text", 0x1_4000_1000UL, ExecFlags, new byte[8]) })],
            cpuType: 0x0100_0007);
        var fat = SyntheticImageBuilders.BuildFat(arm, x64);

        Assert.IsTrue(MachOImageReader.IsFat(fat));
        Assert.IsFalse(MachOImageReader.IsMachO(fat));

        var slices = MachOImageReader.ReadFatSlices(fat);
        Assert.HasCount(2, slices);
        Assert.AreEqual(0x0100000CU, slices[0].CpuType);
        Assert.AreEqual(0x01000007U, slices[1].CpuType);

        var slice = fat.AsSpan((int)slices[1].Offset, (int)slices[1].Size);
        Assert.IsTrue(MachOImageReader.IsMachO(slice));
        var sections = MachOImageReader.ReadSectionList(slice);
        Assert.AreEqual("__text", Assert.ContainsSingle(sections).Name);

        Assert.IsEmpty(MachOImageReader.ReadFatSlices(arm)); // thin image: no slices
    }

    /// <summary>
    /// Verifies section ordinals run across segments in load order and executable flags decide
    /// the code filter, not names.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadSectionList_OrdinalsSpanSegments()
    {
        var image = SyntheticImageBuilders.BuildMachO(
        [
            ("__TEXT", 0x1_0000_0000, new[]
            {
                ("__text", 0x1_0000_1000UL, ExecFlags, new byte[8]),
                ("__unbox", 0x1_0000_2000UL, ExecFlags, new byte[8]),
            }),
            ("__DATA", 0x1_0000_4000, new[] { ("__const", 0x1_0000_4000UL, 0u, new byte[8]) }),
        ]);

        var sections = MachOImageReader.ReadSectionList(image);

        Assert.HasCount(3, sections);
        Assert.AreSequenceEqual(ExpectedSectionOrdinals, sections.Select(s => s.Ordinal));
        Assert.IsTrue(sections[0].IsExecutable);
        Assert.IsTrue(sections[1].IsExecutable);
        Assert.IsFalse(sections[2].IsExecutable);
        Assert.AreEqual("__DATA", sections[2].Segment);
    }

    /// <summary>Verifies malformed inputs yield empty results rather than throwing.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadSymbols_Malformed_ReturnsEmpty()
    {
        Assert.IsEmpty(MachOSymbolReader.ReadSymbols([0xDE, 0xAD], EmptyDemangler));
        Assert.IsEmpty(MachOSymbolReader.ReadFunctionStartBoundaries([0xDE, 0xAD]));

        // A symbol pointing at a nonexistent section ordinal is dropped, not misattributed.
        var orphan = Image([("_ghost", SectType, 99, 0x1_0000_1010)]);
        Assert.IsEmpty(MachOSymbolReader.ReadSymbols(orphan, EmptyDemangler));
    }
}
