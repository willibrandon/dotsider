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
