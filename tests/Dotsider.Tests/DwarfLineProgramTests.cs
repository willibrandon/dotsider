using Dotsider.Core.Analysis.Dwarf;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="DwarfLineProgram"/> — header parsing across v2–v5 and DWARF64, the two
/// file-table shapes, the row state machine (special, standard, and extended opcodes), and the
/// decl-primary source attribution — driven with hand-built <c>.debug_line</c> blobs.
/// </summary>
[TestClass]
public class DwarfLineProgramTests
{
    private const int MaxV5TableEntries = 65_536;

    private static readonly byte[] StandardLengths = [0, 1, 1, 1, 1, 0, 0, 0, 1, 0, 0, 1];

    private static DwarfSections Sections(byte[] line, byte[]? str = null, byte[]? lineStr = null) =>
        new([], [], str ?? [], lineStr ?? [], [], [], line, [], []);

    /// <summary>Builds a v2–v4 line program with the classic directory and file lists.</summary>
    private static byte[] Program(
        ushort version, byte[] ops, string[] dirs, (string Name, int Dir)[] files,
        sbyte lineBase = -5, byte lineRange = 14, byte opcodeBase = 13,
        byte[]? standardLengths = null)
    {
        var tail = new DwarfBlob()
            .U8(1); // minimum_instruction_length
        if (version >= 4) tail.U8(1); // maximum_operations_per_instruction
        tail.U8(1) // default_is_stmt
            .U8((byte)lineBase).U8(lineRange).U8(opcodeBase);
        var lengths = standardLengths ?? StandardLengths;
        for (var i = 0; i < opcodeBase - 1; i++) tail.U8(lengths[i]);

        foreach (var dir in dirs) tail.CStr(dir);
        tail.U8(0);
        foreach (var (name, dir) in files) tail.CStr(name).ULeb((ulong)dir).ULeb(0).ULeb(0);
        tail.U8(0);

        var body = new DwarfBlob().U16(version).U32((uint)tail.Length).Bytes(tail.ToArray()).Bytes(ops);
        return new DwarfBlob().U32((uint)body.Length).Bytes(body.ToArray()).ToArray();
    }

    /// <summary>Builds a v5 line program from raw directory and file table encodings.</summary>
    private static byte[] V5Program(
        byte[] directoryTable, byte[] fileTable, byte[]? ops = null, bool is64 = false)
    {
        var tail = new DwarfBlob()
            .U8(1).U8(1).U8(1)
            .U8(unchecked((byte)(sbyte)-5)).U8(14).U8(13);
        foreach (var length in StandardLengths) tail.U8(length);
        tail.Bytes(directoryTable).Bytes(fileTable);

        var body = new DwarfBlob().U16(5).U8(8).U8(0);
        if (is64)
            body.U64((ulong)tail.Length);
        else
            body.U32((uint)tail.Length);
        body.Bytes(tail.ToArray()).Bytes(ops ?? []);

        return is64
            ? new DwarfBlob().U32(0xFFFF_FFFF).U64((ulong)body.Length).Bytes(body.ToArray()).ToArray()
            : new DwarfBlob().U32((uint)body.Length).Bytes(body.ToArray()).ToArray();
    }

