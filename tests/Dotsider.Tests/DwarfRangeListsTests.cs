using Dotsider.Core.Analysis.Dwarf;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="DwarfRangeLists"/> — the v4 <c>.debug_ranges</c> pair walk and the v5
/// <c>.debug_rnglists</c> RLE walk — driven with hand-built section blobs covering every entry
/// opcode, the base-address escapes, and the <c>rnglistx</c> offset-array indirection.
/// </summary>
public class DwarfRangeListsTests
{
    private static DwarfReader.UnitContext Unit(
        ushort version, int addressSize = 8, ulong baseAddress = 0,
        long addrBase = 8, long rnglistsBase = 12) =>
        new(version, Is64: false, addressSize, baseAddress,
            StrOffsetsBase: 8, addrBase, rnglistsBase, StmtListOffset: -1);

    private static DwarfSections Sections(byte[]? ranges = null, byte[]? rngLists = null, byte[]? addr = null) =>
        new([], [], [], [], [], addr ?? [], [], ranges ?? [], rngLists ?? []);

    /// <summary>Builds a v5 <c>.debug_addr</c> section (8-byte header, u64 entries).</summary>
    private static byte[] AddrTable(params ulong[] entries)
    {
        var blob = new DwarfBlob().U32((uint)(4 + entries.Length * 8)).U16(5).U8(8).U8(0);
        foreach (var e in entries) blob.U64(e);
        return blob.ToArray();
    }

    /// <summary>
    /// Verifies the v4 walk sums begin/end pairs against the CU base, honors the <c>(-1, base)</c>
    /// base-address escape, and stops at the <c>(0, 0)</c> terminator.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void TryResolve_V4Pairs_SumsAndRebases()
    {
        var ranges = new DwarfBlob()
            .U64(0x10).U64(0x20)                    // [base+0x10, base+0x20)
            .U64(ulong.MaxValue).U64(0x2000)        // base := 0x2000
            .U64(0x0).U64(0x40)                     // [0x2000, 0x2040)
            .U64(0).U64(0)                          // terminator
            .ToArray();

        var ok = DwarfRangeLists.TryResolve(Sections(ranges: ranges), 0, isRnglistx: false,
            Unit(4, baseAddress: 0x1000), out var start, out var size);

        Assert.True(ok);
        Assert.Equal(0x1010UL, start);
        Assert.Equal(0x50UL, size);
    }

    /// <summary>Verifies the v4 walk uses 4-byte entries and the 32-bit escape for 32-bit units.</summary>
    [Fact(Timeout = 30_000)]
    public void TryResolve_V4FourByteAddresses_UsesNarrowEscape()
    {
        var ranges = new DwarfBlob()
            .U32(0x10).U32(0x20)
            .U32(uint.MaxValue).U32(0x9000)
            .U32(0x0).U32(0x8)
            .U32(0).U32(0)
            .ToArray();

        var ok = DwarfRangeLists.TryResolve(Sections(ranges: ranges), 0, isRnglistx: false,
            Unit(4, addressSize: 4, baseAddress: 0x100), out var start, out var size);

        Assert.True(ok);
        Assert.Equal(0x110UL, start);
        Assert.Equal(0x18UL, size);
    }

    /// <summary>
    /// Verifies the v5 walk decodes every RLE opcode: base switches (direct and via
    /// <c>.debug_addr</c>), offset pairs, start/end, start/length, and the <c>startx</c> forms.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void TryResolve_V5AllOpcodes_SumsCoveredBytes()
    {
        var list = new DwarfBlob()
            .U8(0x05).U64(0x5000)                   // base_address 0x5000
            .U8(0x04).ULeb(0x10).ULeb(0x30)         // offset_pair -> [0x5010, 0x5030) = 0x20
            .U8(0x07).U64(0x6000).ULeb(0x25)        // start_length = 0x25
            .U8(0x03).ULeb(0).ULeb(8)               // startx_length: addr[0]=0x7000, 8 bytes
            .U8(0x02).ULeb(0).ULeb(1)               // startx_endx: [0x7000, 0x8000) = 0x1000
            .U8(0x01).ULeb(1)                       // base_addressx: base := addr[1] = 0x8000
            .U8(0x04).ULeb(0).ULeb(4)               // offset_pair -> [0x8000, 0x8004) = 4
            .U8(0x06).U64(0x9000).U64(0x9010)       // start_end = 0x10
            .U8(0x00)                               // end_of_list
            .ToArray();

        var ok = DwarfRangeLists.TryResolve(
            Sections(rngLists: list, addr: AddrTable(0x7000, 0x8000)),
            0, isRnglistx: false, Unit(5), out var start, out var size);

        Assert.True(ok);
        Assert.Equal(0x5010UL, start);
        Assert.Equal(0x20UL + 0x25 + 8 + 0x1000 + 4 + 0x10, size);
    }

