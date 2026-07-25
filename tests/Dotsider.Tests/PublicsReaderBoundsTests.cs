using Dotsider.Core.Analysis.NativePdb;
using System.Buffers.Binary;

namespace Dotsider.Tests;

/// <summary>Verifies public-symbol hash, address-map, and record extents.</summary>
[TestClass]
public sealed class PublicsReaderBoundsTests
{
    /// <summary>Verifies a complete synthetic public record retains its name and address.</summary>
    [TestMethod]
    public void Read_ValidPublicSymbol_ReturnsRecord()
    {
        var publics = BuildPublicsStream(recordOffset: 0);
        var records = BuildPublicRecord(declaredLength: null);

        var valid = PublicsReader.TryRead(publics, records, out var symbols);

        Assert.IsTrue(valid);
        var symbol = Assert.ContainsSingle(symbols);
        Assert.AreEqual("PublicFunction", symbol.Name);
        Assert.AreEqual(1, symbol.Segment);
        Assert.AreEqual(0x30U, symbol.Offset);
        Assert.IsTrue(symbol.IsFunction);
    }

    /// <summary>Verifies malformed public/address-map ranges return no records.</summary>
    /// <param name="malformation">The public-symbol range rule to violate.</param>
    [TestMethod]
    [DataRow("AddressMapRange")]
    [DataRow("HashStartOverflow")]
    [DataRow("NegativeRecordOffset")]
    [DataRow("OutOfRangeRecordOffset")]
    [DataRow("PartialAddressMap")]
    [DataRow("TruncatedRecord")]
    [DataRow("UnterminatedName")]
    public void Read_InvalidPublicOrRecordRange_ReturnsEmpty(string malformation)
    {
        var publics = BuildPublicsStream(recordOffset: 0);
        var records = BuildPublicRecord(
            declaredLength: malformation == "TruncatedRecord" ? ushort.MaxValue : null);
        switch (malformation)
        {
            case "AddressMapRange":
                BinaryPrimitives.WriteInt32LittleEndian(publics.AsSpan(4), int.MaxValue);
                break;
            case "HashStartOverflow":
                BinaryPrimitives.WriteInt32LittleEndian(publics, int.MaxValue);
                break;
            case "NegativeRecordOffset":
                BinaryPrimitives.WriteInt32LittleEndian(publics.AsSpan(28), -1);
                break;
            case "OutOfRangeRecordOffset":
                BinaryPrimitives.WriteInt32LittleEndian(publics.AsSpan(28), int.MaxValue);
                break;
            case "PartialAddressMap":
                BinaryPrimitives.WriteInt32LittleEndian(publics.AsSpan(4), 2);
                break;
            case "TruncatedRecord":
                break;
            case "UnterminatedName":
                records[^1] = (byte)'x';
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(malformation));
        }

        Assert.IsFalse(PublicsReader.TryRead(publics, records, out var symbols));
        Assert.IsEmpty(symbols);
    }

    private static byte[] BuildPublicRecord(ushort? declaredLength)
    {
        var name = "PublicFunction\0"u8;
        var bodyLength = 10 + name.Length;
        var length = declaredLength ?? checked((ushort)(sizeof(ushort) + bodyLength));
        var record = new byte[4 + bodyLength];
        BinaryPrimitives.WriteUInt16LittleEndian(record, length);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(2), 0x110E);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(4), 0x2);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(8), 0x30);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(12), 1);
        name.CopyTo(record.AsSpan(14));
        return record;
    }

    private static byte[] BuildPublicsStream(int recordOffset)
    {
        var stream = new byte[32];
        BinaryPrimitives.WriteInt32LittleEndian(stream, 0);
        BinaryPrimitives.WriteInt32LittleEndian(stream.AsSpan(4), sizeof(int));
        BinaryPrimitives.WriteInt32LittleEndian(stream.AsSpan(28), recordOffset);
        return stream;
    }
}
