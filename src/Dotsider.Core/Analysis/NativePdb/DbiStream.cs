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
    /// <summary>The parsed module descriptors.</summary>
    public IReadOnlyList<DbiModule> Modules { get; private init; } = [];

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
        var span = stream.AsSpan();
        if (span.Length < 64
            || BinaryPrimitives.ReadInt32LittleEndian(span) != -1)
        {
            return null;
        }

        var publicStream = BinaryPrimitives.ReadUInt16LittleEndian(span[16..]);
        var symRecordStream = BinaryPrimitives.ReadUInt16LittleEndian(span[20..]);
        var modInfoSize = BinaryPrimitives.ReadInt32LittleEndian(span[24..]);
        var sectionContributionSize = BinaryPrimitives.ReadInt32LittleEndian(span[28..]);
        var sectionMapSize = BinaryPrimitives.ReadInt32LittleEndian(span[32..]);
        var sourceInfoSize = BinaryPrimitives.ReadInt32LittleEndian(span[36..]);
        var typeServerMapSize = BinaryPrimitives.ReadInt32LittleEndian(span[40..]);
        var optionalDbgHeaderSize = BinaryPrimitives.ReadInt32LittleEndian(span[48..]);
        var ecSubstreamSize = BinaryPrimitives.ReadInt32LittleEndian(span[52..]);
        if (modInfoSize < 0
            || sectionContributionSize < 0
            || sectionMapSize < 0
            || sourceInfoSize < 0
            || typeServerMapSize < 0
            || optionalDbgHeaderSize < 0
            || ecSubstreamSize < 0)
        {
            return null;
        }

        ulong optionalOffset = 64;
        if (!TryAdd(ref optionalOffset, (uint)modInfoSize)
            || !TryAdd(ref optionalOffset, (uint)sectionContributionSize)
            || !TryAdd(ref optionalOffset, (uint)sectionMapSize)
            || !TryAdd(ref optionalOffset, (uint)sourceInfoSize)
            || !TryAdd(ref optionalOffset, (uint)typeServerMapSize)
            || !TryAdd(ref optionalOffset, (uint)ecSubstreamSize)
            || !NativeImageRange.TryGet(
                span.Length,
                optionalOffset,
                (uint)optionalDbgHeaderSize,
                out var optionalHeaderOffset,
                out _)
            || !NativeImageRange.TryGet(
                span.Length,
                64,
                (uint)modInfoSize,
                out var moduleInfoOffset,
                out var moduleInfoLength))
        {
            return null;
        }

        var modules = ParseModules(span.Slice(moduleInfoOffset, moduleInfoLength));
        if (modules is null)
        {
            return null;
        }

        var sectionHeaderStream = -1;
        if (optionalDbgHeaderSize >= 12)
        {
            var slot = BinaryPrimitives.ReadUInt16LittleEndian(
                span[(optionalHeaderOffset + 5 * sizeof(ushort))..]);
            if (slot != ushort.MaxValue)
            {
                sectionHeaderStream = slot;
            }
        }

        return new DbiStream
        {
            Modules = modules,
            SectionHeaderStream = sectionHeaderStream,
            SymbolRecordStream = symRecordStream == ushort.MaxValue ? -1 : symRecordStream,
            PublicStream = publicStream == ushort.MaxValue ? -1 : publicStream,
        };
    }

    private static List<DbiModule>? ParseModules(ReadOnlySpan<byte> moduleInfo)
    {
        var modules = new List<DbiModule>();
        var p = 0;
        // Fixed part per record: Unused(4) + SectionContrib(28) + Flags(2) + SymStream(2) +
        // SymByteSize(4) + C11(4) + C13(4) + SourceFileCount(2) + Pad(2) + Unused2(4) +
        // SourceFileNameIndex(4) + PdbFilePathNameIndex(4) = 64, then two NUL strings, align 4.
        while (p < moduleInfo.Length)
        {
            if (p > moduleInfo.Length - 64)
            {
                return null;
            }

            var symbolStreamValue = BinaryPrimitives.ReadUInt16LittleEndian(
                moduleInfo[(p + 34)..]);
            var symbolStream = symbolStreamValue == ushort.MaxValue ? -1 : symbolStreamValue;
            var symbolByteSize = BinaryPrimitives.ReadUInt32LittleEndian(moduleInfo[(p + 36)..]);
            var c11ByteSize = BinaryPrimitives.ReadUInt32LittleEndian(moduleInfo[(p + 40)..]);
            var c13ByteSize = BinaryPrimitives.ReadUInt32LittleEndian(moduleInfo[(p + 44)..]);

            // Skip the two NUL-terminated names, then 4-byte align.
            var q = p + 64;
            if (!TrySkipCString(moduleInfo, ref q)
                || !TrySkipCString(moduleInfo, ref q)
                || !NativeImageRange.TryAlignUp((ulong)q, 4, out var alignedOffset)
                || alignedOffset > (ulong)moduleInfo.Length)
            {
                return null;
            }

            modules.Add(new DbiModule(
                symbolStream,
                symbolByteSize,
                c11ByteSize,
                c13ByteSize));
            p = (int)alignedOffset;
        }

        return modules;
    }

    private static bool TryAdd(ref ulong total, uint value)
    {
        if (!NativeImageRange.TryAdd(total, value, out var sum))
        {
            return false;
        }

        total = sum;
        return true;
    }

    private static bool TrySkipCString(ReadOnlySpan<byte> span, ref int offset)
    {
        if ((uint)offset > (uint)span.Length)
        {
            return false;
        }

        var terminator = span[offset..].IndexOf((byte)0);
        if (terminator < 0)
        {
            return false;
        }

        offset += terminator + 1;
        return true;
    }
}
