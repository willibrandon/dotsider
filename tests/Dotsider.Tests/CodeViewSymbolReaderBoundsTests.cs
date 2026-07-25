using Dotsider.Core.Analysis.NativePdb;
using System.Buffers.Binary;
using System.Text;

namespace Dotsider.Tests;

/// <summary>Verifies contained CodeView C13, checksum, and names-table parsing.</summary>
[TestClass]
public sealed class CodeViewSymbolReaderBoundsTests
{
    private const uint DebugSFileChecksums = 0xF4;
    private const uint DebugSLines = 0xF2;
    private const uint NamesSignature = 0xEFFE_EFFE;

    /// <summary>Verifies valid synthetic line metadata resolves the procedure's source location.</summary>
    [TestMethod]
    public void ReadModule_ValidLineMetadata_ResolvesSourceFileAndLine()
    {
        var checksums = BuildChecksums(nameOffset: 0);
        var lines = BuildLines(fileChecksumOffset: 0, lineCount: 1, blockByteSize: 20);
        var c13 = Concat(
            BuildSubsection(DebugSFileChecksums, checksums),
            BuildSubsection(DebugSLines, lines));
        var (moduleStream, module) = BuildModule(c13);

        var valid = CodeViewSymbolReader.TryReadModule(
            moduleStream,
            module,
            BuildNames("source.cs"),
            out var symbols);

        Assert.IsTrue(valid);
        var symbol = Assert.ContainsSingle(symbols);
        Assert.AreEqual("Function", symbol.Name);
        Assert.AreEqual(1, symbol.Segment);
        Assert.AreEqual(0x20U, symbol.Offset);
        Assert.AreEqual(0x20U, symbol.Size);
        Assert.IsFalse(symbol.IsData);
        Assert.AreEqual("source.cs", symbol.SourceFile);
        Assert.AreEqual(42, symbol.Line);
    }

    /// <summary>Verifies subsection length and alignment overflow rejects the module.</summary>
    /// <param name="malformation">The subsection extent rule to violate.</param>
    [TestMethod]
    [DataRow("LengthOverflow")]
    [DataRow("MissingAlignmentPadding")]
    public void ReadModule_SubsectionLengthOrAlignmentOverflow_ReturnsEmpty(string malformation)
    {
        byte[] c13;
        if (malformation == "LengthOverflow")
        {
            c13 = new byte[8];
            BinaryPrimitives.WriteUInt32LittleEndian(c13, DebugSFileChecksums);
            BinaryPrimitives.WriteUInt32LittleEndian(c13.AsSpan(4), int.MaxValue);
        }
        else
        {
            c13 = new byte[9];
            BinaryPrimitives.WriteUInt32LittleEndian(c13, DebugSFileChecksums);
            BinaryPrimitives.WriteUInt32LittleEndian(c13.AsSpan(4), 1);
        }

        var (moduleStream, module) = BuildModule(c13);

        Assert.IsFalse(CodeViewSymbolReader.TryReadModule(
            moduleStream,
            module,
            [],
            out var symbols));
        Assert.IsEmpty(symbols);
    }

    /// <summary>Verifies an impossible line-count stride is rejected before line attribution.</summary>
    /// <param name="hasColumns">Whether the block uses the larger line-plus-column stride.</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ReadModule_LineCountStrideOutsideBlock_ReturnsEmpty(bool hasColumns)
    {
        var checksums = BuildChecksums(nameOffset: 0);
        var lines = BuildLines(
            fileChecksumOffset: 0,
            lineCount: uint.MaxValue,
            blockByteSize: 20,
            hasColumns);
        var c13 = Concat(
            BuildSubsection(DebugSFileChecksums, checksums),
            BuildSubsection(DebugSLines, lines));
        var (moduleStream, module) = BuildModule(c13);

        Assert.IsFalse(CodeViewSymbolReader.TryReadModule(
            moduleStream,
            module,
            BuildNames("source.cs"),
            out var symbols));
        Assert.IsEmpty(symbols);
    }

