using Dotsider.Core.Analysis.Dwarf;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="DwarfReader"/> — the DIE walk that recovers functions — driven with
/// hand-built <c>.debug_info</c>/<c>.debug_abbrev</c> blobs so every form the decoder claims
/// (string indirection, indexed addresses, DWARF64, references, skips) is pinned byte-for-byte.
/// </summary>
[TestClass]
public class DwarfReaderTests
{
    /// <summary>Writes one abbreviation declaration (code, tag, children, attribute/form pairs).</summary>
    private static DwarfBlob Decl(
        DwarfBlob blob, ulong code, ulong tag, bool children, params (ulong At, ulong Form)[] attrs)
    {
        blob.ULeb(code).ULeb(tag).U8((byte)(children ? 1 : 0));
        foreach (var (at, form) in attrs) blob.ULeb(at).ULeb(form);
        return blob.ULeb(0).ULeb(0);
    }

    /// <summary>Wraps DIE bytes in a compilation-unit header of the given version.</summary>
    private static byte[] Cu(ushort version, byte[] dies, uint abbrevOffset = 0, bool is64 = false, byte unitType = 1)
    {
        var body = new DwarfBlob().U16(version);
        if (version >= 5)
        {
            body.U8(unitType).U8(8);
            if (is64) body.U64(abbrevOffset);
            else body.U32(abbrevOffset);
        }
        else
        {
            if (is64) body.U64(abbrevOffset);
            else body.U32(abbrevOffset);
            body.U8(8);
        }

        body.Bytes(dies);
        var unit = new DwarfBlob();
        if (is64) unit.U32(0xFFFF_FFFF).U64((ulong)body.Length);
        else unit.U32((uint)body.Length);
        return unit.Bytes(body.ToArray()).ToArray();
    }

    private static DwarfSections Sections(
        byte[] info, byte[] abbrev, byte[]? str = null, byte[]? lineStr = null,
        byte[]? strOffsets = null, byte[]? addr = null) =>
        new(info, abbrev, str ?? [], lineStr ?? [], strOffsets ?? [], addr ?? [], [], [], []);

    /// <summary>Builds a v5 <c>.debug_str_offsets</c> section (8-byte header, u32 entries).</summary>
    private static byte[] StrOffsetsTable(params uint[] entries)
    {
        var blob = new DwarfBlob().U32((uint)(4 + entries.Length * 4)).U16(5).U16(0);
        foreach (var e in entries) blob.U32(e);
        return blob.ToArray();
    }

    /// <summary>Builds a v5 <c>.debug_addr</c> section (8-byte header, u64 entries).</summary>
    private static byte[] AddrTable(params ulong[] entries)
    {
        var blob = new DwarfBlob().U32((uint)(4 + entries.Length * 8)).U16(5).U8(8).U8(0);
        foreach (var e in entries) blob.U64(e);
        return blob.ToArray();
    }

    /// <summary>
    /// Verifies a v4 unit yields the subprogram's name, address, size from address-class
    /// <c>high_pc</c>, and decl file/line, and that the unit context captures the CU's base
    /// address and line-program offset.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadFunctions_V4_ReadsNameAddressSizeAndDeclInfo()
    {
        var abbrev = new DwarfBlob();
        Decl(abbrev, 1, DwarfForm.TagCompileUnit, true,
            (DwarfForm.AtLowPc, DwarfForm.Addr), (DwarfForm.AtStmtList, DwarfForm.SecOffset));
        Decl(abbrev, 2, DwarfForm.TagSubprogram, false,
            (DwarfForm.AtName, DwarfForm.String), (DwarfForm.AtDeclFile, DwarfForm.Data1),
            (DwarfForm.AtDeclLine, DwarfForm.Data2), (DwarfForm.AtLowPc, DwarfForm.Addr),
            (DwarfForm.AtHighPc, DwarfForm.Addr));
        abbrev.ULeb(0);

        var dies = new DwarfBlob()
            .ULeb(1).U64(0x400000).U32(0x77)
            .ULeb(2).CStr("main").U8(3).U16(42).U64(0x401000).U64(0x401040)
            .ULeb(0);

        var result = DwarfReader.ReadFunctions(Sections(Cu(4, dies.ToArray()), abbrev.ToArray()));

        var (function, unit) = Assert.ContainsSingle(result);
        Assert.AreEqual("main", function.Name);
        Assert.AreEqual(0x401000UL, function.LowPc);
        Assert.AreEqual(0x40UL, function.Size);
        Assert.AreEqual(3, function.DeclFile);
        Assert.AreEqual(42, function.DeclLine);
        Assert.AreEqual(0x77, function.StmtListOffset);
        Assert.AreEqual(4, unit.Version);
        Assert.AreEqual(0x400000UL, unit.BaseAddress);
        Assert.AreEqual(0x77, unit.StmtListOffset);
    }

