using Dotsider.Core.Analysis.NativePdb;
using System.Buffers.Binary;

namespace Dotsider.Tests;

/// <summary>Verifies DBI and module-substream size declarations use contained unsigned ranges.</summary>
[TestClass]
public sealed class DbiStreamBoundsTests
{
    /// <summary>Verifies DBI preserves the unsigned on-disk module size fields without wrapping.</summary>
    [TestMethod]
    public void Parse_ModuleSizeDeclarations_ArePreservedWithoutSignedWrap()
    {
        var stream = BuildDbiWithModule(
            symbolByteSize: 0x8000_0000,
            c11ByteSize: 0xFFFF_FFFE,
            c13ByteSize: uint.MaxValue);

        var dbi = DbiStream.Parse(stream);

        Assert.IsNotNull(dbi);
        var module = Assert.ContainsSingle(dbi.Modules);
        Assert.AreEqual(0x8000_0000U, module.SymbolByteSize);
        Assert.AreEqual(0xFFFF_FFFEU, module.C11ByteSize);
        Assert.AreEqual(uint.MaxValue, module.C13ByteSize);
    }

    /// <summary>Verifies every non-nil unsigned DBI stream index remains addressable.</summary>
    [TestMethod]
    public void Parse_ModuleStreamAboveSignedInt16Range_IsPreserved()
    {
        var stream = BuildDbiWithModule(
            symbolByteSize: 8,
            c11ByteSize: 0,
            c13ByteSize: 0,
            symbolStream: 0x8000);

        var dbi = DbiStream.Parse(stream);

        Assert.IsNotNull(dbi);
        Assert.AreEqual(0x8000, Assert.ContainsSingle(dbi.Modules).SymbolStream);
    }

    /// <summary>Verifies impossible module substream ranges produce no CodeView symbols.</summary>
    /// <param name="range">The module substream declaration to make impossible.</param>
    [TestMethod]
    [DataRow("C11")]
    [DataRow("C13")]
    [DataRow("Symbols")]
    public void ReadModule_OverflowingModuleSubstreamRanges_ReturnsEmpty(string range)
    {
        var module = range switch
        {
            "C11" => new DbiModule(4, 8, uint.MaxValue, 0),
            "C13" => new DbiModule(4, 8, 0, uint.MaxValue),
            "Symbols" => new DbiModule(4, uint.MaxValue, 0, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(range)),
        };

        var valid = CodeViewSymbolReader.TryReadModule(
            new byte[64],
            module,
            [],
            out var symbols);

        Assert.IsFalse(valid);
        Assert.IsEmpty(symbols);
    }

    /// <summary>Verifies every top-level DBI substream must fit before the optional header.</summary>
    /// <param name="fieldOffset">The DBI header size field to corrupt.</param>
    /// <param name="value">The impossible signed declaration.</param>
    [TestMethod]
    [DataRow(24, -1)]
    [DataRow(24, int.MaxValue)]
    [DataRow(28, -1)]
    [DataRow(28, int.MaxValue)]
    [DataRow(32, -1)]
    [DataRow(32, int.MaxValue)]
    [DataRow(36, -1)]
    [DataRow(36, int.MaxValue)]
    [DataRow(40, -1)]
    [DataRow(40, int.MaxValue)]
    [DataRow(48, -1)]
    [DataRow(48, int.MaxValue)]
    [DataRow(52, -1)]
    [DataRow(52, int.MaxValue)]
    public void Parse_TopLevelSubstreamRangeOutsideDbi_ReturnsNull(int fieldOffset, int value)
    {
        var stream = new byte[64];
        BinaryPrimitives.WriteInt32LittleEndian(stream, -1);
        BinaryPrimitives.WriteInt32LittleEndian(stream.AsSpan(fieldOffset), value);

        Assert.IsNull(DbiStream.Parse(stream));
    }

    /// <summary>Verifies both NUL-terminated DBI module names are structurally required.</summary>
    [TestMethod]
    public void Parse_UnterminatedModuleName_ReturnsNull()
    {
        var stream = BuildDbiWithModule(8, 0, 0);
        stream.AsSpan(64 + 64, 4).Fill((byte)'A');

        Assert.IsNull(DbiStream.Parse(stream));
    }

    private static byte[] BuildDbiWithModule(
        uint symbolByteSize,
        uint c11ByteSize,
        uint c13ByteSize,
        ushort symbolStream = 4)
    {
        const int moduleInfoSize = 68;
        var stream = new byte[64 + moduleInfoSize];
        BinaryPrimitives.WriteInt32LittleEndian(stream, -1);
        BinaryPrimitives.WriteInt32LittleEndian(stream.AsSpan(24), moduleInfoSize);
        BinaryPrimitives.WriteUInt16LittleEndian(stream.AsSpan(64 + 34), symbolStream);
        BinaryPrimitives.WriteUInt32LittleEndian(stream.AsSpan(64 + 36), symbolByteSize);
        BinaryPrimitives.WriteUInt32LittleEndian(stream.AsSpan(64 + 40), c11ByteSize);
        BinaryPrimitives.WriteUInt32LittleEndian(stream.AsSpan(64 + 44), c13ByteSize);
        return stream;
    }
}