    /// <summary>
    /// Verifies a v4 program joins directories into file names, decodes copy/advance rows, ends
    /// coverage at the end-sequence row, and attributes decl-first with row fallback.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Parse_V4_RowsAndDeclPrimaryAttribution()
    {
        var ops = new DwarfBlob()
            .U8(0).ULeb(9).U8(2).U64(0x1000)    // set_address 0x1000
            .U8(3).SLeb(9)                      // advance_line -> 10
            .U8(1)                              // copy: row(0x1000, file 1, 10)
            .U8(2).ULeb(0x20)                   // advance_pc -> 0x1020
            .U8(3).SLeb(5)                      // -> 15
            .U8(1)                              // copy: row(0x1020, file 1, 15)
            .U8(2).ULeb(0x10)                   // -> 0x1030
            .U8(0).ULeb(1).U8(1)                // end_sequence at 0x1030
            .ToArray();

        var line = Program(4, ops, ["src"], [("main.cs", 1), ("/abs/other.cs", 1)]);
        var program = DwarfLineProgram.Parse(Sections(line), 0);

        Assert.IsNotNull(program);
        Assert.AreEqual("src/main.cs", program.FileName(1));
        Assert.AreEqual("/abs/other.cs", program.FileName(2)); // rooted names are not joined
        Assert.IsNull(program.FileName(0));                   // v4 tables are 1-based
        Assert.IsNull(program.FileName(3));

        Assert.IsTrue(program.TryFindLine(0x1000, out var file, out var lineNo));
        Assert.AreEqual("src/main.cs", file);
        Assert.AreEqual(10, lineNo);
        Assert.IsTrue(program.TryFindLine(0x101F, out _, out lineNo));
        Assert.AreEqual(10, lineNo);
        Assert.IsTrue(program.TryFindLine(0x102F, out _, out lineNo));
        Assert.AreEqual(15, lineNo);
        Assert.IsFalse(program.TryFindLine(0x1030, out _, out _)); // past the sequence
        Assert.IsFalse(program.TryFindLine(0x0FFF, out _, out _)); // before it

        Assert.AreEqual(("src/main.cs", 42), program.ResolveSource(1, 42, 0x1000));   // decl primary
        Assert.AreEqual(("src/main.cs", 15), program.ResolveSource(-1, 0, 0x1024));   // row fallback
        Assert.AreEqual(("src/main.cs", 10), program.ResolveSource(1, 0, 0x1000));    // mixed
    }

    /// <summary>
    /// Verifies special-opcode address/line arithmetic, <c>const_add_pc</c>,
    /// <c>fixed_advance_pc</c>, and that an unknown standard opcode is skipped by its declared
    /// operand count.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Parse_SpecialAndPcOpcodes_AdvanceExactly()
    {
        // opcode_base 14: op 13 is an unknown standard opcode declared with 2 operands.
        byte[] lengths = [0, 1, 1, 1, 1, 0, 0, 0, 1, 0, 0, 1, 2];
        var ops = new DwarfBlob()
            .U8(0).ULeb(9).U8(2).U64(0x2000)    // set_address 0x2000
            .U8(14 + 53)                        // special: +8 addr, +2 line -> row(0x2008, 3)
            .U8(13).ULeb(7).ULeb(9)             // unknown standard opcode: skip 2 LEBs
            .U8(8)                              // const_add_pc: +(255-14)/6 = +40 -> 0x2030
            .U8(9).U16(0x10)                    // fixed_advance_pc -> 0x2040
            .U8(14 + 4)                         // special: +0 addr, +1 line -> row(0x2040, 4)
            .U8(0).ULeb(1).U8(1)                // end_sequence
            .ToArray();

        var line = Program(4, ops, [], [("a.c", 0)],
            lineBase: -3, lineRange: 6, opcodeBase: 14, standardLengths: lengths);
        var program = DwarfLineProgram.Parse(Sections(line), 0);

        Assert.IsNotNull(program);
        Assert.IsTrue(program.TryFindLine(0x2008, out var file, out var lineNo));
        Assert.AreEqual("a.c", file); // directory index 0 resolves to the bare name
        Assert.AreEqual(3, lineNo);
        Assert.IsTrue(program.TryFindLine(0x203F, out _, out lineNo));
        Assert.AreEqual(3, lineNo);
        Assert.IsTrue(program.TryFindLine(0x2040, out _, out lineNo));
        Assert.AreEqual(4, lineNo);
    }