    /// <summary>
    /// Verifies a v5 unit honors explicit <c>str_offsets_base</c>/<c>addr_base</c> attributes
    /// when resolving <c>strx1</c>/<c>addrx1</c>, and takes offset-class <c>high_pc</c> as the
    /// size directly.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadFunctions_V5_ExplicitBasesAndOffsetHighPc()
    {
        var abbrev = new DwarfBlob();
        Decl(abbrev, 1, DwarfForm.TagCompileUnit, true,
            (DwarfForm.AtStrOffsetsBase, DwarfForm.SecOffset), (DwarfForm.AtAddrBase, DwarfForm.SecOffset));
        Decl(abbrev, 2, DwarfForm.TagSubprogram, false,
            (DwarfForm.AtName, DwarfForm.Strx1), (DwarfForm.AtLowPc, DwarfForm.Addrx1),
            (DwarfForm.AtHighPc, DwarfForm.Data4));
        abbrev.ULeb(0);

        // Bases point past the tables' first entries, so index 0 must land on the second entry.
        var dies = new DwarfBlob()
            .ULeb(1).U32(12).U32(16)
            .ULeb(2).U8(0).U8(0).U32(0x30)
            .ULeb(0);

        var sections = Sections(Cu(5, dies.ToArray()), abbrev.ToArray(),
            str: new DwarfBlob().U8(0).CStr("EntryPoint").ToArray(),
            strOffsets: StrOffsetsTable(999, 1),
            addr: AddrTable(0xDEAD, 0x2000));

        var (function, unit) = Assert.ContainsSingle(DwarfReader.ReadFunctions(sections));
        Assert.AreEqual("EntryPoint", function.Name);
        Assert.AreEqual(0x2000UL, function.LowPc);
        Assert.AreEqual(0x30UL, function.Size);
        Assert.AreEqual(5, unit.Version);
    }

    /// <summary>
    /// Verifies every string form resolves the name: <c>strp</c>, <c>line_strp</c>, and the five
    /// <c>strx</c> encodings through the default v5 <c>.debug_str_offsets</c> base.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(DwarfForm.Strp)]
    [DataRow(DwarfForm.LineStrp)]
    [DataRow(DwarfForm.Strx)]
    [DataRow(DwarfForm.Strx1)]
    [DataRow(DwarfForm.Strx2)]
    [DataRow(DwarfForm.Strx3)]
    [DataRow(DwarfForm.Strx4)]
    public void ReadFunctions_StringForms_ResolveName(ulong form)
    {
        var abbrev = new DwarfBlob();
        Decl(abbrev, 1, DwarfForm.TagCompileUnit, true);
        Decl(abbrev, 2, DwarfForm.TagSubprogram, false,
            (DwarfForm.AtName, form), (DwarfForm.AtLowPc, DwarfForm.Addr));
        abbrev.ULeb(0);

        var dies = new DwarfBlob().ULeb(1).ULeb(2);
        switch (form)
        {
            case DwarfForm.Strp or DwarfForm.LineStrp: dies.U32(1); break;
            case DwarfForm.Strx: dies.ULeb(1); break;
            case DwarfForm.Strx1: dies.U8(1); break;
            case DwarfForm.Strx2: dies.U16(1); break;
            case DwarfForm.Strx3: dies.U16(1).U8(0); break;
            case DwarfForm.Strx4: dies.U32(1); break;
        }

        dies.U64(0x1000).ULeb(0);

        var strings = new DwarfBlob().U8(0).CStr("fn").ToArray();
        var sections = Sections(Cu(5, dies.ToArray()), abbrev.ToArray(),
            str: strings, lineStr: strings, strOffsets: StrOffsetsTable(0, 1));

        var (function, _) = Assert.ContainsSingle(DwarfReader.ReadFunctions(sections));
        Assert.AreEqual("fn", function.Name);
        Assert.AreEqual(0x1000UL, function.LowPc);
    }