    /// <summary>Verifies checksum references cannot wrap into the checksum subsection.</summary>
    [TestMethod]
    public void ReadModule_ChecksumOffsetOverflow_ReturnsEmpty()
    {
        var checksums = BuildChecksums(nameOffset: 0);
        var lines = BuildLines(
            fileChecksumOffset: uint.MaxValue,
            lineCount: 1,
            blockByteSize: 20);
        var c13 = Concat(
            BuildSubsection(DebugSFileChecksums, checksums),
            BuildSubsection(DebugSLines, lines));
        var (moduleStream, module) = BuildModule(c13);

        Assert.IsFalse(CodeViewSymbolReader.TryReadModule(
            moduleStream,
            module,
            BuildNames("source.cs"),
            out var symbols));
        Assert.IsEmpty(symbols);
    }

    /// <summary>Verifies a truncated checksum record cannot supply a source-file reference.</summary>
    [TestMethod]
    public void ReadModule_TruncatedChecksumRecord_ReturnsEmpty()
    {
        var checksums = new byte[6];
        var lines = BuildLines(fileChecksumOffset: 0, lineCount: 1, blockByteSize: 20);
        var c13 = Concat(
            BuildSubsection(DebugSFileChecksums, checksums),
            BuildSubsection(DebugSLines, lines));
        var (moduleStream, module) = BuildModule(c13);

        Assert.IsFalse(CodeViewSymbolReader.TryReadModule(
            moduleStream,
            module,
            BuildNames("source.cs"),
            out var symbols));
        Assert.IsEmpty(symbols);
    }

    /// <summary>Verifies `/names` declarations and references remain inside the string data.</summary>
    /// <param name="malformation">The names-table range rule to violate.</param>
    [TestMethod]
    [DataRow("DeclaredByteSize")]
    [DataRow("NameOffset")]
    public void ReadModule_NamesRangeOverflow_ReturnsEmpty(string malformation)
    {
        var nameOffset = malformation == "NameOffset" ? uint.MaxValue : 0;
        var checksums = BuildChecksums(nameOffset);
        var lines = BuildLines(fileChecksumOffset: 0, lineCount: 1, blockByteSize: 20);
        var c13 = Concat(
            BuildSubsection(DebugSFileChecksums, checksums),
            BuildSubsection(DebugSLines, lines));
        var (moduleStream, module) = BuildModule(c13);
        var names = BuildNames("source.cs");
        if (malformation == "DeclaredByteSize")
        {
            BinaryPrimitives.WriteUInt32LittleEndian(names.AsSpan(8), uint.MaxValue);
        }

        Assert.IsFalse(CodeViewSymbolReader.TryReadModule(
            moduleStream,
            module,
            names,
            out var symbols));
        Assert.IsEmpty(symbols);
    }

    /// <summary>Verifies a name must terminate inside the declared `/names` byte range.</summary>
    [TestMethod]
    public void ReadModule_UnterminatedName_ReturnsEmpty()
    {
        var checksums = BuildChecksums(nameOffset: 0);
        var lines = BuildLines(fileChecksumOffset: 0, lineCount: 1, blockByteSize: 20);
        var c13 = Concat(
            BuildSubsection(DebugSFileChecksums, checksums),
            BuildSubsection(DebugSLines, lines));
        var (moduleStream, module) = BuildModule(c13);
        var names = BuildNames("source.cs");
        names[^1] = (byte)'x';

        Assert.IsFalse(CodeViewSymbolReader.TryReadModule(
            moduleStream,
            module,
            names,
            out var symbols));
        Assert.IsEmpty(symbols);
    }

    /// <summary>Verifies known CodeView symbol records require an in-record NUL terminator.</summary>
    [TestMethod]
    public void ReadModule_UnterminatedSymbolName_ReturnsEmpty()
    {
        var moduleStream = BuildProcedureRecord();
        moduleStream[^1] = (byte)'x';
        var module = new DbiModule(4, (uint)moduleStream.Length, 0, 0);

        Assert.IsFalse(CodeViewSymbolReader.TryReadModule(
            moduleStream,
            module,
            [],
            out var symbols));
        Assert.IsEmpty(symbols);
    }