    /// <summary>Verifies a v2 header (no maximum-operations field) still parses and decodes rows.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Parse_V2Header_DecodesRows()
    {
        var ops = new DwarfBlob()
            .U8(0).ULeb(9).U8(2).U64(0x100)
            .U8(1)
            .U8(0).ULeb(1).U8(1)
            .ToArray();

        var program = DwarfLineProgram.Parse(Sections(Program(2, ops, [], [("f.c", 0)])), 0);

        Assert.IsNotNull(program);
        Assert.IsTrue(program.TryFindLine(0x100, out var file, out _));
        Assert.AreEqual("f.c", file);
    }

    /// <summary>
    /// Verifies the v5 form-described tables: directories via <c>line_strp</c>, files carrying
    /// path, directory index, and skipped timestamp/MD5 columns, with 0-based numbering.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Parse_V5Tables_ResolveFormsAndZeroBasedFiles()
    {
        var lineStr = new DwarfBlob().CStr("proj").CStr("src").ToArray(); // offsets 0 and 5

        var tail = new DwarfBlob()
            .U8(1).U8(1).U8(1)                          // min_inst, max_ops, default_is_stmt
            .U8(unchecked((byte)(sbyte)-5)).U8(14).U8(13);
        foreach (var l in StandardLengths) tail.U8(l);

        tail.U8(1).ULeb(1).ULeb(DwarfForm.LineStrp)     // directory format: path as line_strp
            .ULeb(2).U32(0).U32(5);                     // 2 directories: "proj", "src"
        tail.U8(4)                                      // file format: 4 columns
            .ULeb(1).ULeb(DwarfForm.String)             //   path
            .ULeb(2).ULeb(DwarfForm.Udata)              //   directory index
            .ULeb(3).ULeb(DwarfForm.Udata)              //   timestamp (skipped)
            .ULeb(5).ULeb(DwarfForm.Data16)             //   MD5 (skipped)
            .ULeb(2)
            .CStr("app.cs").ULeb(0).ULeb(7).Bytes(new byte[16])
            .CStr("util.cs").ULeb(1).ULeb(9).Bytes(new byte[16]);

        var ops = new DwarfBlob()
            .U8(0).ULeb(9).U8(2).U64(0x9000)
            .U8(4).ULeb(1)                              // set_file 1
            .U8(1)                                      // copy: row(0x9000, file 1, 1)
            .U8(2).ULeb(0x10)
            .U8(0).ULeb(1).U8(1)
            .ToArray();

        var body = new DwarfBlob().U16(5).U8(8).U8(0)
            .U32((uint)tail.Length).Bytes(tail.ToArray()).Bytes(ops);
        var line = new DwarfBlob().U32((uint)body.Length).Bytes(body.ToArray()).ToArray();

        var program = DwarfLineProgram.Parse(Sections(line, lineStr: lineStr), 0);

        Assert.IsNotNull(program);
        Assert.AreEqual("proj/app.cs", program.FileName(0));
        Assert.AreEqual("src/util.cs", program.FileName(1));
        Assert.IsTrue(program.TryFindLine(0x9008, out var file, out var lineNo));
        Assert.AreEqual("src/util.cs", file);
        Assert.AreEqual(1, lineNo);
    }

    /// <summary>
    /// Verifies the specification-defined empty v5 file table, and an empty directory table,
    /// consume no entries and still produce a valid empty line program.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Parse_V5ZeroFormatsAndEntries_AcceptsEmptyTables()
    {
        var emptyTable = new DwarfBlob().U8(0).ULeb(0).ToArray();

        var program = DwarfLineProgram.Parse(
            Sections(V5Program(emptyTable, emptyTable)), 0);

        Assert.IsNotNull(program);
        Assert.IsNull(program.FileName(0));
        Assert.IsFalse(program.TryFindLine(0, out _, out _));
    }