    /// <summary>
    /// Verifies every indexed address form resolves <c>low_pc</c> through the default v5
    /// <c>.debug_addr</c> base.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(DwarfForm.Addrx)]
    [DataRow(DwarfForm.Addrx1)]
    [DataRow(DwarfForm.Addrx2)]
    [DataRow(DwarfForm.Addrx3)]
    [DataRow(DwarfForm.Addrx4)]
    public void ReadFunctions_AddressIndexForms_ResolveLowPc(ulong form)
    {
        var abbrev = new DwarfBlob();
        Decl(abbrev, 1, DwarfForm.TagCompileUnit, true);
        Decl(abbrev, 2, DwarfForm.TagSubprogram, false,
            (DwarfForm.AtName, DwarfForm.String), (DwarfForm.AtLowPc, form));
        abbrev.ULeb(0);

        var dies = new DwarfBlob().ULeb(1).ULeb(2).CStr("f");
        switch (form)
        {
            case DwarfForm.Addrx: dies.ULeb(1); break;
            case DwarfForm.Addrx1: dies.U8(1); break;
            case DwarfForm.Addrx2: dies.U16(1); break;
            case DwarfForm.Addrx3: dies.U16(1).U8(0); break;
            case DwarfForm.Addrx4: dies.U32(1); break;
        }

        dies.ULeb(0);

        var sections = Sections(Cu(5, dies.ToArray()), abbrev.ToArray(), addr: AddrTable(0, 0x4000));

        var (function, _) = Assert.ContainsSingle(DwarfReader.ReadFunctions(sections));
        Assert.AreEqual(0x4000UL, function.LowPc);
    }

    /// <summary>
    /// Verifies a DWARF64 unit parses: 0xFFFFFFFF-escaped length, 8-byte abbrev offset, and
    /// 8-byte <c>strp</c> section offsets.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadFunctions_Dwarf64_ParsesUnitAndWideOffsets()
    {
        var abbrev = new DwarfBlob();
        Decl(abbrev, 1, DwarfForm.TagCompileUnit, true);
        Decl(abbrev, 2, DwarfForm.TagSubprogram, false,
            (DwarfForm.AtName, DwarfForm.Strp), (DwarfForm.AtLowPc, DwarfForm.Addr),
            (DwarfForm.AtHighPc, DwarfForm.Data8));
        abbrev.ULeb(0);

        var dies = new DwarfBlob()
            .ULeb(1)
            .ULeb(2).U64(1).U64(0x1000).U64(0x20)
            .ULeb(0);

        var sections = Sections(Cu(4, dies.ToArray(), is64: true), abbrev.ToArray(),
            str: new DwarfBlob().U8(0).CStr("fn").ToArray());

        var (function, unit) = Assert.ContainsSingle(DwarfReader.ReadFunctions(sections));
        Assert.IsTrue(unit.Is64);
        Assert.AreEqual("fn", function.Name);
        Assert.AreEqual(0x20UL, function.Size);
    }

    /// <summary>
    /// Builds two CUs where the second CU's definition DIE names itself through a reference to a
    /// declaration DIE (name + linkage name) earlier in that CU — the declaration sits at
    /// unit-relative offset 12, section-absolute offset 24.
    /// </summary>
    private static DwarfSections SpecificationPair(ulong refAttribute, ulong refForm, uint refValue)
    {
        var abbrev = new DwarfBlob();
        Decl(abbrev, 1, DwarfForm.TagCompileUnit, false);
        abbrev.ULeb(0);
        var secondTableOffset = (uint)abbrev.Length;
        Decl(abbrev, 1, DwarfForm.TagCompileUnit, true);
        Decl(abbrev, 2, DwarfForm.TagSubprogram, false,
            (DwarfForm.AtName, DwarfForm.String), (DwarfForm.AtLinkageName, DwarfForm.String));
        Decl(abbrev, 3, DwarfForm.TagSubprogram, false,
            (refAttribute, refForm), (DwarfForm.AtLowPc, DwarfForm.Addr), (DwarfForm.AtHighPc, DwarfForm.Addr));
        abbrev.ULeb(0);

        var first = Cu(4, new DwarfBlob().ULeb(1).ToArray()); // 12 bytes: root-only unit
        var dies = new DwarfBlob()
            .ULeb(1)
            .ULeb(2).CStr("Widget.Run").CStr("_ZN6Widget3RunEv")
            .ULeb(3).U32(refValue).U64(0x5000).U64(0x5010)
            .ULeb(0);
        var second = Cu(4, dies.ToArray(), abbrevOffset: secondTableOffset);
        return Sections([.. first, .. second], abbrev.ToArray());
    }