    /// <summary>
    /// Verifies a <c>rnglistx</c> index resolves through the CU's offset array — each entry an
    /// offset from the array base to its list — before walking the list.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void TryResolve_Rnglistx_ResolvesThroughOffsetArray()
    {
        var list0 = new DwarfBlob().U8(0x07).U64(0x111).ULeb(1).U8(0x00).ToArray();
        var list1 = new DwarfBlob().U8(0x07).U64(0x2000).ULeb(0x30).U8(0x00).ToArray();
        var section = new DwarfBlob()
            .U32(0).U16(5).U8(8).U8(0).U32(2)       // header: length (unused), version, addr, seg, count
            .U32(8).U32((uint)(8 + list0.Length))   // offset array, relative to its own base (12)
            .Bytes(list0).Bytes(list1)
            .ToArray();

        var ok = DwarfRangeLists.TryResolve(Sections(rngLists: section), 1, isRnglistx: true,
            Unit(5), out var start, out var size);

        Assert.True(ok);
        Assert.Equal(0x2000UL, start);
        Assert.Equal(0x30UL, size);

        Assert.True(DwarfRangeLists.TryResolve(Sections(rngLists: section), 0, isRnglistx: true,
            Unit(5), out start, out size));
        Assert.Equal(0x111UL, start);
        Assert.Equal(1UL, size);
    }

    /// <summary>
    /// Verifies malformed inputs fail closed: out-of-range offsets, unknown opcodes, a
    /// <c>startx</c> without <c>.debug_addr</c>, and lists that cover nothing.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void TryResolve_Malformed_ReturnsFalse()
    {
        // Offset beyond the section.
        Assert.False(DwarfRangeLists.TryResolve(Sections(ranges: [1, 2]), 0x100, false,
            Unit(4), out _, out _));

        // Unknown v5 opcode.
        Assert.False(DwarfRangeLists.TryResolve(Sections(rngLists: [0xAA]), 0, false,
            Unit(5), out _, out _));

        // startx with no .debug_addr to resolve against.
        var startx = new DwarfBlob().U8(0x03).ULeb(0).ULeb(8).U8(0x00).ToArray();
        Assert.False(DwarfRangeLists.TryResolve(Sections(rngLists: startx), 0, false,
            Unit(5), out _, out _));

        // Immediate terminator: nothing covered.
        Assert.False(DwarfRangeLists.TryResolve(Sections(rngLists: [0x00]), 0, false,
            Unit(5), out _, out _));
        var emptyV4 = new DwarfBlob().U64(0).U64(0).ToArray();
        Assert.False(DwarfRangeLists.TryResolve(Sections(ranges: emptyV4), 0, false,
            Unit(4), out _, out _));

        // rnglistx index beyond the offset array.
        var tiny = new DwarfBlob().U32(0).U16(5).U8(8).U8(0).U32(0).ToArray();
        Assert.False(DwarfRangeLists.TryResolve(Sections(rngLists: tiny), 5, true,
            Unit(5), out _, out _));

        // Truncated v4 list (no terminator) keeps nothing rather than throwing.
        var truncated = new DwarfBlob().U64(0x10).U64(0x20).ToArray();
        Assert.False(DwarfRangeLists.TryResolve(Sections(ranges: truncated), 0, false,
            Unit(4), out _, out _));
    }
}
