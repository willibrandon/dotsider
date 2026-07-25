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
    /// <summary>Reads the RSDS CodeView identity, or null when the image has none.</summary>
    /// <param name="pe">The raw PE image bytes.</param>
    public static CodeViewId? TryRead(ReadOnlySpan<byte> pe)
    {
        if (pe.Length < 0x40 || pe[0] != (byte)'M' || pe[1] != (byte)'Z') return null;
        var peHeaderValue = BinaryPrimitives.ReadUInt32LittleEndian(pe[0x3C..]);
        if (!NativeImageRange.TryGet(
                pe.Length,
                peHeaderValue,
                24,
                out var peHeader,
                out _)
            || BinaryPrimitives.ReadUInt32LittleEndian(pe[peHeader..]) != 0x0000_4550)
        {
            return null;
        }

        var optionalSize = BinaryPrimitives.ReadUInt16LittleEndian(pe[(peHeader + 20)..]);
        var optional = peHeader + 24;
        if (!NativeImageRange.TryGet(
            pe.Length,
            optional,
            optionalSize,
            out _,
            out _)
            || optionalSize < sizeof(ushort))
            return null;

        var magic = BinaryPrimitives.ReadUInt16LittleEndian(pe[optional..]);
        var directoriesOffset = magic switch
        {
            0x10B => 96,
            0x20B => 112,
            _ => 0,
        };
        const int debugDirectoryIndex = 6;
        const int directoryEntrySize = 8;
        var debugEntryOffset = directoriesOffset + debugDirectoryIndex * directoryEntrySize;
        if (directoriesOffset == 0
            || optionalSize < debugEntryOffset + directoryEntrySize)
            return null;

        var debugEntry = optional + debugEntryOffset;
        var debugRva = BinaryPrimitives.ReadUInt32LittleEndian(pe[debugEntry..]);
        var debugSize = BinaryPrimitives.ReadUInt32LittleEndian(pe[(debugEntry + 4)..]);
        if (debugRva == 0 || debugSize == 0 || debugSize % 28 != 0) return null;

        var imageBase = magic == 0x20B
            ? BinaryPrimitives.ReadUInt64LittleEndian(pe[(optional + 24)..])
            : BinaryPrimitives.ReadUInt32LittleEndian(pe[(optional + 28)..]);
        var addressSpace = NativeAddressSpace.Create(pe);
        if (addressSpace is null
            || !NativeImageRange.TryAdd(imageBase, debugRva, out var debugAddress)
            || !addressSpace.TryGetFileOffset(
                debugAddress,
                out var tableOffset,
                out var available)
            || debugSize > (uint)available)
        {
            return null;
        }

        // Walk the 28-byte IMAGE_DEBUG_DIRECTORY entries for a CodeView (type 2) RSDS record.
        var table = pe.Slice(tableOffset, (int)debugSize);
        for (var offset = 0; offset < table.Length; offset += 28)
        {
            var entry = table[offset..];
            var type = BinaryPrimitives.ReadUInt32LittleEndian(entry[12..]);
            if (type != 2) continue; // IMAGE_DEBUG_TYPE_CODEVIEW

            var dataSize = BinaryPrimitives.ReadUInt32LittleEndian(entry[16..]);
            var dataOffset = BinaryPrimitives.ReadUInt32LittleEndian(entry[24..]);
            if (dataSize < 24
                || !NativeImageRange.TryGet(
                    pe.Length,
                    dataOffset,
                    dataSize,
                    out var codeViewOffset,
                    out var codeViewSize))
                continue;

            var codeView = pe.Slice(codeViewOffset, codeViewSize);
            if (BinaryPrimitives.ReadUInt32LittleEndian(codeView) != 0x5344_5352) continue; // "RSDS"
            var guid = new Guid(codeView.Slice(4, 16));
            var age = BinaryPrimitives.ReadInt32LittleEndian(codeView[20..]);
            var path = ReadCString(codeView[24..]);
            if (path is null) continue;
            return new CodeViewId(guid, age, path);
        }

        return null;
    }

    private static string? ReadCString(ReadOnlySpan<byte> span)
    {
        var end = span.IndexOf((byte)0);
        if (end < 0) return null;
        return Encoding.UTF8.GetString(span[..end]);
    }
}