    /// <summary>
    /// Verifies a nameless definition resolves its name through a unit-relative
    /// <c>specification</c> reference, preferring the declaration's linkage name.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadFunctions_SpecificationRef4_ResolvesUnitRelativeName()
    {
        var sections = SpecificationPair(DwarfForm.AtSpecification, DwarfForm.Ref4, refValue: 12);

        var (function, _) = Assert.ContainsSingle(DwarfReader.ReadFunctions(sections));
        Assert.AreEqual("_ZN6Widget3RunEv", function.Name);
        Assert.AreEqual(0x5000UL, function.LowPc);
        Assert.AreEqual(0x10UL, function.Size);
    }

    /// <summary>
    /// Verifies <c>abstract_origin</c> in the <c>ref_addr</c> form resolves as a
    /// section-absolute offset, not unit-relative.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadFunctions_AbstractOriginRefAddr_ResolvesSectionAbsoluteName()
    {
        var sections = SpecificationPair(DwarfForm.AtAbstractOrigin, DwarfForm.RefAddr, refValue: 24);

        var (function, _) = Assert.ContainsSingle(DwarfReader.ReadFunctions(sections));
        Assert.AreEqual("_ZN6Widget3RunEv", function.Name);
    }

    /// <summary>
    /// Verifies a v4 range-based subprogram (no <c>low_pc</c>) is kept with its
    /// <c>.debug_ranges</c> offset recorded as a section offset.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadFunctions_V4Ranges_RecordsSectionOffset()
    {
        var abbrev = new DwarfBlob();
        Decl(abbrev, 1, DwarfForm.TagCompileUnit, true, (DwarfForm.AtLowPc, DwarfForm.Addr));
        Decl(abbrev, 2, DwarfForm.TagSubprogram, false,
            (DwarfForm.AtName, DwarfForm.String), (DwarfForm.AtRanges, DwarfForm.SecOffset));
        abbrev.ULeb(0);

        var dies = new DwarfBlob()
            .ULeb(1).U64(0x400000)
            .ULeb(2).CStr("ranged").U32(0x40)
            .ULeb(0);

        var result = DwarfReader.ReadFunctions(Sections(Cu(4, dies.ToArray()), abbrev.ToArray()));

        var (function, unit) = Assert.ContainsSingle(result);
        Assert.AreEqual("ranged", function.Name);
        Assert.AreEqual(0x40, function.RangesOffset);
        Assert.IsFalse(function.RangesIsRnglistx);
        Assert.AreEqual(0x400000UL, unit.BaseAddress);
    }

    /// <summary>
    /// Verifies a v5 <c>rnglistx</c> range reference is recorded as an index and the CU's
    /// explicit <c>rnglists_base</c> is captured.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadFunctions_V5Rnglistx_RecordsIndexAndBase()
    {
        var abbrev = new DwarfBlob();
        Decl(abbrev, 1, DwarfForm.TagCompileUnit, true, (DwarfForm.AtRnglistsBase, DwarfForm.SecOffset));
        Decl(abbrev, 2, DwarfForm.TagSubprogram, false,
            (DwarfForm.AtName, DwarfForm.String), (DwarfForm.AtRanges, DwarfForm.Rnglistx));
        abbrev.ULeb(0);

        var dies = new DwarfBlob()
            .ULeb(1).U32(0x20)
            .ULeb(2).CStr("r5").ULeb(2)
            .ULeb(0);

        var result = DwarfReader.ReadFunctions(Sections(Cu(5, dies.ToArray()), abbrev.ToArray()));

        var (function, unit) = Assert.ContainsSingle(result);
        Assert.AreEqual(2, function.RangesOffset);
        Assert.IsTrue(function.RangesIsRnglistx);
        Assert.AreEqual(0x20, unit.RnglistsBase);
    }

