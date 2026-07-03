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

    /// <summary>A public symbol before RVA resolution.</summary>
    /// <param name="Name">The raw symbol name.</param>
    /// <param name="Segment">The one-based section index.</param>
    /// <param name="Offset">The offset within the section.</param>
    /// <param name="IsFunction">Whether the record carries the function flag.</param>
    internal readonly record struct PublicSymbol(string Name, int Segment, uint Offset, bool IsFunction);

    /// <summary>
    /// Reads the public symbols from the publics and symbol-record streams.
    /// </summary>
    /// <param name="publicsStream">The DBI publics hash stream.</param>
    /// <param name="symbolRecordStream">The global symbol-record stream the address map indexes.</param>
    public static List<PublicSymbol> Read(byte[] publicsStream, byte[] symbolRecordStream)
    {
        var result = new List<PublicSymbol>();
        try
        {
            var pub = publicsStream.AsSpan();
            if (pub.Length < 28) return result;

            // PSGSIHDR: SymHash size, AddrMap size, then thunk fields.
            var symHashSize = BinaryPrimitives.ReadInt32LittleEndian(pub);
            var addrMapSize = BinaryPrimitives.ReadInt32LittleEndian(pub[4..]);
            if (symHashSize < 0 || addrMapSize < 0) return result;

            var addrMapStart = 28 + symHashSize;
            if (addrMapStart < 0 || (long)addrMapStart + addrMapSize > pub.Length) return result;

            var records = symbolRecordStream.AsSpan();
            var count = addrMapSize / 4;
            for (var i = 0; i < count; i++)
            {
                var recordOffset = BinaryPrimitives.ReadInt32LittleEndian(pub[(addrMapStart + i * 4)..]);
                if (recordOffset < 0 || recordOffset + 4 > records.Length) continue;

                var length = BinaryPrimitives.ReadUInt16LittleEndian(records[recordOffset..]);
                var kind = BinaryPrimitives.ReadUInt16LittleEndian(records[(recordOffset + 2)..]);
                if (kind != SPub32 || length < 2) continue;

                var body = records[(recordOffset + 4)..Math.Min(records.Length, recordOffset + 2 + length)];
                if (body.Length < 11) continue; // flags(4), offset(4), segment(2), name

                var flags = BinaryPrimitives.ReadUInt32LittleEndian(body);
                var offset = BinaryPrimitives.ReadUInt32LittleEndian(body[4..]);
                var segment = BinaryPrimitives.ReadUInt16LittleEndian(body[8..]);
                var name = ReadCString(body[10..]);
                if (name.Length > 0)
                    result.Add(new PublicSymbol(name, segment, offset, (flags & PublicSymbolIsFunction) != 0));
            }
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            // Return whatever was read before the stream went out of shape.
        }

        return result;
    }

    private static string ReadCString(ReadOnlySpan<byte> span)
    {
        var end = span.IndexOf((byte)0);
        if (end < 0) end = span.Length;
        return Encoding.UTF8.GetString(span[..end]);
    }
}
