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

    /// <summary>A function or data symbol recovered from a module, before RVA resolution.</summary>
    /// <param name="Name">The raw symbol name.</param>
    /// <param name="Segment">The one-based section index.</param>
    /// <param name="Offset">The offset within the section.</param>
    /// <param name="Size">The code/data size, or 0 for data records that carry none.</param>
    /// <param name="IsData">Whether this is a data record rather than a procedure.</param>
    /// <param name="SourceFile">The declaring source file, when C13 line data resolved it.</param>
    /// <param name="Line">The first source line, when C13 line data resolved it.</param>
    internal readonly record struct ModuleSymbol(
        string Name, int Segment, uint Offset, uint Size, bool IsData, string? SourceFile, int? Line);

    /// <summary>
    /// Reads the function and data symbols of one module, attributing source file and line from
    /// the module's C13 line subsections.
    /// </summary>
    /// <param name="moduleStream">The module's full symbol stream.</param>
    /// <param name="module">The module descriptor giving the symbol and line block sizes.</param>
    /// <param name="names">The <c>/names</c> string table stream, for resolving file names.</param>
    public static List<ModuleSymbol> ReadModule(
        byte[] moduleStream, DbiStream.Module module, byte[] names)
    {
        var result = new List<ModuleSymbol>();
        var span = moduleStream.AsSpan();
        if (module.SymbolStream < 0 || module.SymByteSize <= 4 || module.SymByteSize > span.Length)
            return result;

        // Line lookup keyed by (segment, offset): each function has its own DEBUG_S_LINES block.
        var lines = ParseLineTable(moduleStream, module, names);

        var p = 4; // past the CodeView signature
        var end = module.SymByteSize;
        while (p + 4 <= end)
        {
            var length = BinaryPrimitives.ReadUInt16LittleEndian(span[p..]);
            if (length < 2) break;
            var recordEnd = p + 2 + length;
            if (recordEnd > end) break;

            var kind = BinaryPrimitives.ReadUInt16LittleEndian(span[(p + 2)..]);
            var body = span[(p + 4)..recordEnd];

            if (kind is SGProc32 or SLProc32 && body.Length >= 35)
            {
                // parent,end,next,codeSize,dbgStart,dbgEnd,typeIndex,offset (8×4), segment(2), flags(1), name
                var codeSize = BinaryPrimitives.ReadUInt32LittleEndian(body[12..]);
                var offset = BinaryPrimitives.ReadUInt32LittleEndian(body[28..]);
                var segment = BinaryPrimitives.ReadUInt16LittleEndian(body[32..]);
                var name = ReadCString(body[35..]);
                if (name.Length > 0)
                {
                    var (file, line) = lines.GetValueOrDefault((segment, offset));
                    result.Add(new ModuleSymbol(name, segment, offset, codeSize, false, file, line));
                }
            }
            else if (kind is SGData32 or SLData32 && body.Length >= 10)
            {
                // typeIndex(4), offset(4), segment(2), name
                var offset = BinaryPrimitives.ReadUInt32LittleEndian(body[4..]);
                var segment = BinaryPrimitives.ReadUInt16LittleEndian(body[8..]);
                var name = ReadCString(body[10..]);
                if (name.Length > 0)
                    result.Add(new ModuleSymbol(name, segment, offset, 0, true, null, null));
            }

            p = recordEnd;
        }

        return result;
    }

    private static Dictionary<(int Segment, uint Offset), (string? File, int? Line)> ParseLineTable(
        byte[] moduleStream, DbiStream.Module module, byte[] names)
    {
        var table = new Dictionary<(int, uint), (string?, int?)>();
        var c13Start = module.SymByteSize + module.C11ByteSize;
        if (module.C13ByteSize <= 0 || c13Start + module.C13ByteSize > moduleStream.Length) return table;

        var c13 = moduleStream.AsSpan(c13Start, module.C13ByteSize);

        // First pass: collect the file-checksums subsection (offset-within-it → /names offset).
        ReadOnlySpan<byte> checksums = default;
        var p = 0;
        while (p + 8 <= c13.Length)
        {
            var kind = BinaryPrimitives.ReadUInt32LittleEndian(c13[p..]);
            var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(c13[(p + 4)..]);
            var contentStart = p + 8;
            if (length < 0 || contentStart + length > c13.Length) break;
            if (kind == DebugSFileChecksums) checksums = c13.Slice(contentStart, length);
            p = contentStart + ((length + 3) & ~3);
        }

        // Second pass: DEBUG_S_LINES blocks → first line per (segment, offset).
        p = 0;
        while (p + 8 <= c13.Length)
        {
            var kind = BinaryPrimitives.ReadUInt32LittleEndian(c13[p..]);
            var length = (int)BinaryPrimitives.ReadUInt32LittleEndian(c13[(p + 4)..]);
            var contentStart = p + 8;
            if (length < 0 || contentStart + length > c13.Length) break;

            if (kind == DebugSLines && length >= 12)
            {
                var content = c13.Slice(contentStart, length);
                var contribOffset = BinaryPrimitives.ReadUInt32LittleEndian(content);
                var contribSegment = BinaryPrimitives.ReadUInt16LittleEndian(content[4..]);
                var flags = BinaryPrimitives.ReadUInt16LittleEndian(content[6..]);
                var hasColumns = (flags & 1) != 0;

                var bp = 12;
                while (bp + 12 <= content.Length)
                {
                    var fileChecksumOffset = BinaryPrimitives.ReadUInt32LittleEndian(content[bp..]);
                    var numLines = BinaryPrimitives.ReadUInt32LittleEndian(content[(bp + 4)..]);
                    bp += 12; // fileId, numLines, blockSize
                    if (numLines == 0 || bp + 8 > content.Length) break;

                    var firstLineData = BinaryPrimitives.ReadUInt32LittleEndian(content[(bp + 4)..]);
                    var line = (int)(firstLineData & 0xFFFFFF);
                    var file = ResolveFile(checksums, fileChecksumOffset, names);
                    table.TryAdd((contribSegment, contribOffset), (file, line));

                    // Advance past this block's line (and optional column) entries.
                    bp += (int)numLines * 8;
                    if (hasColumns) bp += (int)numLines * 4;
                }
            }

            p = contentStart + ((length + 3) & ~3);
        }

        return table;
    }

    private static string? ResolveFile(ReadOnlySpan<byte> checksums, uint checksumOffset, byte[] names)
    {
        if (checksums.IsEmpty || checksumOffset + 4 > (uint)checksums.Length) return null;
        var nameOffset = BinaryPrimitives.ReadUInt32LittleEndian(checksums[(int)checksumOffset..]);
        return ResolveName(names, nameOffset);
    }

    private static string? ResolveName(byte[] names, uint nameOffset)
    {
        // /names layout: u32 signature, u32 hashVersion, u32 byteSize, then the string data.
        var span = names.AsSpan();
        if (span.Length < 12 || BinaryPrimitives.ReadUInt32LittleEndian(span) != NamesSignature) return null;
        var byteSize = BinaryPrimitives.ReadUInt32LittleEndian(span[8..]);
        var dataStart = 12;
        if (dataStart + byteSize > span.Length || nameOffset >= byteSize) return null;
        return ReadCString(span.Slice(dataStart + (int)nameOffset));
    }

    private static string ReadCString(ReadOnlySpan<byte> span)
    {
        var end = span.IndexOf((byte)0);
        if (end < 0) end = span.Length;
        return Encoding.UTF8.GetString(span[..end]);
    }
}
