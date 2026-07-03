using System.Buffers.Binary;
using System.Text;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Extracts the RSDS CodeView debug-directory entry from a PE image's raw bytes — the PDB GUID,
/// age, and recorded path a Windows native PDB is matched against. Reads the bytes directly so the
/// native-symbol facade needs no <c>PEReader</c>. Returns null when the image has no RSDS entry.
/// </summary>
internal static class PeCodeView
{
    /// <summary>The RSDS identity of a PE image.</summary>
    /// <param name="Guid">The PDB GUID.</param>
    /// <param name="Age">The PDB age.</param>
    /// <param name="PdbPath">The recorded PDB path (its file name is used for same-directory probing).</param>
    internal readonly record struct CodeViewId(Guid Guid, int Age, string PdbPath);

    /// <summary>Reads the RSDS CodeView identity, or null when the image has none.</summary>
    /// <param name="pe">The raw PE image bytes.</param>
    public static CodeViewId? TryRead(ReadOnlySpan<byte> pe)
    {
        try
        {
            if (pe.Length < 0x40 || pe[0] != (byte)'M' || pe[1] != (byte)'Z') return null;
            var peHeader = BinaryPrimitives.ReadInt32LittleEndian(pe[0x3C..]);
            if (peHeader <= 0 || peHeader + 24 > pe.Length) return null;
            if (BinaryPrimitives.ReadUInt32LittleEndian(pe[peHeader..]) != 0x0000_4550) return null;

            var optional = peHeader + 24;
            var magic = BinaryPrimitives.ReadUInt16LittleEndian(pe[optional..]);
            var directoriesStart = optional + (magic == 0x20B ? 112 : 96);
            const int debugDirectoryIndex = 6;
            var debugEntry = directoriesStart + debugDirectoryIndex * 8;
            if (debugEntry + 8 > pe.Length) return null;

            var debugRva = BinaryPrimitives.ReadUInt32LittleEndian(pe[debugEntry..]);
            var debugSize = BinaryPrimitives.ReadUInt32LittleEndian(pe[(debugEntry + 4)..]);
            if (debugRva == 0 || debugSize == 0) return null;

            var addressSpace = NativeAddressSpace.Create(pe);
            if (addressSpace is null
                || !addressSpace.TryGetFileOffset(ReadImageBase(pe) + debugRva, out var tableOffset, out _))
            {
                return null;
            }

            // Walk the 28-byte IMAGE_DEBUG_DIRECTORY entries for a CodeView (type 2) RSDS record.
            for (var offset = tableOffset; offset + 28 <= pe.Length && offset < tableOffset + (int)debugSize; offset += 28)
            {
                var type = BinaryPrimitives.ReadUInt32LittleEndian(pe[(offset + 12)..]);
                if (type != 2) continue; // IMAGE_DEBUG_TYPE_CODEVIEW
                var pointerToRawData = (int)BinaryPrimitives.ReadUInt32LittleEndian(pe[(offset + 24)..]);
                if (pointerToRawData <= 0 || pointerToRawData + 24 > pe.Length) continue;

                if (BinaryPrimitives.ReadUInt32LittleEndian(pe[pointerToRawData..]) != 0x5344_5352) continue; // "RSDS"
                var guid = new Guid(pe.Slice(pointerToRawData + 4, 16));
                var age = BinaryPrimitives.ReadInt32LittleEndian(pe[(pointerToRawData + 20)..]);
                var path = ReadCString(pe[(pointerToRawData + 24)..]);
                return new CodeViewId(guid, age, path);
            }

            return null;
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static ulong ReadImageBase(ReadOnlySpan<byte> pe)
    {
        var peHeader = BinaryPrimitives.ReadInt32LittleEndian(pe[0x3C..]);
        var optional = peHeader + 24;
        var magic = BinaryPrimitives.ReadUInt16LittleEndian(pe[optional..]);
        return magic == 0x20B
            ? BinaryPrimitives.ReadUInt64LittleEndian(pe[(optional + 24)..])
            : BinaryPrimitives.ReadUInt32LittleEndian(pe[(optional + 28)..]);
    }

    private static string ReadCString(ReadOnlySpan<byte> span)
    {
        var end = span.IndexOf((byte)0);
        if (end < 0) end = span.Length;
        return Encoding.UTF8.GetString(span[..end]);
    }
}