    /// <summary>Verifies a module cannot declare both legacy and C13 line streams.</summary>
    [TestMethod]
    public void ReadModule_C11AndC13Declared_ReturnsEmpty()
    {
        var moduleStream = new byte[10];
        var module = new DbiModule(4, 8, 1, 1);

        Assert.IsFalse(CodeViewSymbolReader.TryReadModule(
            moduleStream,
            module,
            [],
            out var symbols));
        Assert.IsEmpty(symbols);
    }

    private static byte[] BuildChecksums(uint nameOffset)
    {
        var content = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(content, nameOffset);
        return content;
    }

    private static byte[] BuildLines(
        uint fileChecksumOffset,
        uint lineCount,
        uint blockByteSize,
        bool hasColumns = false)
    {
        var content = new byte[32];
        BinaryPrimitives.WriteUInt32LittleEndian(content, 0x20);
        BinaryPrimitives.WriteUInt16LittleEndian(content.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(content.AsSpan(6), hasColumns ? (ushort)1 : (ushort)0);
        BinaryPrimitives.WriteUInt32LittleEndian(content.AsSpan(8), 0x20);
        BinaryPrimitives.WriteUInt32LittleEndian(content.AsSpan(12), fileChecksumOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(content.AsSpan(16), lineCount);
        BinaryPrimitives.WriteUInt32LittleEndian(content.AsSpan(20), blockByteSize);
        BinaryPrimitives.WriteUInt32LittleEndian(content.AsSpan(24), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(content.AsSpan(28), 42);
        return content;
    }

    private static (byte[] Stream, DbiModule Module) BuildModule(byte[] c13)
    {
        var symbols = BuildProcedureRecord();
        var stream = Concat(symbols, c13);
        return (
            stream,
            new DbiModule(
                SymbolStream: 4,
                SymbolByteSize: (uint)symbols.Length,
                C11ByteSize: 0,
                C13ByteSize: (uint)c13.Length));
    }

    private static byte[] BuildNames(string name)
    {
        var data = Encoding.UTF8.GetBytes(name + "\0");
        var names = new byte[12 + data.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(names, NamesSignature);
        BinaryPrimitives.WriteUInt32LittleEndian(names.AsSpan(4), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(names.AsSpan(8), (uint)data.Length);
        data.CopyTo(names.AsSpan(12));
        return names;
    }

    private static byte[] BuildProcedureRecord()
    {
        var name = "Function\0"u8;
        const int bodySizeWithoutName = 35;
        var bodySize = bodySizeWithoutName + name.Length;
        var length = checked((ushort)(sizeof(ushort) + bodySize));
        var stream = new byte[4 + sizeof(ushort) + length];
        var record = stream.AsSpan(4);
        BinaryPrimitives.WriteUInt16LittleEndian(record, length);
        BinaryPrimitives.WriteUInt16LittleEndian(record[2..], 0x1110);
        var body = record[4..];
        BinaryPrimitives.WriteUInt32LittleEndian(body[12..], 0x20);
        BinaryPrimitives.WriteUInt32LittleEndian(body[28..], 0x20);
        BinaryPrimitives.WriteUInt16LittleEndian(body[32..], 1);
        name.CopyTo(body[35..]);
        return stream;
    }

    private static byte[] BuildSubsection(uint kind, byte[] content)
    {
        var paddedLength = (content.Length + 3) & ~3;
        var subsection = new byte[8 + paddedLength];
        BinaryPrimitives.WriteUInt32LittleEndian(subsection, kind);
        BinaryPrimitives.WriteUInt32LittleEndian(subsection.AsSpan(4), (uint)content.Length);
        content.CopyTo(subsection.AsSpan(8));
        return subsection;
    }

    private static byte[] Concat(params byte[][] parts)
    {
        var result = new byte[parts.Sum(static part => part.Length)];
        var offset = 0;
        foreach (var part in parts)
        {
            part.CopyTo(result, offset);
            offset += part.Length;
        }

        return result;
    }
}
