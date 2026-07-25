using System.Buffers.Binary;
using System.Text;

namespace Dotsider.Core.Analysis.NativePdb;

/// <summary>
/// Reads the <c>S_PUB32</c> public symbols of a PDB. Publics are reached indirectly: the DBI's
/// publics stream begins with a header whose address map is a sorted list of offsets into the
/// global symbol-record stream, and each offset points at an <c>S_PUB32</c> record. Native AOT
/// managed publics in executable sections frequently carry no flags, so a public is treated as a
/// function when it either has the function flag or lands in an executable section — filtering on
/// the flag alone would drop most managed method publics.
/// </summary>
internal static class PublicsReader
{
    private const ushort SPub32 = 0x110E;
    private const uint PublicSymbolIsFunction = 0x2;

    /// <summary>
    /// Reads the public symbols from the publics and symbol-record streams.
    /// </summary>
    /// <param name="publicsStream">The DBI publics hash stream.</param>
    /// <param name="symbolRecordStream">The global symbol-record stream the address map indexes.</param>
    public static List<PublicSymbol> Read(byte[] publicsStream, byte[] symbolRecordStream)
        => TryRead(publicsStream, symbolRecordStream, out var symbols) ? symbols : [];

    /// <summary>
    /// Tries to read the public symbols from the publics and symbol-record streams.
    /// </summary>
    /// <param name="publicsStream">The DBI publics hash stream.</param>
    /// <param name="symbolRecordStream">The global symbol-record stream the address map indexes.</param>
    /// <param name="symbols">The symbols read from structurally valid streams.</param>
    /// <returns><see langword="true"/> when every declared range and record is valid.</returns>
    public static bool TryRead(
        byte[] publicsStream,
        byte[] symbolRecordStream,
        out List<PublicSymbol> symbols)
    {
        symbols = [];
        var publics = publicsStream.AsSpan();
        if (publics.Length < 28)
        {
            return false;
        }

        // PSGSIHDR: SymHash size, AddrMap size, then thunk fields.
        var symbolHashByteSize = BinaryPrimitives.ReadUInt32LittleEndian(publics);
        var addressMapByteSize = BinaryPrimitives.ReadUInt32LittleEndian(publics[4..]);
        if (addressMapByteSize % sizeof(uint) != 0
            || !NativeImageRange.TryAdd(28, symbolHashByteSize, out var addressMapOffset)
            || !NativeImageRange.TryGet(
                publics.Length,
                addressMapOffset,
                addressMapByteSize,
                out var containedAddressMapOffset,
                out var containedAddressMapByteSize))
        {
            return false;
        }

        var records = symbolRecordStream.AsSpan();
        var count = containedAddressMapByteSize / sizeof(uint);
        for (var i = 0; i < count; i++)
        {
            var recordOffset = BinaryPrimitives.ReadUInt32LittleEndian(
                publics[(containedAddressMapOffset + i * sizeof(uint))..]);
            if (!NativeImageRange.TryGet(
                    records.Length,
                    recordOffset,
                    2 * sizeof(ushort),
                    out var containedRecordOffset,
                    out _))
            {
                symbols = [];
                return false;
            }

            var length = BinaryPrimitives.ReadUInt16LittleEndian(records[containedRecordOffset..]);
            var kind = BinaryPrimitives.ReadUInt16LittleEndian(
                records[(containedRecordOffset + sizeof(ushort))..]);
            if (length < sizeof(ushort)
                || !NativeImageRange.TryGet(
                    records.Length,
                    recordOffset,
                    (ulong)sizeof(ushort) + length,
                    out _,
                    out var recordByteSize))
            {
                symbols = [];
                return false;
            }

            if (kind != SPub32)
            {
                continue;
            }

            var body = records.Slice(containedRecordOffset + 4, recordByteSize - 4);
            if (body.Length < 11)
            {
                symbols = [];
                return false;
            }

            var flags = BinaryPrimitives.ReadUInt32LittleEndian(body);
            var offset = BinaryPrimitives.ReadUInt32LittleEndian(body[4..]);
            var segment = BinaryPrimitives.ReadUInt16LittleEndian(body[8..]);
            if (!TryReadCString(body[10..], out var name))
            {
                symbols = [];
                return false;
            }

            if (name.Length > 0)
            {
                symbols.Add(new PublicSymbol(
                    name,
                    segment,
                    offset,
                    (flags & PublicSymbolIsFunction) != 0));
            }
        }

        return true;
    }

    private static bool TryReadCString(ReadOnlySpan<byte> span, out string value)
    {
        var end = span.IndexOf((byte)0);
        if (end < 0)
        {
            value = string.Empty;
            return false;
        }

        value = Encoding.UTF8.GetString(span[..end]);
        return true;
    }
}