    /// <summary>
    /// Verifies a non-empty directory or file table with no entry descriptors is rejected before
    /// iterating its attacker-controlled count.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Parse_V5ZeroFormatsWithEntries_RejectsBothTables()
    {
        var emptyTable = new DwarfBlob().U8(0).ULeb(0).ToArray();
        var nonProgressingTable = new DwarfBlob()
            .U8(0).ULeb(MaxV5TableEntries + 1UL)
            .ToArray();
        var directoryTable = new DwarfBlob()
            .U8(1).ULeb(1).ULeb(DwarfForm.String)
            .ULeb(1).CStr("src")
            .ToArray();

        var badDirectories = DwarfLineProgram.Parse(
            Sections(V5Program(nonProgressingTable, emptyTable)), 0);
        var badFiles = DwarfLineProgram.Parse(
            Sections(V5Program(directoryTable, nonProgressingTable)), 0);

        Assert.IsNull(badDirectories);
        Assert.IsNull(badFiles);
    }

    /// <summary>
    /// Verifies the v5 table entry ceiling accepts its exact boundary and rejects the first
    /// value above it before allocating entries.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Parse_V5EntryLimit_EnforcesExactBoundary()
    {
        var emptyDirectories = new DwarfBlob().U8(0).ULeb(0).ToArray();
        var maximumFiles = new DwarfBlob()
            .U8(1).ULeb(1).ULeb(DwarfForm.String)
            .ULeb(MaxV5TableEntries).Bytes(new byte[MaxV5TableEntries])
            .ToArray();
        var excessiveFiles = new DwarfBlob()
            .U8(1).ULeb(1).ULeb(DwarfForm.String)
            .ULeb(MaxV5TableEntries + 1UL).Bytes(new byte[MaxV5TableEntries + 1])
            .ToArray();

        var maximum = DwarfLineProgram.Parse(
            Sections(V5Program(emptyDirectories, maximumFiles)), 0);
        var excessive = DwarfLineProgram.Parse(
            Sections(V5Program(emptyDirectories, excessiveFiles)), 0);

        Assert.IsNotNull(maximum);
        Assert.AreEqual("", maximum.FileName(MaxV5TableEntries - 1));
        Assert.IsNull(maximum.FileName(MaxV5TableEntries));
        Assert.IsNull(excessive);
    }

    /// <summary>
    /// Verifies a structurally impossible entry count and a table that would have to borrow
    /// line-program bytes both invalidate the prologue.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Parse_V5EntryDataOutsidePrologue_IsRejected()
    {
        var emptyTable = new DwarfBlob().U8(0).ULeb(0).ToArray();
        var truncatedFiles = new DwarfBlob()
            .U8(1).ULeb(1).ULeb(DwarfForm.String)
            .ULeb(2).CStr("one.cs")
            .ToArray();
        var headerWithoutEntry = new DwarfBlob()
            .U8(1).ULeb(1).ULeb(DwarfForm.String)
            .ULeb(1)
            .ToArray();

        var truncated = DwarfLineProgram.Parse(
            Sections(V5Program(emptyTable, truncatedFiles)), 0);
        var borrowed = DwarfLineProgram.Parse(
            Sections(V5Program(headerWithoutEntry, emptyTable, "borrowed\0"u8.ToArray())), 0);

        Assert.IsNull(truncated);
        Assert.IsNull(borrowed);
    }

    /// <summary>
    /// Verifies every truncation of otherwise valid v5 directory and file tables fails closed
    /// when the enclosing unit and prologue lengths describe that truncated data exactly.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Parse_V5TablesTruncatedAtEveryByte_FailClosed()
    {
        var directoryTable = new DwarfBlob()
            .U8(1).ULeb(1).ULeb(DwarfForm.String)
            .ULeb(1).CStr("src")
            .ToArray();
        var fileTable = new DwarfBlob()
            .U8(2)
            .ULeb(1).ULeb(DwarfForm.String)
            .ULeb(2).ULeb(DwarfForm.Udata)
            .ULeb(1).CStr("app.cs").ULeb(0)
            .ToArray();
        byte[] tables = [.. directoryTable, .. fileTable];

        var complete = DwarfLineProgram.Parse(
            Sections(V5Program(directoryTable, fileTable)), 0);
        Assert.IsNotNull(complete);

        for (var length = 0; length < tables.Length; length++)
        {
            var truncated = DwarfLineProgram.Parse(
                Sections(V5Program(tables[..length], [])), 0);
            Assert.IsNull(truncated, $"table prefix length {length} unexpectedly parsed");
        }
    }

