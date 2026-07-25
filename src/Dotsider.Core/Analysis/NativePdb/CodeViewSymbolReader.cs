using System.Buffers.Binary;
using System.Text;

namespace Dotsider.Core.Analysis.NativePdb;

/// <summary>
/// Walks a PDB module's symbol stream for function records (<c>S_GPROC32</c>/<c>S_LPROC32</c>)
/// and data records (<c>S_GDATA32</c>/<c>S_LDATA32</c>), and joins the C13 line subsections that
/// follow the symbols to attribute each function to a source file and line. The module stream is
/// laid out as <c>[u32 CodeView signature][symbol records][C11 lines][C13 lines]</c>; symbol
/// records span <c>[4, SymByteSize)</c> and the C13 block begins at <c>SymByteSize + C11ByteSize</c>.
/// </summary>
internal static class CodeViewSymbolReader
{
    private const ushort SLProc32 = 0x110F;
    private const ushort SGProc32 = 0x1110;
    private const ushort SLData32 = 0x110C;
    private const ushort SGData32 = 0x110D;
    private const uint DebugSLines = 0xF2;
    private const uint DebugSFileChecksums = 0xF4;
    private const uint NamesSignature = 0xEFFE_EFFE;

    /// <summary>
    /// Reads the function and data symbols of one module, attributing source file and line from
    /// the module's C13 line subsections.
    /// </summary>
    /// <param name="moduleStream">The module's full symbol stream.</param>
    /// <param name="module">The module descriptor giving the symbol and line block sizes.</param>
    /// <param name="names">The <c>/names</c> string table stream, for resolving file names.</param>
    public static List<CodeViewModuleSymbol> ReadModule(
        byte[] moduleStream, DbiModule module, byte[] names)
        => TryReadModule(moduleStream, module, names, out var symbols) ? symbols : [];

    /// <summary>
    /// Tries to read the function and data symbols of one module.
    /// </summary>
    /// <param name="moduleStream">The module's full symbol stream.</param>
    /// <param name="module">The module descriptor giving the symbol and line block sizes.</param>
    /// <param name="names">The <c>/names</c> string table stream, for resolving file names.</param>
    /// <param name="symbols">The symbols read from a structurally valid module.</param>
    /// <returns><see langword="true"/> when every declared module range is valid.</returns>
    public static bool TryReadModule(
        byte[] moduleStream,
        DbiModule module,
        byte[] names,
        out List<CodeViewModuleSymbol> symbols)
    {
        symbols = [];
        var span = moduleStream.AsSpan();
        if (module.SymbolStream < 0
            || module.SymbolByteSize <= sizeof(uint)
            || module.C11ByteSize != 0 && module.C13ByteSize != 0
            || !NativeImageRange.TryAdd(
                module.SymbolByteSize,
                module.C11ByteSize,
                out var lineDataEnd)
            || !NativeImageRange.TryAdd(
                lineDataEnd,
                module.C13ByteSize,
                out var moduleDataEnd)
            || !NativeImageRange.TryGet(
                span.Length,
                0,
                moduleDataEnd,
                out _,
                out _)
            || !NativeImageRange.TryGet(
                span.Length,
                0,
                module.SymbolByteSize,
                out _,
                out var symbolByteSize)
            || !TryParseLineTable(moduleStream, module, names, out var lines))
        {
            return false;
        }

        var p = 4; // past the CodeView signature
        while (p < symbolByteSize)
        {
            if (p > symbolByteSize - sizeof(uint))
            {
                symbols = [];
                return false;
            }

            var length = BinaryPrimitives.ReadUInt16LittleEndian(span[p..]);
            if (length < sizeof(ushort)
                || !NativeImageRange.TryGet(
                    symbolByteSize,
                    (ulong)p,
                    (ulong)sizeof(ushort) + length,
                    out _,
                    out var recordByteSize))
            {
                symbols = [];
                return false;
            }

            var kind = BinaryPrimitives.ReadUInt16LittleEndian(span[(p + 2)..]);
            var body = span.Slice(p + 4, recordByteSize - 4);

            if (kind is SGProc32 or SLProc32)
            {
                // parent,end,next,codeSize,dbgStart,dbgEnd,typeIndex,offset (8×4), segment(2), flags(1), name
                if (body.Length < 35
                    || !TryReadCString(body[35..], out var name))
                {
                    symbols = [];
                    return false;
                }

                var codeSize = BinaryPrimitives.ReadUInt32LittleEndian(body[12..]);
                var offset = BinaryPrimitives.ReadUInt32LittleEndian(body[28..]);
                var segment = BinaryPrimitives.ReadUInt16LittleEndian(body[32..]);
                if (name.Length > 0)
                {
                    var (file, line) = lines.GetValueOrDefault((segment, offset));
                    symbols.Add(new CodeViewModuleSymbol(
                        name,
                        segment,
                        offset,
                        codeSize,
                        false,
                        file,
                        line));
                }
            }
            else if (kind is SGData32 or SLData32)
            {
                // typeIndex(4), offset(4), segment(2), name
                if (body.Length < 10
                    || !TryReadCString(body[10..], out var name))
                {
                    symbols = [];
                    return false;
                }

                var offset = BinaryPrimitives.ReadUInt32LittleEndian(body[4..]);
                var segment = BinaryPrimitives.ReadUInt16LittleEndian(body[8..]);
                if (name.Length > 0)
                {
                    symbols.Add(new CodeViewModuleSymbol(
                        name,
                        segment,
                        offset,
                        0,
                        true,
                        null,
                        null));
                }
            }

            p += recordByteSize;
        }

        return true;
    }

