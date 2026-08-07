using Dotsider.Core.Analysis.Models;
using System.Buffers.Binary;
using System.Text;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Reads the linked dylibs and symbol table of a thin 64-bit Mach-O image — the
/// import/export analog of the PE data directories for macOS Native AOT output.
/// Imports are the loaded dylibs (each with the undefined external symbols bound to
/// it through the two-level namespace library ordinal); exports are the defined
/// external symbols. Fat archives and 32-bit images yield empty results.
/// Malformed or overflowing structural ranges also yield empty results rather than
/// partially trusted tables.
/// </summary>
internal static class MachOImageReader
{
    private const uint Magic64LittleEndian = 0xFEEDFACF;
    private const int HeaderSize = 32;
    private const int NListSize = 16;
    private const int MaxCommands = 4_096;
    private const int MaxSymbols = 262_144;
    private const int MaxStringLength = 4_096;

    private const uint LcSymtab = 0x2;
    private const uint LcDysymtab = 0xB;
    private const uint LcLoadDylib = 0xC;
    private const uint LcLoadWeakDylib = 0x80000018;
    private const uint LcReexportDylib = 0x8000001F;
    private const uint LcLoadUpwardDylib = 0x80000023;
    private const uint LcSegment64 = 0x19;
    private const uint LcUuid = 0x1B;
    private const uint LcFunctionStarts = 0x26;

    private const uint FatMagic = 0xCAFEBABE;
    private const uint FatMagic64 = 0xCAFEBABF;
    private const int MaxFatSlices = 64;

    private const int Segment64HeaderSize = 72;
    private const int Section64Size = 80;
    private const uint SectionTypeGbZeroFill = 0xC;
    private const uint SectionTypeThreadLocalZeroFill = 0x12;
    private const uint SectionTypeZeroFill = 0x1;

    private const byte NStab = 0xE0;
    private const byte NType = 0x0E;
    private const byte NExt = 0x01;
    private const byte NUndef = 0x0;
    private const byte NSect = 0xE;