    /// <summary>
    /// Verifies a table with entries rejects an unsupported form instead of treating it as a
    /// zero-width value.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Parse_V5UnsupportedEntryForm_IsRejected()
    {
        var unknownFormTable = new DwarfBlob()
            .U8(1).ULeb(1).ULeb(0xFF)
            .ULeb(MaxV5TableEntries)
            .ToArray();
        var emptyTable = new DwarfBlob().U8(0).ULeb(0).ToArray();

        var program = DwarfLineProgram.Parse(
            Sections(V5Program(unknownFormTable, emptyTable)), 0);

        Assert.IsNull(program);
    }

    /// <summary>
    /// Verifies a directory index that cannot fit the parser's index representation invalidates
    /// the table rather than wrapping to a plausible directory.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Parse_V5DirectoryIndexAboveInt32_IsRejected()
    {
        var directoryTable = new DwarfBlob()
            .U8(1).ULeb(1).ULeb(DwarfForm.String)
            .ULeb(1).CStr("src")
            .ToArray();
        var fileTable = new DwarfBlob()
            .U8(2)
            .ULeb(1).ULeb(DwarfForm.String)
            .ULeb(2).ULeb(DwarfForm.Udata)
            .ULeb(1).CStr("app.cs").ULeb((ulong)int.MaxValue + 1)
            .ToArray();

        var program = DwarfLineProgram.Parse(
            Sections(V5Program(directoryTable, fileTable)), 0);

        Assert.IsNull(program);
    }

    /// <summary>
    /// Verifies every v5 form supported by the table reader consumes its minimum encoded width
    /// in DWARF32 and DWARF64, and rejects a value truncated by one byte.
    /// </summary>
    [TestMethod]
    [DataRow(DwarfForm.String, 1, false)]
    [DataRow(DwarfForm.Udata, 1, false)]
    [DataRow(DwarfForm.Data1, 1, false)]
    [DataRow(DwarfForm.Data2, 2, false)]
    [DataRow(DwarfForm.Data4, 4, false)]
    [DataRow(DwarfForm.Data8, 8, false)]
    [DataRow(DwarfForm.Data16, 16, false)]
    [DataRow(DwarfForm.Block, 1, false)]
    [DataRow(DwarfForm.Strp, 4, false)]
    [DataRow(DwarfForm.Strp, 8, true)]
    [DataRow(DwarfForm.LineStrp, 4, false)]
    [DataRow(DwarfForm.LineStrp, 8, true)]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Parse_V5SupportedEntryForm_RequiresItsMinimumWidth(
        ulong form, int minimumWidth, bool is64)
    {
        var emptyDirectories = new DwarfBlob().U8(0).ULeb(0).ToArray();
        var exactFiles = new DwarfBlob()
            .U8(2)
            .ULeb(1).ULeb(DwarfForm.String)
            .ULeb(0x2000).ULeb(form)
            .ULeb(1).CStr("app.cs").Bytes(new byte[minimumWidth])
            .ToArray();
        var truncatedFiles = new DwarfBlob()
            .U8(2)
            .ULeb(1).ULeb(DwarfForm.String)
            .ULeb(0x2000).ULeb(form)
            .ULeb(1).CStr("app.cs").Bytes(new byte[minimumWidth - 1])
            .ToArray();

        var exact = DwarfLineProgram.Parse(
            Sections(V5Program(emptyDirectories, exactFiles, is64: is64)), 0);
        var truncated = DwarfLineProgram.Parse(
            Sections(V5Program(emptyDirectories, truncatedFiles, is64: is64)), 0);

        Assert.IsNotNull(exact);
        Assert.AreEqual("app.cs", exact.FileName(0));
        Assert.IsNull(truncated);
    }