    private static bool TryParseLineTable(
        byte[] moduleStream,
        DbiModule module,
        byte[] names,
        out Dictionary<(int Segment, uint Offset), (string? File, int? Line)> table)
    {
        table = [];
        if (module.C13ByteSize == 0)
        {
            return true;
        }

        if (!NativeImageRange.TryAdd(
                module.SymbolByteSize,
                module.C11ByteSize,
                out var c13Start)
            || !NativeImageRange.TryGet(
                moduleStream.Length,
                c13Start,
                module.C13ByteSize,
                out var c13Offset,
                out var c13ByteSize))
        {
            return false;
        }

        var c13 = moduleStream.AsSpan(c13Offset, c13ByteSize);

        // First pass: collect the file-checksums subsection (offset-within-it → /names offset).
        Dictionary<uint, uint>? checksumNames = null;
        var p = 0;
        while (p < c13.Length)
        {
            if (!TryReadSubsection(c13, p, out var kind, out var content, out var nextOffset))
            {
                return false;
            }

            if (kind == DebugSFileChecksums)
            {
                if (checksumNames is not null
                    || !TryReadChecksums(content, out checksumNames))
                {
                    return false;
                }
            }

            p = nextOffset;
        }

        // Second pass: DEBUG_S_LINES blocks → first line per (segment, offset).
        p = 0;
        while (p < c13.Length)
        {
            if (!TryReadSubsection(c13, p, out var kind, out var content, out var nextOffset))
            {
                return false;
            }

            if (kind == DebugSLines
                && !TryReadLines(content, checksumNames, names, table))
            {
                return false;
            }

            p = nextOffset;
        }

        return true;
    }