    /// <summary>Returns true if the bytes are a thin little-endian 64-bit Mach-O image.</summary>
    internal static bool IsMachO(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= HeaderSize
        && BinaryPrimitives.ReadUInt32LittleEndian(bytes) == Magic64LittleEndian;

    /// <summary>Returns true if the bytes are a fat/universal Mach-O archive.</summary>
    internal static bool IsFat(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 8
        && BinaryPrimitives.ReadUInt32BigEndian(bytes) is FatMagic or FatMagic64;

    /// <summary>
    /// Enumerates the architecture slices of a fat archive (both the 32- and 64-bit header
    /// forms, which are big-endian), or an empty list for thin images.
    /// </summary>
    /// <param name="bytes">The raw archive bytes.</param>
    internal static IReadOnlyList<MachOFatSlice> ReadFatSlices(ReadOnlySpan<byte> bytes)
    {
        if (!IsFat(bytes)) return [];
        var is64 = BinaryPrimitives.ReadUInt32BigEndian(bytes) == FatMagic64;
        var count = BinaryPrimitives.ReadUInt32BigEndian(bytes[4..]);
        var entrySize = is64 ? 32U : 20U;
        if (count > MaxFatSlices
            || !NativeImageRange.TryGetTable(
                bytes.Length,
                8,
                count,
                entrySize,
                entrySize,
                out var tableOffset,
                out _))
        {
            return [];
        }

        var slices = new List<MachOFatSlice>((int)count);
        for (var i = 0; i < count; i++)
        {
            var entry = tableOffset + (int)(i * entrySize);
            var cpuType = BinaryPrimitives.ReadUInt32BigEndian(bytes[entry..]);
            var offset = is64
                ? BinaryPrimitives.ReadUInt64BigEndian(bytes[(entry + 8)..])
                : BinaryPrimitives.ReadUInt32BigEndian(bytes[(entry + 8)..]);
            var size = is64
                ? BinaryPrimitives.ReadUInt64BigEndian(bytes[(entry + 16)..])
                : BinaryPrimitives.ReadUInt32BigEndian(bytes[(entry + 12)..]);
            if (size == 0
                || !NativeImageRange.TryGet(
                    bytes.Length,
                    offset,
                    size,
                    out var sliceOffset,
                    out var sliceSize))
            {
                return [];
            }

            slices.Add(new MachOFatSlice(cpuType, sliceOffset, sliceSize));
        }

        return slices;
    }

    /// <summary>Reads the image's <c>LC_UUID</c> — the identity a dSYM must match.</summary>
    /// <param name="bytes">The raw image bytes.</param>
    /// <param name="uuid">The 16-byte UUID payload.</param>
    internal static bool TryReadUuid(ReadOnlySpan<byte> bytes, out byte[] uuid)
    {
        uuid = [];
        if (!TryReadCommands(bytes, out var commands)) return false;
        foreach (var (cmd, offset, size) in commands)
        {
            if (cmd != LcUuid || size < 24) continue;
            uuid = bytes.Slice(offset + 8, 16).ToArray();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Walks every <c>LC_SEGMENT_64</c> into its sections, in load order, with the running
    /// 1-based ordinals the symbol table's <c>n_sect</c> field references.
    /// </summary>
    /// <param name="bytes">The raw image bytes.</param>
    internal static IReadOnlyList<MachOSection> ReadSectionList(ReadOnlySpan<byte> bytes)
    {
        if (!TryReadCommands(bytes, out var commands)) return [];

        var sections = new List<MachOSection>();
        var ordinal = 0;
        foreach (var (cmd, offset, size) in commands)
        {
            if (cmd != LcSegment64) continue;
            if (size < Segment64HeaderSize) return [];
            var segmentName = ReadFixedName(bytes, offset + 8);
            var sectionCount = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(offset + 64)..]);
            if (!NativeImageRange.TryGetTable(
                offset + size,
                (ulong)(offset + Segment64HeaderSize),
                sectionCount,
                Section64Size,
                Section64Size,
                out var sectionTable,
                out _))
            {
                return [];
            }

            for (var i = 0; i < sectionCount; i++)
            {
                var sectionOffset = sectionTable + (int)(i * Section64Size);
                var address = BinaryPrimitives.ReadUInt64LittleEndian(bytes[(sectionOffset + 32)..]);
                var sizeValue = BinaryPrimitives.ReadUInt64LittleEndian(bytes[(sectionOffset + 40)..]);
                var fileOffsetValue = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(sectionOffset + 48)..]);
                var flags = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(sectionOffset + 64)..]);
                var indirectSymbolIndex = BinaryPrimitives.ReadUInt32LittleEndian(
                    bytes[(sectionOffset + 72)..]);
                var stubSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(sectionOffset + 76)..]);
                if (sizeValue > long.MaxValue
                    || indirectSymbolIndex > int.MaxValue
                    || stubSize > int.MaxValue
                    || !NativeImageRange.TryAdd(address, sizeValue, out _))
                {
                    return [];
                }

                long fileOffset;
                if (IsZeroFill(flags))
                {
                    fileOffset = fileOffsetValue;
                }
                else if (!NativeImageRange.TryGet(
                    bytes.Length,
                    fileOffsetValue,
                    sizeValue,
                    out var validatedFileOffset,
                    out _))
                {
                    return [];
                }
                else
                {
                    fileOffset = validatedFileOffset;
                }

                ordinal++;
                sections.Add(new MachOSection(
                    Name: ReadFixedName(bytes, sectionOffset),
                    Segment: segmentName,
                    Address: address,
                    FileOffset: fileOffset,
                    Size: (long)sizeValue,
                    Flags: flags,
                    Ordinal: ordinal,
                    IndirectSymbolIndex: (int)indirectSymbolIndex,
                    StubSize: (int)stubSize));
            }
        }

        return sections;
    }

    /// <summary>
    /// Finds the <c>__TEXT</c> segment's <c>vmaddr</c> — the base <c>LC_FUNCTION_STARTS</c>
    /// deltas are relative to (explicitly that segment, not the first one).
    /// </summary>
    /// <param name="bytes">The raw image bytes.</param>
    /// <param name="vmaddr">The <c>__TEXT</c> segment's virtual address.</param>
    internal static bool TryGetTextBase(ReadOnlySpan<byte> bytes, out ulong vmaddr)
    {
        vmaddr = 0;
        if (!TryReadCommands(bytes, out var commands)) return false;
        foreach (var (cmd, offset, size) in commands)
        {
            if (cmd != LcSegment64 || size < Segment64HeaderSize) continue;
            if (ReadFixedName(bytes, offset + 8) != "__TEXT") continue;
            vmaddr = BinaryPrimitives.ReadUInt64LittleEndian(bytes[(offset + 24)..]);
            return true;
        }

        return false;
    }

    /// <summary>Finds the <c>LC_FUNCTION_STARTS</c> payload — ULEB128 address deltas.</summary>
    /// <param name="bytes">The raw image bytes.</param>
    /// <param name="fileOffset">The payload's file offset.</param>
    /// <param name="size">The payload's byte size.</param>
    internal static bool TryGetFunctionStarts(ReadOnlySpan<byte> bytes, out int fileOffset, out int size)
    {
        fileOffset = 0;
        size = 0;
        if (!TryReadCommands(bytes, out var commands)) return false;
        foreach (var (cmd, offset, cmdSize) in commands)
        {
            if (cmd != LcFunctionStarts || cmdSize < 16) continue;
            var fileOffsetValue = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(offset + 8)..]);
            var sizeValue = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(offset + 12)..]);
            return sizeValue > 0
                && NativeImageRange.TryGet(
                    bytes.Length,
                    fileOffsetValue,
                    sizeValue,
                    out fileOffset,
                    out size);
        }

        return false;
    }

    /// <summary>Finds the <c>LC_SYMTAB</c> table locations.</summary>
    /// <param name="bytes">The raw image bytes.</param>
    /// <param name="symtab">The validated symbol and string table ranges.</param>
    internal static bool TryGetSymtab(ReadOnlySpan<byte> bytes, out MachOSymbolTable symtab)
    {
        symtab = default;
        if (!TryReadCommands(bytes, out var commands)) return false;
        foreach (var (cmd, offset, size) in commands)
        {
            if (cmd != LcSymtab || size < 24) continue;
            return TryReadSymbolTable(bytes, offset, out symtab);
        }

        return false;
    }

    /// <summary>Finds the <c>LC_DYSYMTAB</c> indirect symbol table, which maps stub/pointer slots to symbols.</summary>
    /// <param name="bytes">The raw image bytes.</param>
    /// <param name="table">The validated indirect-symbol table.</param>
    internal static bool TryGetIndirectSymbolTable(
        ReadOnlySpan<byte> bytes,
        out MachOIndirectSymbolTable table)
    {
        table = default;
        if (!TryReadCommands(bytes, out var commands)) return false;
        foreach (var (cmd, offset, size) in commands)
        {
            if (cmd != LcDysymtab || size < 80) continue;
            var tableOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(offset + 56)..]);
            var count = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(offset + 60)..]);
            if (count == 0
                || !NativeImageRange.TryGetTable(
                    bytes.Length,
                    tableOffset,
                    count,
                    sizeof(uint),
                    sizeof(uint),
                    out var fileOffset,
                    out _))
            {
                return false;
            }

            table = new MachOIndirectSymbolTable(fileOffset, (int)count);
            return true;
        }

        return false;
    }

    private static bool IsZeroFill(uint flags) =>
        (flags & 0xFF) is SectionTypeZeroFill or SectionTypeGbZeroFill or SectionTypeThreadLocalZeroFill;

    /// <summary>Enumerates fully validated load commands of a thin image.</summary>
    private static bool TryReadCommands(
        ReadOnlySpan<byte> bytes,
        out List<(uint Cmd, int Offset, int Size)> commands)
    {
        commands = [];
        if (!IsMachO(bytes)) return false;

        var commandCount = BinaryPrimitives.ReadUInt32LittleEndian(bytes[16..]);
        var commandsSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes[20..]);
        if (commandCount > MaxCommands
            || !NativeImageRange.TryGet(
                bytes.Length,
                HeaderSize,
                commandsSize,
                out var command,
                out var commandBytes))
        {
            return false;
        }

        var commandsEnd = command + commandBytes;
        commands = new List<(uint, int, int)>((int)commandCount);
        for (var i = 0; i < commandCount; i++)
        {
            if (!NativeImageRange.TryGet(commandsEnd, command, 8, out _, out _))
            {
                commands = [];
                return false;
            }

            var cmd = BinaryPrimitives.ReadUInt32LittleEndian(bytes[command..]);
            var cmdSizeValue = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(command + 4)..]);
            if (cmdSizeValue < 8
                || (cmdSizeValue & 7) != 0
                || !NativeImageRange.TryGet(
                    commandsEnd,
                    (ulong)command,
                    cmdSizeValue,
                    out _,
                    out var cmdSize))
            {
                commands = [];
                return false;
            }

            commands.Add((cmd, command, cmdSize));
            command += cmdSize;
        }

        if (command != commandsEnd)
        {
            commands = [];
            return false;
        }

        return true;
    }

    private static string ReadFixedName(ReadOnlySpan<byte> bytes, int offset)
    {
        var slice = bytes.Slice(offset, 16);
        var end = slice.IndexOf((byte)0);
        if (end < 0) end = 16;
        return Encoding.ASCII.GetString(slice[..end]);
    }

    /// <summary>Reads the loaded dylibs and the undefined external symbols bound to each.</summary>
    internal static IReadOnlyList<ImportedModuleInfo> ReadImports(ReadOnlySpan<byte> bytes)
    {
        if (!IsMachO(bytes)) return [];
        if (Parse(bytes) is not { } image) return [];

        var modules = image.Dylibs
            .Select(d => (Name: d, Functions: new List<ImportedFunctionInfo>()))
            .ToList();

        if (image.Symbols is { } symtab)
        {
            for (var i = 0; i < symtab.Count && i < MaxSymbols; i++)
            {
                var entry = symtab.Offset + i * NListSize;
                var type = bytes[entry + 4];
                if ((type & NStab) != 0) continue;
                if ((type & NExt) == 0 || (type & NType) != NUndef) continue;

                var nameOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes[entry..]);
                var desc = BinaryPrimitives.ReadUInt16LittleEndian(bytes[(entry + 6)..]);
                var name = ReadString(
                    bytes,
                    symtab.StringOffset,
                    symtab.StringSize,
                    nameOffset);
                if (string.IsNullOrEmpty(name)) continue;

                // Two-level namespace: high byte of n_desc is the 1-based dylib ordinal.
                var ordinal = (desc >> 8) & 0xFF;
                if (ordinal >= 1 && ordinal <= modules.Count)
                    modules[ordinal - 1].Functions.Add(
                        new ImportedFunctionInfo(name, Ordinal: null, Hint: null));
            }
        }

        return [.. modules.Select(m => new ImportedModuleInfo(m.Name, m.Functions))];
    }

    /// <summary>Reads the defined external symbols (the exported symbols).</summary>
    internal static IReadOnlyList<ExportedFunctionInfo> ReadExports(ReadOnlySpan<byte> bytes)
    {
        if (!IsMachO(bytes)) return [];
        if (Parse(bytes) is not { Symbols: { } symtab }) return [];

        var exports = new List<ExportedFunctionInfo>();
        for (var i = 0; i < symtab.Count && i < MaxSymbols; i++)
        {
            var entry = symtab.Offset + i * NListSize;
            var type = bytes[entry + 4];
            if ((type & NStab) != 0) continue;
            if ((type & NExt) == 0 || (type & NType) != NSect) continue;

            var nameOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes[entry..]);
            var value = BinaryPrimitives.ReadUInt64LittleEndian(bytes[(entry + 8)..]);
            var name = ReadString(
                bytes,
                symtab.StringOffset,
                symtab.StringSize,
                nameOffset);
            if (string.IsNullOrEmpty(name)) continue;

            exports.Add(new ExportedFunctionInfo(
                Ordinal: i, Name: name, Rva: (int)value, ForwardedTo: null));
        }

        return exports;
    }

    private static (IReadOnlyList<string> Dylibs, MachOSymbolTable? Symbols)? Parse(
        ReadOnlySpan<byte> bytes)
    {
        if (!TryReadCommands(bytes, out var commands)) return null;

        var dylibs = new List<string>();
        MachOSymbolTable? symbols = null;

        foreach (var (cmd, command, cmdSize) in commands)
        {
            switch (cmd)
            {
                case LcLoadDylib or LcLoadWeakDylib or LcReexportDylib or LcLoadUpwardDylib:
                    {
                        if (cmdSize < 24) return null;
                        var nameOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(command + 8)..]);
                        var name = ReadString(bytes, command, cmdSize, nameOffset);
                        dylibs.Add(string.IsNullOrEmpty(name) ? "(unknown)" : ShortDylibName(name));
                        break;
                    }

                case LcSymtab:
                    {
                        if (cmdSize < 24
                            || !TryReadSymbolTable(bytes, command, out var symbolTable))
                            return null;
                        symbols = symbolTable;
                        break;
                    }
            }
        }

        return (dylibs, symbols);
    }

    private static string ShortDylibName(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash >= 0 && slash < path.Length - 1 ? path[(slash + 1)..] : path;
    }

    private static string? ReadString(
        ReadOnlySpan<byte> bytes,
        int tableOffset,
        int tableSize,
        uint offset)
    {
        if (offset >= (uint)tableSize
            || !NativeImageRange.TryGet(
                bytes.Length,
                tableOffset,
                tableSize,
                out _,
                out _)
            || !NativeImageRange.TryAdd((ulong)tableOffset, offset, out var startValue)
            || !NativeImageRange.TryGet(bytes.Length, startValue, 1, out var start, out _))
        {
            return null;
        }

        var available = tableSize - (int)offset;
        var slice = bytes.Slice(start, Math.Min(available, MaxStringLength + 1));
        var end = slice.IndexOf((byte)0);
        if (end < 0 || end > MaxStringLength) return null;

        return Encoding.UTF8.GetString(slice[..end]);
    }

    private static bool TryReadSymbolTable(
        ReadOnlySpan<byte> bytes,
        int commandOffset,
        out MachOSymbolTable symbolTable)
    {
        var symbolOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(commandOffset + 8)..]);
        var symbolCount = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(commandOffset + 12)..]);
        var stringOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(commandOffset + 16)..]);
        var stringSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(commandOffset + 20)..]);
        if (symbolCount == 0
            || !NativeImageRange.TryGetTable(
                bytes.Length,
                symbolOffset,
                symbolCount,
                NListSize,
                NListSize,
                out var symbols,
                out _)
            || !NativeImageRange.TryGet(
                bytes.Length,
                stringOffset,
                stringSize,
                out var strings,
                out var stringsLength))
        {
            symbolTable = default;
            return false;
        }

        symbolTable = new MachOSymbolTable(
            symbols,
            (int)symbolCount,
            strings,
            stringsLength);
        return true;
    }

}
