using System.Buffers.Binary;

namespace Dotsider.Core.Analysis.NativePdb;

/// <summary>
/// Parses the DBI stream (stream 3) of a PDB: its header, the per-module descriptors that name
/// each module's symbol stream and byte sizes, and the optional debug header at the tail whose
/// slots include the section-header dump. The substreams that precede the optional debug header
/// are skipped by their declared sizes, in order, so the tail is located correctly.
/// </summary>
internal sealed class DbiStream
{
    /// <summary>One module's symbol-stream location and substream sizes.</summary>
    /// <param name="SymbolStream">The module's symbol stream index, or -1 when it has none.</param>
    /// <param name="SymByteSize">The byte length of the CodeView symbol records (including the leading signature).</param>
    /// <param name="C11ByteSize">The byte length of the C11 line block that follows the symbols.</param>
    /// <param name="C13ByteSize">The byte length of the C13 line block that follows C11.</param>
    internal readonly record struct Module(int SymbolStream, int SymByteSize, int C11ByteSize, int C13ByteSize);

    /// <summary>The parsed module descriptors.</summary>
    public IReadOnlyList<Module> Modules { get; private init; } = [];

    /// <summary>The section-header dump stream index (optional debug header slot 5), or -1 when absent.</summary>
    public int SectionHeaderStream { get; private init; } = -1;

    /// <summary>The global symbol record stream index.</summary>
    public int SymbolRecordStream { get; private init; } = -1;

    /// <summary>The publics hash stream index.</summary>
    public int PublicStream { get; private init; } = -1;

    /// <summary>
    /// Parses a DBI stream, or returns null when the header is malformed.
    /// </summary>
    /// <param name="stream">The raw DBI stream bytes.</param>
    public static DbiStream? Parse(byte[] stream)
    {
        try
        {
            var span = stream.AsSpan();
            if (span.Length < 64) return null;
            if (BinaryPrimitives.ReadInt32LittleEndian(span) != -1) return null; // VersionSignature

            var publicStream = BinaryPrimitives.ReadUInt16LittleEndian(span[16..]);
            var symRecordStream = BinaryPrimitives.ReadUInt16LittleEndian(span[20..]);
            var modInfoSize = BinaryPrimitives.ReadInt32LittleEndian(span[24..]);
            var sectionContributionSize = BinaryPrimitives.ReadInt32LittleEndian(span[28..]);
            var sectionMapSize = BinaryPrimitives.ReadInt32LittleEndian(span[32..]);
            var sourceInfoSize = BinaryPrimitives.ReadInt32LittleEndian(span[36..]);
            var typeServerMapSize = BinaryPrimitives.ReadInt32LittleEndian(span[40..]);
            var optionalDbgHeaderSize = BinaryPrimitives.ReadInt32LittleEndian(span[48..]);
            var ecSubstreamSize = BinaryPrimitives.ReadInt32LittleEndian(span[52..]);

            if (modInfoSize < 0 || (long)64 + modInfoSize > span.Length) return null;

            var modules = ParseModules(span.Slice(64, modInfoSize));

            var sectionHeaderStream = -1;
            var optionalOffset = 64L + modInfoSize + sectionContributionSize + sectionMapSize
                + sourceInfoSize + typeServerMapSize + ecSubstreamSize;
            // Slot 5 of the optional debug header is the section-header dump.
            if (optionalOffset >= 0 && optionalOffset + 12 <= span.Length && optionalDbgHeaderSize >= 12)
            {
                var slot = BinaryPrimitives.ReadUInt16LittleEndian(span[(int)(optionalOffset + 5 * 2)..]);
                if (slot != 0xFFFF) sectionHeaderStream = slot;
            }

            return new DbiStream
            {
                Modules = modules,
                SectionHeaderStream = sectionHeaderStream,
                SymbolRecordStream = symRecordStream == 0xFFFF ? -1 : symRecordStream,
                PublicStream = publicStream == 0xFFFF ? -1 : publicStream,
            };
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static List<Module> ParseModules(ReadOnlySpan<byte> modInfo)
    {
        var modules = new List<Module>();
        var p = 0;
        // Fixed part per record: Unused(4) + SectionContrib(28) + Flags(2) + SymStream(2) +
        // SymByteSize(4) + C11(4) + C13(4) + SourceFileCount(2) + Pad(2) + Unused2(4) +
        // SourceFileNameIndex(4) + PdbFilePathNameIndex(4) = 64, then two NUL strings, align 4.
        while (p + 64 <= modInfo.Length)
        {
            var symStream = (short)BinaryPrimitives.ReadUInt16LittleEndian(modInfo[(p + 34)..]);
            var symByteSize = BinaryPrimitives.ReadInt32LittleEndian(modInfo[(p + 36)..]);
            var c11 = BinaryPrimitives.ReadInt32LittleEndian(modInfo[(p + 40)..]);
            var c13 = BinaryPrimitives.ReadInt32LittleEndian(modInfo[(p + 44)..]);
            modules.Add(new Module(symStream, Math.Max(0, symByteSize), Math.Max(0, c11), Math.Max(0, c13)));

            // Skip the two NUL-terminated names, then 4-byte align.
            var q = p + 64;
            q = SkipCString(modInfo, q);
            q = SkipCString(modInfo, q);
            q = (q + 3) & ~3;
            if (q <= p) break; // no progress → malformed
            p = q;
        }

        return modules;
    }

    private static int SkipCString(ReadOnlySpan<byte> span, int p)
    {
        while (p < span.Length && span[p] != 0) p++;
        return p + 1;
    }
}