    private static bool TryReadLines(
        ReadOnlySpan<byte> content,
        Dictionary<uint, uint>? checksumNames,
        byte[] names,
        Dictionary<(int Segment, uint Offset), (string? File, int? Line)> table)
    {
        if (content.Length < 12)
        {
            return false;
        }

        var contributionOffset = BinaryPrimitives.ReadUInt32LittleEndian(content);
        var contributionSegment = BinaryPrimitives.ReadUInt16LittleEndian(content[4..]);
        var flags = BinaryPrimitives.ReadUInt16LittleEndian(content[6..]);
        var lineStride = (flags & 1) != 0 ? 12u : 8u;
        var blockOffset = 12;
        while (blockOffset < content.Length)
        {
            if (blockOffset > content.Length - 12)
            {
                return false;
            }

            var fileChecksumOffset = BinaryPrimitives.ReadUInt32LittleEndian(
                content[blockOffset..]);
            var lineCount = BinaryPrimitives.ReadUInt32LittleEndian(
                content[(blockOffset + 4)..]);
            var blockByteSize = BinaryPrimitives.ReadUInt32LittleEndian(
                content[(blockOffset + 8)..]);
            if (lineCount == 0
                || blockByteSize < 12
                || !NativeImageRange.TryGet(
                    content.Length,
                    (ulong)blockOffset,
                    blockByteSize,
                    out _,
                    out var containedBlockByteSize)
                || !NativeImageRange.TryGetTable(
                    containedBlockByteSize,
                    12,
                    lineCount,
                    lineStride,
                    lineStride,
                    out _,
                    out _)
                || checksumNames is null
                || !checksumNames.TryGetValue(fileChecksumOffset, out var nameOffset)
                || !TryResolveName(names, nameOffset, out var file))
            {
                return false;
            }

            var firstLineData = BinaryPrimitives.ReadUInt32LittleEndian(
                content[(blockOffset + 16)..]);
            var line = (int)(firstLineData & 0xFF_FFFF);
            table.TryAdd((contributionSegment, contributionOffset), (file, line));
            blockOffset += containedBlockByteSize;
        }

        return true;
    }

    private static bool TryReadSubsection(
        ReadOnlySpan<byte> c13,
        int offset,
        out uint kind,
        out ReadOnlySpan<byte> content,
        out int nextOffset)
    {
        kind = 0;
        content = default;
        nextOffset = 0;
        if (offset < 0 || offset > c13.Length - 8)
        {
            return false;
        }

        kind = BinaryPrimitives.ReadUInt32LittleEndian(c13[offset..]);
        var length = BinaryPrimitives.ReadUInt32LittleEndian(c13[(offset + 4)..]);
        var contentOffset = offset + 8;
        if (!NativeImageRange.TryGet(
                c13.Length,
                (ulong)contentOffset,
                length,
                out _,
                out var contentLength)
            || !NativeImageRange.TryAdd((ulong)contentOffset, length, out var contentEnd)
            || !NativeImageRange.TryAlignUp(contentEnd, 4, out var alignedEnd)
            || alignedEnd > (ulong)c13.Length)
        {
            return false;
        }

        content = c13.Slice(contentOffset, contentLength);
        nextOffset = (int)alignedEnd;
        return true;
    }

    private static bool TryReadChecksums(
        ReadOnlySpan<byte> checksums,
        out Dictionary<uint, uint> names)
    {
        names = [];
        var offset = 0;
        while (offset < checksums.Length)
        {
            if (!NativeImageRange.TryGet(
                    checksums.Length,
                    (ulong)offset,
                    sizeof(uint) + 2,
                    out var containedOffset,
                    out _))
            {
                return false;
            }

            var nameOffset = BinaryPrimitives.ReadUInt32LittleEndian(
                checksums[containedOffset..]);
            var checksumByteSize = checksums[containedOffset + sizeof(uint)];
            if (!NativeImageRange.TryAdd(
                    (ulong)containedOffset + sizeof(uint) + 2,
                    checksumByteSize,
                    out var checksumEnd)
                || !NativeImageRange.TryAlignUp(checksumEnd, 4, out var nextOffset)
                || nextOffset > (ulong)checksums.Length)
            {
                return false;
            }

            names.Add((uint)offset, nameOffset);
            offset = (int)nextOffset;
        }

        return true;
    }

    private static bool TryResolveName(byte[] names, uint nameOffset, out string name)
    {
        name = string.Empty;
        var span = names.AsSpan();
        if (span.Length < 12
            || BinaryPrimitives.ReadUInt32LittleEndian(span) != NamesSignature)
        {
            return false;
        }

        var byteSize = BinaryPrimitives.ReadUInt32LittleEndian(span[8..]);
        if (nameOffset >= byteSize
            || !NativeImageRange.TryGet(
                span.Length,
                12,
                byteSize,
                out var dataOffset,
                out var dataLength)
            || !NativeImageRange.TryGet(
                dataLength,
                nameOffset,
                1,
                out var containedNameOffset,
                out _))
        {
            return false;
        }

        return TryReadCString(
            span.Slice(
                dataOffset + containedNameOffset,
                dataLength - containedNameOffset),
            out name);
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