    /// <summary>
    /// Verifies a DIE carrying every skippable form — blocks, exprloc, supplementary refs,
    /// <c>data16</c>, <c>implicit_const</c>, <c>indirect</c>, and an unresolvable <c>strx2</c> —
    /// advances the cursor exactly, so the subprogram after it still parses.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadFunctions_SkipsEveryFormOnUnrelatedDie()
    {
        var abbrev = new DwarfBlob();
        Decl(abbrev, 1, DwarfForm.TagCompileUnit, true);

        abbrev.ULeb(2).ULeb(0x0B).U8(0); // DW_TAG_lexical_block, no children
        void Attr(ulong form) => abbrev.ULeb(0x60).ULeb(form);
        Attr(DwarfForm.Exprloc);
        Attr(DwarfForm.Block1);
        Attr(DwarfForm.Data16);
        Attr(DwarfForm.RefSig8);
        Attr(DwarfForm.FlagPresent);
        Attr(DwarfForm.Sdata);
        abbrev.ULeb(0x60).ULeb(DwarfForm.ImplicitConst).SLeb(-5);
        Attr(DwarfForm.Block2);
        Attr(DwarfForm.Block4);
        Attr(DwarfForm.Block);
        Attr(DwarfForm.RefSup4);
        Attr(DwarfForm.RefSup8);
        Attr(DwarfForm.StrpSup);
        Attr(DwarfForm.Flag);
        Attr(DwarfForm.Ref1);
        Attr(DwarfForm.Ref2);
        Attr(DwarfForm.Ref8);
        Attr(DwarfForm.RefUdata);
        Attr(DwarfForm.Udata);
        Attr(DwarfForm.Loclistx);
        Attr(DwarfForm.Indirect);
        Attr(DwarfForm.Data4);
        Attr(DwarfForm.Strx2);
        abbrev.ULeb(0).ULeb(0);

        Decl(abbrev, 3, DwarfForm.TagSubprogram, false,
            (DwarfForm.AtName, DwarfForm.String), (DwarfForm.AtLowPc, DwarfForm.Addr));
        abbrev.ULeb(0);

        var dies = new DwarfBlob()
            .ULeb(1)
            .ULeb(2)
            .ULeb(3).Bytes([7, 8, 9])       // exprloc
            .U8(2).Bytes([1, 2])            // block1
            .Bytes(new byte[16])            // data16
            .U64(0)                         // ref_sig8
                                            // flag_present: no bytes
            .SLeb(-100)                     // sdata
                                            // implicit_const: no bytes
            .U16(1).U8(0)                   // block2
            .U32(0)                         // block4 (empty)
            .ULeb(1).U8(0xAA)               // block
            .U32(0)                         // ref_sup4
            .U64(0)                         // ref_sup8
            .U32(0)                         // strp_sup
            .U8(1)                          // flag
            .U8(1)                          // ref1
            .U16(2)                         // ref2
            .U64(3)                         // ref8
            .ULeb(4)                        // ref_udata
            .ULeb(5)                        // udata
            .ULeb(6)                        // loclistx
            .ULeb(DwarfForm.Data1).U8(7)    // indirect -> data1
            .U32(0xFEED)                    // data4
            .U16(1)                         // strx2 (no str_offsets section; still advances)
            .ULeb(3).CStr("after").U64(0x9000)
            .ULeb(0);

        var result = DwarfReader.ReadFunctions(Sections(Cu(5, dies.ToArray()), abbrev.ToArray()));

        var (function, _) = Assert.ContainsSingle(result);
        Assert.AreEqual("after", function.Name);
        Assert.AreEqual(0x9000UL, function.LowPc);
    }