    /// <summary>
    /// Verifies <c>DW_LNE_define_file</c> appends to the v4 file table mid-program and rows can
    /// attribute to the added file.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Parse_DefineFile_AppendsToTable()
    {
        var define = new DwarfBlob().CStr("gen.c").ULeb(1).ULeb(0).ULeb(0).ToArray();
        var ops = new DwarfBlob()
            .U8(0).ULeb((ulong)(1 + define.Length)).U8(3).Bytes(define) // define_file "inc/gen.c"
            .U8(4).ULeb(2)                                              // set_file 2
            .U8(0).ULeb(9).U8(2).U64(0x100)
            .U8(1)
            .U8(0).ULeb(1).U8(1)
            .ToArray();

        var program = DwarfLineProgram.Parse(Sections(Program(4, ops, ["inc"], [("main.c", 0)])), 0);

        Assert.IsNotNull(program);
        Assert.AreEqual("inc/gen.c", program.FileName(2));
        Assert.IsTrue(program.TryFindLine(0x100, out var file, out _));
        Assert.AreEqual("inc/gen.c", file);
    }

    /// <summary>Verifies a DWARF64 header (escaped length, 8-byte header length) parses.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Parse_Dwarf64Header_DecodesRows()
    {
        var tail = new DwarfBlob().U8(1).U8(1).U8(1)
            .U8(unchecked((byte)(sbyte)-5)).U8(14).U8(13);
        foreach (var l in StandardLengths) tail.U8(l);
        tail.U8(0).CStr("f.c").ULeb(0).ULeb(0).ULeb(0).U8(0); // no dirs, one file

        var ops = new DwarfBlob()
            .U8(0).ULeb(9).U8(2).U64(0x100)
            .U8(1)
            .U8(0).ULeb(1).U8(1)
            .ToArray();

        var body = new DwarfBlob().U16(4).U64((ulong)tail.Length).Bytes(tail.ToArray()).Bytes(ops);
        var line = new DwarfBlob().U32(0xFFFF_FFFF).U64((ulong)body.Length).Bytes(body.ToArray()).ToArray();

        var program = DwarfLineProgram.Parse(Sections(line), 0);

        Assert.IsNotNull(program);
        Assert.IsTrue(program.TryFindLine(0x100, out var file, out _));
        Assert.AreEqual("f.c", file);
    }

    /// <summary>
    /// Verifies malformed inputs: out-of-range offsets and bad headers yield no program, and a
    /// program whose body breaks mid-opcode keeps the rows decoded before the damage.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Parse_Malformed_FailsClosedOrKeepsPartialRows()
    {
        Assert.IsNull(DwarfLineProgram.Parse(Sections([1, 2, 3]), 0x100));

        var badVersion = Program(4, [], [], []);
        badVersion[4] = 9; // version u16 low byte
        Assert.IsNull(DwarfLineProgram.Parse(Sections(badVersion), 0));

        var zeroRange = Program(4, [], [], []);
        zeroRange[14] = 0; // line_range: length(4) + version(2) + header_length(4) + 4 machine bytes
        Assert.IsNull(DwarfLineProgram.Parse(Sections(zeroRange), 0));

        // A row, then an extended op whose declared length overruns the unit: row survives.
        var ops = new DwarfBlob()
            .U8(0).ULeb(9).U8(2).U64(0x500)
            .U8(1)
            .U8(0).ULeb(200)
            .ToArray();
        var program = DwarfLineProgram.Parse(Sections(Program(4, ops, [], [("k.c", 0)])), 0);
        Assert.IsNotNull(program);
        Assert.IsTrue(program.TryFindLine(0x500, out var file, out _));
        Assert.AreEqual("k.c", file);

        // An empty program parses but covers nothing.
        var empty = DwarfLineProgram.Parse(Sections(Program(4, [], [], [("e.c", 0)])), 0);
        Assert.IsNotNull(empty);
        Assert.IsFalse(empty.TryFindLine(0, out _, out _));
    }
}
