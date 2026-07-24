using Dotsider.Core.Analysis.Wasm;

namespace Dotsider.Tests;

/// <summary>
/// Verifies bounded WebAssembly reads and the binary format's fixed-width LEB128 limits.
/// </summary>
[TestClass]
public sealed class WasmDataReaderTests
{
    /// <summary>
    /// A u32 accepts its largest value and legal non-minimal five-byte encodings.
    /// </summary>
    [TestMethod]
    public void ReadUnsignedLeb12832_LegalFiveByteEncodings_ReturnValues()
    {
        var maximumReader = new WasmDataReader([0xFF, 0xFF, 0xFF, 0xFF, 0x0F]);
        var nonMinimalReader = new WasmDataReader([0x80, 0x80, 0x80, 0x80, 0x00]);

        Assert.AreEqual(uint.MaxValue, maximumReader.ReadUnsignedLeb12832());
        Assert.IsTrue(maximumReader.AtEnd);
        Assert.AreEqual(0U, nonMinimalReader.ReadUnsignedLeb12832());
        Assert.IsTrue(nonMinimalReader.AtEnd);
    }

    /// <summary>
    /// A u32 rejects payload bits outside its 32-bit range and a sixth encoded byte.
    /// </summary>
    [TestMethod]
    public void ReadUnsignedLeb12832_InvalidFifthOrSixthByte_ThrowsInvalidDataException()
    {
        Assert.ThrowsExactly<InvalidDataException>(
            () => ReadUnsignedLeb12832([0x80, 0x80, 0x80, 0x80, 0x10]));
        Assert.ThrowsExactly<InvalidDataException>(
            () => ReadUnsignedLeb12832([0x80, 0x80, 0x80, 0x80, 0x80, 0x00]));
    }

    /// <summary>
    /// Signed and unsigned 64-bit values accept their legal ten-byte boundary encodings.
    /// </summary>
    [TestMethod]
    public void ReadLeb12864_LegalTenByteEncodings_ReturnValues()
    {
        var unsignedReader = new WasmDataReader(
            [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x01]);
        var signedMaximumReader = new WasmDataReader(
            [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x00]);
        var signedMinimumReader = new WasmDataReader(
            [0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x7F]);

        Assert.AreEqual(ulong.MaxValue, unsignedReader.ReadUnsignedLeb12864());
        Assert.AreEqual(long.MaxValue, signedMaximumReader.ReadSignedLeb128());
        Assert.AreEqual(long.MinValue, signedMinimumReader.ReadSignedLeb128());
    }

    /// <summary>
    /// Signed and unsigned 64-bit values reject invalid payload bits in their tenth byte.
    /// </summary>
    [TestMethod]
    public void ReadLeb12864_InvalidTenthByte_ThrowsInvalidDataException()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => ReadUnsignedLeb12864(
            [0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x02]));
        Assert.ThrowsExactly<InvalidDataException>(() => ReadSignedLeb128(
            [0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x01]));
    }

    /// <summary>
    /// A child reader cannot consume bytes that follow its declared containing region.
    /// </summary>
    [TestMethod]
    public void ReadSubReader_ReadPastDeclaredRegion_ThrowsInvalidDataException()
    {
        var reader = new WasmDataReader([0x01, 0x02, 0x03]);
        var child = reader.ReadSubReader(1);

        Assert.AreEqual((byte)0x01, child.ReadByte());
        Assert.ThrowsExactly<InvalidDataException>(() => ReadPastChildBoundary());
        Assert.AreEqual((byte)0x02, reader.ReadByte());
    }

    private static byte ReadPastChildBoundary()
    {
        var reader = new WasmDataReader([0x01, 0x02]);
        var child = reader.ReadSubReader(1);
        _ = child.ReadByte();
        return child.ReadByte();
    }

    private static uint ReadUnsignedLeb12832(byte[] bytes)
    {
        var reader = new WasmDataReader(bytes);
        return reader.ReadUnsignedLeb12832();
    }

    private static ulong ReadUnsignedLeb12864(byte[] bytes)
    {
        var reader = new WasmDataReader(bytes);
        return reader.ReadUnsignedLeb12864();
    }

    private static long ReadSignedLeb128(byte[] bytes)
    {
        var reader = new WasmDataReader(bytes);
        return reader.ReadSignedLeb128();
    }
}