    /// <summary>
    /// Verifies the linkage name (plain or MIPS-vendor) is preferred over the source name.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(DwarfForm.AtLinkageName)]
    [DataRow(DwarfForm.AtMipsLinkageName)]
    public void ReadFunctions_LinkageName_PreferredOverSourceName(ulong linkageAttribute)
    {
        var abbrev = new DwarfBlob();
        Decl(abbrev, 1, DwarfForm.TagCompileUnit, true);
        Decl(abbrev, 2, DwarfForm.TagSubprogram, false,
            (DwarfForm.AtName, DwarfForm.String), (linkageAttribute, DwarfForm.String),
            (DwarfForm.AtLowPc, DwarfForm.Addr));
        abbrev.ULeb(0);

        var dies = new DwarfBlob()
            .ULeb(1)
            .ULeb(2).CStr("plain").CStr("_Zmangled").U64(0x1000)
            .ULeb(0);

        var (function, _) = Assert.ContainsSingle(DwarfReader.ReadFunctions(Sections(Cu(4, dies.ToArray()), abbrev.ToArray())));
        Assert.AreEqual("_Zmangled", function.Name);
    }

    /// <summary>
    /// Verifies an address-class <c>high_pc</c> below <c>low_pc</c> yields size 0 rather than
    /// wrapping negative.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadFunctions_HighPcBelowLowPc_SizeZero()
    {
        var abbrev = new DwarfBlob();
        Decl(abbrev, 1, DwarfForm.TagCompileUnit, true);
        Decl(abbrev, 2, DwarfForm.TagSubprogram, false,
            (DwarfForm.AtName, DwarfForm.String), (DwarfForm.AtLowPc, DwarfForm.Addr),
            (DwarfForm.AtHighPc, DwarfForm.Addr));
        abbrev.ULeb(0);

        var dies = new DwarfBlob()
            .ULeb(1)
            .ULeb(2).CStr("f").U64(0x2000).U64(0x1000)
            .ULeb(0);

        var (function, _) = Assert.ContainsSingle(DwarfReader.ReadFunctions(Sections(Cu(4, dies.ToArray()), abbrev.ToArray())));
        Assert.AreEqual(0UL, function.Size);
    }

    /// <summary>
    /// Verifies a second unit whose declared length overruns the section ends the walk, keeping
    /// the first unit's functions.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadFunctions_TruncatedSecondUnit_KeepsFirstFunction()
    {
        var abbrev = new DwarfBlob();
        Decl(abbrev, 1, DwarfForm.TagCompileUnit, true);
        Decl(abbrev, 2, DwarfForm.TagSubprogram, false,
            (DwarfForm.AtName, DwarfForm.String), (DwarfForm.AtLowPc, DwarfForm.Addr));
        abbrev.ULeb(0);

        var dies = new DwarfBlob().ULeb(1).ULeb(2).CStr("ok").U64(0x1000).ULeb(0);
        var truncated = new DwarfBlob().U32(0xFFFF).Bytes(new byte[12]).ToArray();
        var info = new List<byte>();
        info.AddRange(Cu(4, dies.ToArray()));
        info.AddRange(truncated);

        var result = DwarfReader.ReadFunctions(Sections([.. info], abbrev.ToArray()));

        var (function, _) = Assert.ContainsSingle(result);
        Assert.AreEqual("ok", function.Name);
    }

    /// <summary>
    /// Verifies an unsupported-version unit is skipped by its declared length and the walk
    /// continues into the next unit.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadFunctions_UnsupportedVersionUnit_SkipsToNextUnit()
    {
        var abbrev = new DwarfBlob();
        Decl(abbrev, 1, DwarfForm.TagCompileUnit, true);
        Decl(abbrev, 2, DwarfForm.TagSubprogram, false,
            (DwarfForm.AtName, DwarfForm.String), (DwarfForm.AtLowPc, DwarfForm.Addr));
        abbrev.ULeb(0);

        var dies = new DwarfBlob().ULeb(1).ULeb(2).CStr("ok").U64(0x1000).ULeb(0);
        var info = new List<byte>();
        info.AddRange(Cu(9, [1, 2, 3, 4]));
        info.AddRange(Cu(4, dies.ToArray()));

        var result = DwarfReader.ReadFunctions(Sections([.. info], abbrev.ToArray()));

        var (function, _) = Assert.ContainsSingle(result);
        Assert.AreEqual("ok", function.Name);
    }

