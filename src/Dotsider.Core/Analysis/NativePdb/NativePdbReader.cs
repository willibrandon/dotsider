using System.Buffers.Binary;
using System.Text;

namespace Dotsider.Core.Analysis.NativePdb;

/// <summary>
/// Reads function and data symbols from a Windows native PDB and joins them to the PE image they
/// describe. Orchestrates the MSF container, the DBI stream, the per-module CodeView records, and
/// the section map — resolving each symbol's CodeView <c>(segment, offset)</c> to an RVA, virtual
/// address, and file offset. Also exposes a cheap identity probe that reads only the blocks needed
/// to compare a PDB's GUID and age against the image's debug directory.
/// </summary>
internal static class NativePdbReader
{
    /// <summary>
    /// Reads a PDB's GUID and age with a handful of targeted block reads, without materializing
    /// the whole file. Used to confirm a sidecar matches the image before committing to a full
    /// parse.
    /// </summary>
    /// <param name="path">The PDB file path.</param>
    /// <param name="guid">The PDB info-stream GUID when the read succeeds.</param>
    /// <param name="age">The PDB info-stream age when the read succeeds.</param>
    /// <returns>True when the GUID and age were read.</returns>
    public static bool TryReadPdbId(string path, out Guid guid, out int age)
    {
        guid = default;
        age = 0;
        try
        {
            using var fs = File.OpenRead(path);
            Span<byte> header = stackalloc byte[56];
            if (fs.Read(header) != header.Length) return false;

            var magic = Encoding.ASCII.GetString(header[..24]);
            if (!magic.StartsWith("Microsoft C/C++ MSF 7.00", StringComparison.Ordinal)) return false;

            var blockSize = BinaryPrimitives.ReadInt32LittleEndian(header[32..]);
            if (blockSize is not (512 or 1024 or 2048 or 4096 or 8192)) return false;
            var numDirectoryBytes = BinaryPrimitives.ReadInt32LittleEndian(header[44..]);
            var blockMapAddr = BinaryPrimitives.ReadInt32LittleEndian(header[52..]);
            if (numDirectoryBytes <= 0 || blockMapAddr <= 0) return false;

            var directoryBlockCount = (numDirectoryBytes + blockSize - 1) / blockSize;
            var block = new byte[blockSize];

            // Read the block map, then the directory blocks it points at.
            fs.Position = (long)blockMapAddr * blockSize;
            if (fs.Read(block, 0, blockSize) != blockSize) return false;
            var directory = new byte[directoryBlockCount * blockSize];
            for (var i = 0; i < directoryBlockCount; i++)
            {
                var dirBlock = BinaryPrimitives.ReadInt32LittleEndian(block.AsSpan(i * 4));
                fs.Position = (long)dirBlock * blockSize;
                if (fs.Read(directory, i * blockSize, blockSize) != blockSize) return false;
            }

            // Directory: numStreams, sizes[], then block lists. Stream 1's first block holds the
            // version/signature/age/GUID.
            var dir = directory.AsSpan(0, numDirectoryBytes);
            var p = 0;
            var numStreams = BinaryPrimitives.ReadInt32LittleEndian(dir[p..]);
            p += 4;
            if (numStreams < 2) return false;
            var sizes = new int[numStreams];
            for (var i = 0; i < numStreams; i++)
            {
                var s = BinaryPrimitives.ReadInt32LittleEndian(dir[p..]);
                p += 4;
                sizes[i] = s == unchecked((int)0xFFFFFFFF) ? 0 : s;
            }

            // Skip stream 0's block list to reach stream 1's first block index.
            var stream0Blocks = (sizes[0] + blockSize - 1) / blockSize;
            p += stream0Blocks * 4;
            if (sizes[1] < 28) return false;
            var stream1FirstBlock = BinaryPrimitives.ReadInt32LittleEndian(dir[p..]);

            fs.Position = (long)stream1FirstBlock * blockSize;
            if (fs.Read(block, 0, blockSize) != blockSize) return false;
            age = BinaryPrimitives.ReadInt32LittleEndian(block.AsSpan(8));
            guid = new Guid(block.AsSpan(12, 16));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads the module function and data symbols from a PDB, resolved against the PE image.
    /// Returns an empty list when the container or DBI stream cannot be parsed. Publics from the
    /// global hash stream are read separately.
    /// </summary>
    /// <param name="pdbBytes">The complete PDB file bytes.</param>
    /// <param name="peImageBytes">The PE image the PDB describes, for RVA→file mapping and section fallback.</param>
    public static IReadOnlyList<RawNativeSymbol> Read(byte[] pdbBytes, ReadOnlyMemory<byte> peImageBytes)
    {
        var msf = MsfFile.TryOpen(pdbBytes);
        if (msf is null || msf.StreamCount < 4) return [];

        var dbi = DbiStream.Parse(msf.GetStream(3));
        if (dbi is null) return [];

        var sectionMap = BuildSectionMap(msf, dbi, peImageBytes.Span);
        if (sectionMap is null) return [];

        var names = ResolveNamesStream(msf);
        var imageBase = ReadImageBase(peImageBytes.Span);
        var addressSpace = NativeAddressSpace.Create(peImageBytes.Span);

        var result = new List<RawNativeSymbol>();

        RawNativeSymbol? Resolve(string name, int segment, uint offset, uint size, bool isData, string? file, int? line)
        {
            if (sectionMap.ToRva(segment, offset) is not { } rva) return null;
            var va = imageBase + rva;
            long? fileOffset = addressSpace is not null
                && addressSpace.TryGetFileOffset(va, out var fo, out _) ? fo : null;
            return new RawNativeSymbol(
                Name: name, VirtualAddress: va, Rva: rva, FileOffset: fileOffset,
                Section: sectionMap.SectionName(segment), Size: size,
                IsData: isData || !sectionMap.IsExecutable(segment), IsBoundary: false,
                SourceFile: file, Line: line);
        }

        // Module procedures and data records — the rich source with sizes and line info.
        foreach (var module in dbi.Modules)
        {
            if (module.SymbolStream < 0 || module.SymbolStream >= msf.StreamCount) continue;
            var moduleStream = msf.GetStream(module.SymbolStream);
            foreach (var symbol in CodeViewSymbolReader.ReadModule(moduleStream, module, names))
            {
                if (Resolve(symbol.Name, symbol.Segment, symbol.Offset, symbol.Size, symbol.IsData,
                    symbol.SourceFile, symbol.Line) is { } raw)
                {
                    result.Add(raw);
                }
            }
        }

        // Publics — named symbols without sizes; the merge pass sizes and dedups them against the
        // richer module records above.
        if (dbi.PublicStream >= 0 && dbi.PublicStream < msf.StreamCount
            && dbi.SymbolRecordStream >= 0 && dbi.SymbolRecordStream < msf.StreamCount)
        {
            var publics = PublicsReader.Read(msf.GetStream(dbi.PublicStream), msf.GetStream(dbi.SymbolRecordStream));
            foreach (var pub in publics)
            {
                var isData = !pub.IsFunction && !sectionMap.IsExecutable(pub.Segment);
                if (Resolve(pub.Name, pub.Segment, pub.Offset, 0, isData, null, null) is { } raw)
                    result.Add(raw);
            }
        }

        return result;
    }

    private static PdbSectionMap? BuildSectionMap(MsfFile msf, DbiStream dbi, ReadOnlySpan<byte> peImage)
    {
        if (dbi.SectionHeaderStream >= 0 && dbi.SectionHeaderStream < msf.StreamCount)
        {
            var headers = msf.GetStream(dbi.SectionHeaderStream);
            if (headers.Length >= 40) return PdbSectionMap.FromSectionHeaders(headers);
        }

        // Fall back to the PE image's own section headers.
        var peHeaders = ReadPeSectionHeaders(peImage);
        return peHeaders.Length >= 40 ? PdbSectionMap.FromSectionHeaders(peHeaders) : null;
    }

    private static byte[] ResolveNamesStream(MsfFile msf)
    {
        // Stream 1's named-stream map: u32 stringBytes, the string blob, then a hash table of
        // (nameOffset, streamIndex). Find "/names" and return that stream.
        var info = msf.GetStream(1).AsSpan();
        if (info.Length < 32) return [];

        var stringBytes = BinaryPrimitives.ReadInt32LittleEndian(info[28..]);
        var blobStart = 32;
        if (stringBytes < 0 || blobStart + stringBytes > info.Length) return [];
        var blob = info.Slice(blobStart, stringBytes);

        var p = blobStart + stringBytes;
        if (p + 8 > info.Length) return [];
        var size = BinaryPrimitives.ReadInt32LittleEndian(info[p..]);
        var capacity = BinaryPrimitives.ReadInt32LittleEndian(info[(p + 4)..]);
        p += 8;
        // Present bit vector, then deleted bit vector, then the entries.
        p = SkipBitVector(info, ref p);
        p = SkipBitVector(info, ref p);
        for (var i = 0; i < size && p + 8 <= info.Length; i++)
        {
            var nameOffset = BinaryPrimitives.ReadInt32LittleEndian(info[p..]);
            var streamIndex = BinaryPrimitives.ReadInt32LittleEndian(info[(p + 4)..]);
            p += 8;
            if (nameOffset >= 0 && nameOffset < blob.Length)
            {
                var name = ReadCString(blob[nameOffset..]);
                if (name == "/names" && streamIndex >= 0 && streamIndex < msf.StreamCount)
                    return msf.GetStream(streamIndex);
            }
        }

        _ = capacity;
        return [];
    }

    private static int SkipBitVector(ReadOnlySpan<byte> span, ref int p)
    {
        if (p + 4 > span.Length) return p;
        var words = BinaryPrimitives.ReadInt32LittleEndian(span[p..]);
        p += 4 + Math.Max(0, words) * 4;
        return p;
    }

    private static ulong ReadImageBase(ReadOnlySpan<byte> pe)
    {
        try
        {
            if (pe.Length < 0x40 || pe[0] != (byte)'M' || pe[1] != (byte)'Z') return 0;
            var peHeader = BinaryPrimitives.ReadInt32LittleEndian(pe[0x3C..]);
            if (peHeader <= 0 || peHeader + 24 > pe.Length) return 0;
            if (BinaryPrimitives.ReadUInt32LittleEndian(pe[peHeader..]) != 0x0000_4550) return 0;
            var optional = peHeader + 24;
            var magic = BinaryPrimitives.ReadUInt16LittleEndian(pe[optional..]);
            return magic == 0x20B
                ? BinaryPrimitives.ReadUInt64LittleEndian(pe[(optional + 24)..])
                : BinaryPrimitives.ReadUInt32LittleEndian(pe[(optional + 28)..]);
        }
        catch (ArgumentOutOfRangeException)
        {
            return 0;
        }
    }

    private static byte[] ReadPeSectionHeaders(ReadOnlySpan<byte> pe)
    {
        try
        {
            if (pe.Length < 0x40 || pe[0] != (byte)'M' || pe[1] != (byte)'Z') return [];
            var peHeader = BinaryPrimitives.ReadInt32LittleEndian(pe[0x3C..]);
            if (peHeader <= 0 || peHeader + 24 > pe.Length) return [];
            if (BinaryPrimitives.ReadUInt32LittleEndian(pe[peHeader..]) != 0x0000_4550) return [];
            var sectionCount = BinaryPrimitives.ReadUInt16LittleEndian(pe[(peHeader + 6)..]);
            var optionalSize = BinaryPrimitives.ReadUInt16LittleEndian(pe[(peHeader + 20)..]);
            var sectionTable = peHeader + 24 + optionalSize;
            var byteCount = sectionCount * 40;
            if (sectionTable < 0 || sectionTable + byteCount > pe.Length) return [];
            return pe.Slice(sectionTable, byteCount).ToArray();
        }
        catch (ArgumentOutOfRangeException)
        {
            return [];
        }
    }

    private static string ReadCString(ReadOnlySpan<byte> span)
    {
        var end = span.IndexOf((byte)0);
        if (end < 0) end = span.Length;
        return Encoding.UTF8.GetString(span[..end]);
    }
}
