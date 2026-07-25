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
            fs.ReadExactly(header);
            if (!MsfSuperBlock.TryRead(header, fs.Length, out var superBlock))
            {
                return false;
            }

            var block = new byte[superBlock.BlockSize];
            if (!TryReadBlock(fs, superBlock, superBlock.BlockMapAddress, block))
            {
                return false;
            }

            var directory = new byte[superBlock.DirectoryByteCount];
            for (var i = 0; i < superBlock.DirectoryBlockCount; i++)
            {
                var directoryBlock = BinaryPrimitives.ReadUInt32LittleEndian(
                    block.AsSpan(i * sizeof(uint)));
                if (!superBlock.TryGetBlockOffset(directoryBlock, out var directoryBlockOffset))
                {
                    return false;
                }

                var destinationOffset = i * superBlock.BlockSize;
                var byteCount = Math.Min(
                    superBlock.BlockSize,
                    superBlock.DirectoryByteCount - destinationOffset);
                fs.Position = directoryBlockOffset;
                fs.ReadExactly(directory.AsSpan(destinationOffset, byteCount));
            }

            // Directory: numStreams, sizes[], then block lists. Stream 1's first block holds the
            // version/signature/age/GUID.
            var dir = directory.AsSpan();
            var p = 0;
            if (!TryReadUInt32(dir, ref p, out var streamCount)
                || streamCount < 2
                || !NativeImageRange.TryGetTable(
                    dir.Length,
                    (ulong)p,
                    streamCount,
                    sizeof(uint),
                    sizeof(uint),
                    out var sizeTableOffset,
                    out var sizeTableLength))
            {
                return false;
            }

            var sizes = dir.Slice(sizeTableOffset, sizeTableLength);
            var stream0Size = BinaryPrimitives.ReadUInt32LittleEndian(sizes);
            var stream1Size = BinaryPrimitives.ReadUInt32LittleEndian(sizes[sizeof(uint)..]);
            if (stream0Size == uint.MaxValue
                || stream1Size is uint.MaxValue or < 28
                || !superBlock.TryGetStreamBlockCount(stream0Size, out var stream0BlockCount)
                || !superBlock.TryGetStreamBlockCount(stream1Size, out var stream1BlockCount)
                || stream1BlockCount == 0)
            {
                return false;
            }

            // Skip stream 0's block list to reach stream 1's first block index.
            var blockListOffset = (ulong)sizeTableOffset + (ulong)sizeTableLength;
            if (!NativeImageRange.TryAdd(
                    blockListOffset,
                    (ulong)stream0BlockCount * sizeof(uint),
                    out var stream1BlockOffset)
                || !NativeImageRange.TryGet(
                    dir.Length,
                    stream1BlockOffset,
                    sizeof(uint),
                    out var stream1BlockFileOffset,
                    out _))
            {
                return false;
            }

            var stream1FirstBlock = BinaryPrimitives.ReadUInt32LittleEndian(
                dir[stream1BlockFileOffset..]);
            if (!TryReadBlock(fs, superBlock, stream1FirstBlock, block))
            {
                return false;
            }

            age = BinaryPrimitives.ReadInt32LittleEndian(block.AsSpan(8));
            guid = new Guid(block.AsSpan(12, 16));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException)
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
        => TryRead(pdbBytes, peImageBytes, out var symbols) ? symbols : [];

    /// <summary>
    /// Tries to read the module and public symbols from a PDB resolved against its PE image.
    /// </summary>
    /// <param name="pdbBytes">The complete PDB file bytes.</param>
    /// <param name="peImageBytes">The PE image the PDB describes.</param>
    /// <param name="symbols">The symbols read from a structurally valid PDB.</param>
    /// <returns><see langword="true"/> when every required container and symbol range is valid.</returns>
    public static bool TryRead(
        byte[] pdbBytes,
        ReadOnlyMemory<byte> peImageBytes,
        out IReadOnlyList<RawNativeSymbol> symbols)
    {
        symbols = [];
        var msf = MsfFile.TryOpen(pdbBytes);
        if (msf is null || msf.StreamCount < 4) return false;

        var dbi = DbiStream.Parse(msf.GetStream(3));
        if (dbi is null) return false;

        var sectionMap = BuildSectionMap(msf, dbi, peImageBytes.Span);
        if (sectionMap is null) return false;

        var names = ResolveNamesStream(msf);
        var imageBase = ReadImageBase(peImageBytes.Span);
        var addressSpace = NativeAddressSpace.Create(peImageBytes.Span);

        var result = new List<RawNativeSymbol>();

        RawNativeSymbol? Resolve(string name, int segment, uint offset, uint size, bool isData, string? file, int? line)
        {
            if (sectionMap.ToRva(segment, offset) is not { } rva) return null;
            if (!NativeImageRange.TryAdd(imageBase, rva, out var va)) return null;
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
            if (module.SymbolStream < 0)
            {
                continue;
            }

            if (module.SymbolStream >= msf.StreamCount)
            {
                return false;
            }

            var moduleStream = msf.GetStream(module.SymbolStream);
            if (!CodeViewSymbolReader.TryReadModule(moduleStream, module, names, out var moduleSymbols))
            {
                return false;
            }

            foreach (var symbol in moduleSymbols)
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
        if ((dbi.PublicStream < 0) != (dbi.SymbolRecordStream < 0))
        {
            return false;
        }

        if (dbi.PublicStream >= 0)
        {
            if (dbi.PublicStream >= msf.StreamCount
                || dbi.SymbolRecordStream >= msf.StreamCount
                || !PublicsReader.TryRead(
                    msf.GetStream(dbi.PublicStream),
                    msf.GetStream(dbi.SymbolRecordStream),
                    out var publics))
            {
                return false;
            }

            foreach (var pub in publics)
            {
                var isData = !pub.IsFunction && !sectionMap.IsExecutable(pub.Segment);
                if (Resolve(pub.Name, pub.Segment, pub.Offset, 0, isData, null, null) is { } raw)
                    result.Add(raw);
            }
        }

        symbols = result;
        return true;
    }

    private static PdbSectionMap? BuildSectionMap(MsfFile msf, DbiStream dbi, ReadOnlySpan<byte> peImage)
    {
        if (dbi.SectionHeaderStream >= 0 && dbi.SectionHeaderStream < msf.StreamCount)
        {
            var headers = msf.GetStream(dbi.SectionHeaderStream);
            if (headers.Length >= 40) return PdbSectionMap.FromSectionHeaders(headers);
            return null;
        }

        if (dbi.SectionHeaderStream >= msf.StreamCount)
        {
            return null;
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

        var stringByteSize = BinaryPrimitives.ReadUInt32LittleEndian(info[28..]);
        if (!NativeImageRange.TryGet(
                info.Length,
                32,
                stringByteSize,
                out var blobOffset,
                out var blobLength)
            || !NativeImageRange.TryAdd(32, stringByteSize, out var hashTableOffset)
            || !NativeImageRange.TryGet(
                info.Length,
                hashTableOffset,
                2 * sizeof(uint),
                out var p,
                out _))
        {
            return [];
        }

        var blob = info.Slice(blobOffset, blobLength);
        var size = BinaryPrimitives.ReadUInt32LittleEndian(info[p..]);
        var capacity = BinaryPrimitives.ReadUInt32LittleEndian(info[(p + sizeof(uint))..]);
        p += 2 * sizeof(uint);
        if (size > capacity)
        {
            return [];
        }

        // Present bit vector, then deleted bit vector, then the entries.
        if (!TrySkipBitVector(info, ref p)
            || !TrySkipBitVector(info, ref p)
            || !NativeImageRange.TryGetTable(
                info.Length,
                (ulong)p,
                size,
                2 * sizeof(uint),
                2 * sizeof(uint),
                out _,
                out _))
        {
            return [];
        }

        for (uint i = 0; i < size; i++)
        {
            var nameOffset = BinaryPrimitives.ReadUInt32LittleEndian(info[p..]);
            var streamIndex = BinaryPrimitives.ReadUInt32LittleEndian(
                info[(p + sizeof(uint))..]);
            p += 2 * sizeof(uint);
            if (nameOffset < (uint)blob.Length && streamIndex <= int.MaxValue)
            {
                var name = ReadCString(blob[(int)nameOffset..]);
                if (name == "/names" && streamIndex < (uint)msf.StreamCount)
                    return msf.GetStream((int)streamIndex);
            }
        }

        return [];
    }

    private static bool TrySkipBitVector(ReadOnlySpan<byte> span, ref int offset)
    {
        if (!NativeImageRange.TryGet(
                span.Length,
                (ulong)offset,
                sizeof(uint),
                out var wordCountOffset,
                out _))
        {
            return false;
        }

        var wordCount = BinaryPrimitives.ReadUInt32LittleEndian(span[wordCountOffset..]);
        if (!NativeImageRange.TryAdd(
                (ulong)wordCountOffset + sizeof(uint),
                (ulong)wordCount * sizeof(uint),
                out var end)
            || end > (ulong)span.Length)
        {
            return false;
        }

        offset = (int)end;
        return true;
    }

    private static ulong ReadImageBase(ReadOnlySpan<byte> pe)
    {
        if (pe.Length < 0x40 || pe[0] != (byte)'M' || pe[1] != (byte)'Z')
        {
            return 0;
        }

        var peHeader = BinaryPrimitives.ReadUInt32LittleEndian(pe[0x3C..]);
        if (!NativeImageRange.TryGet(
                pe.Length,
                peHeader,
                24,
                out var containedPeHeader,
                out _)
            || BinaryPrimitives.ReadUInt32LittleEndian(pe[containedPeHeader..]) != 0x0000_4550)
        {
            return 0;
        }

        var optional = (ulong)peHeader + 24;
        if (!NativeImageRange.TryGet(
                pe.Length,
                optional,
                32,
                out var containedOptional,
                out _))
        {
            return 0;
        }

        var magic = BinaryPrimitives.ReadUInt16LittleEndian(pe[containedOptional..]);
        return magic switch
        {
            0x20B => BinaryPrimitives.ReadUInt64LittleEndian(pe[(containedOptional + 24)..]),
            0x10B => BinaryPrimitives.ReadUInt32LittleEndian(pe[(containedOptional + 28)..]),
            _ => 0,
        };
    }

    private static byte[] ReadPeSectionHeaders(ReadOnlySpan<byte> pe)
    {
        if (pe.Length < 0x40 || pe[0] != (byte)'M' || pe[1] != (byte)'Z')
        {
            return [];
        }

        var peHeader = BinaryPrimitives.ReadUInt32LittleEndian(pe[0x3C..]);
        if (!NativeImageRange.TryGet(
                pe.Length,
                peHeader,
                24,
                out var containedPeHeader,
                out _)
            || BinaryPrimitives.ReadUInt32LittleEndian(pe[containedPeHeader..]) != 0x0000_4550)
        {
            return [];
        }

        var sectionCount = BinaryPrimitives.ReadUInt16LittleEndian(
            pe[(containedPeHeader + 6)..]);
        var optionalSize = BinaryPrimitives.ReadUInt16LittleEndian(
            pe[(containedPeHeader + 20)..]);
        var sectionTable = (ulong)peHeader + 24 + optionalSize;
        if (!NativeImageRange.TryGetTable(
                pe.Length,
                sectionTable,
                sectionCount,
                40,
                40,
                out var containedSectionTable,
                out var sectionTableByteSize))
        {
            return [];
        }

        return pe.Slice(containedSectionTable, sectionTableByteSize).ToArray();
    }

    private static string ReadCString(ReadOnlySpan<byte> span)
    {
        var end = span.IndexOf((byte)0);
        if (end < 0) end = span.Length;
        return Encoding.UTF8.GetString(span[..end]);
    }

    private static bool TryReadBlock(
        FileStream stream,
        MsfSuperBlock superBlock,
        uint blockIndex,
        Span<byte> destination)
    {
        if (destination.Length != superBlock.BlockSize
            || !superBlock.TryGetBlockOffset(blockIndex, out var fileOffset))
        {
            return false;
        }

        stream.Position = fileOffset;
        stream.ReadExactly(destination);
        return true;
    }

    private static bool TryReadUInt32(ReadOnlySpan<byte> span, ref int offset, out uint value)
    {
        if (offset < 0 || offset > span.Length - sizeof(uint))
        {
            value = 0;
            return false;
        }

        value = BinaryPrimitives.ReadUInt32LittleEndian(span[offset..]);
        offset += sizeof(uint);
        return true;
    }
}