    /// <summary>
    /// Verifies a v5 skeleton unit (unit type 4) contributes no functions even though it holds a
    /// well-formed subprogram.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadFunctions_SkeletonUnit_Skipped()
    {
        var abbrev = new DwarfBlob();
        Decl(abbrev, 1, DwarfForm.TagCompileUnit, true);
        Decl(abbrev, 2, DwarfForm.TagSubprogram, false,
            (DwarfForm.AtName, DwarfForm.String), (DwarfForm.AtLowPc, DwarfForm.Addr));
        abbrev.ULeb(0);

        var dies = new DwarfBlob().ULeb(1).ULeb(2).CStr("hidden").U64(0x1000).ULeb(0);

        var result = DwarfReader.ReadFunctions(
            Sections(Cu(5, dies.ToArray(), unitType: 4), abbrev.ToArray()));

        Assert.IsEmpty(result);
    }

    /// <summary>
    /// Verifies an abbreviation code missing from the table ends that unit's walk, keeping the
    /// functions parsed before it.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadFunctions_UnknownAbbrevCode_KeepsEarlierFunctions()
    {
        var abbrev = new DwarfBlob();
        Decl(abbrev, 1, DwarfForm.TagCompileUnit, true);
        Decl(abbrev, 2, DwarfForm.TagSubprogram, false,
            (DwarfForm.AtName, DwarfForm.String), (DwarfForm.AtLowPc, DwarfForm.Addr));
        abbrev.ULeb(0);

        var dies = new DwarfBlob().ULeb(1).ULeb(2).CStr("first").U64(0x1000).ULeb(99);

        var result = DwarfReader.ReadFunctions(Sections(Cu(4, dies.ToArray()), abbrev.ToArray()));

        var (function, _) = Assert.ContainsSingle(result);
        Assert.AreEqual("first", function.Name);
    }

    /// <summary>Verifies empty or absent sections yield no functions.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadFunctions_EmptyOrZeroLengthInfo_ReturnsEmpty()
    {
        Assert.IsEmpty(DwarfReader.ReadFunctions(Sections([], [])));

        var zeroLength = new DwarfBlob().U32(0).Bytes(new byte[16]).ToArray();
        Assert.IsEmpty(DwarfReader.ReadFunctions(Sections(zeroLength, [1])));
    }

    /// <summary>
    /// Verifies <see cref="DwarfSections.Collect"/> maps base names through the lookup and
    /// tolerates absent sections as empty bytes.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Collect_MapsBaseNamesAndToleratesAbsent()
    {
        var sections = DwarfSections.Collect(
            (name, _) => name == "info" ? [0xAB] : null,
            maximumTotalBytes: 1);

        Assert.AreSequenceEqual(new byte[] { 0xAB }, sections.Info);
        Assert.IsEmpty(sections.Abbrev);
        Assert.IsFalse(sections.HasInfo); // DIEs without abbreviations are unreadable
    }

    /// <summary>
    /// Verifies section collection shares one budget and stops materializing when it is exhausted.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Collect_ExactAggregateBudget_StopsLaterLookups()
    {
        var lookups = new List<(string Name, int Remaining)>();

        var sections = DwarfSections.Collect((name, remaining) =>
        {
            lookups.Add((name, remaining));
            return name switch
            {
                "info" => [0x01, 0x02],
                "abbrev" => [0x03],
                _ => [0x04],
            };
        }, maximumTotalBytes: 3);

        Assert.AreSequenceEqual(new byte[] { 0x01, 0x02 }, sections.Info);
        Assert.AreSequenceEqual(new byte[] { 0x03 }, sections.Abbrev);
        Assert.IsEmpty(sections.Str);
        Assert.AreSequenceEqual<(string Name, int Remaining)>(
            [("info", 3), ("abbrev", 1)],
            lookups);
    }

    /// <summary>
    /// Verifies an over-budget required section is discarded and stops unnecessary sibling lookups.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Collect_RequiredSectionExceedsBudget_StopsFurtherLookups()
    {
        var lookups = new List<string>();

        var sections = DwarfSections.Collect((name, _) =>
        {
            lookups.Add(name);
            return [0x01, 0x02, 0x03];
        },
            maximumTotalBytes: 2);

        Assert.IsEmpty(sections.Info);
        Assert.IsEmpty(sections.Abbrev);
        Assert.AreSequenceEqual<string>(["info"], lookups);
    }
}
