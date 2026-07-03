using System.Buffers.Binary;
using System.Text;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Reads the linked dylibs and symbol table of a thin 64-bit Mach-O image — the
/// import/export analog of the PE data directories for macOS Native AOT output.
/// Imports are the loaded dylibs (each with the undefined external symbols bound to
/// it through the two-level namespace library ordinal); exports are the defined
/// external symbols. Fat archives and 32-bit images yield empty results.
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

    /// <summary>One architecture slice of a fat/universal archive.</summary>
    /// <param name="CpuType">The slice's <c>cputype</c>.</param>
    /// <param name="Offset">The slice's byte offset in the archive.</param>
    /// <param name="Size">The slice's byte size.</param>
    internal readonly record struct MachOFatSlice(uint CpuType, long Offset, long Size);

    /// <summary>
    /// Enumerates the architecture slices of a fat archive (both the 32- and 64-bit header
    /// forms, which are big-endian), or an empty list for thin images.
    /// </summary>
    /// <param name="bytes">The raw archive bytes.</param>
    internal static IReadOnlyList<MachOFatSlice> ReadFatSlices(ReadOnlySpan<byte> bytes)
    {
        var slices = new List<MachOFatSlice>();
        try
        {
            if (!IsFat(bytes)) return slices;
            var is64 = BinaryPrimitives.ReadUInt32BigEndian(bytes) == FatMagic64;
            var count = BinaryPrimitives.ReadUInt32BigEndian(bytes[4..]);
            if (count > MaxFatSlices) return slices;

            var entrySize = is64 ? 32 : 20;
            for (var i = 0; i < count; i++)
            {
                var entry = 8 + i * entrySize;
                if (entry + entrySize > bytes.Length) break;
                var cpuType = BinaryPrimitives.ReadUInt32BigEndian(bytes[entry..]);
                long offset, size;
                if (is64)
                {
                    offset = (long)BinaryPrimitives.ReadUInt64BigEndian(bytes[(entry + 8)..]);
                    size = (long)BinaryPrimitives.ReadUInt64BigEndian(bytes[(entry + 16)..]);
                }
                else
                {
                    offset = BinaryPrimitives.ReadUInt32BigEndian(bytes[(entry + 8)..]);
                    size = BinaryPrimitives.ReadUInt32BigEndian(bytes[(entry + 12)..]);
                }

                if (offset < 0 || size <= 0 || offset + size > bytes.Length) continue;
                slices.Add(new MachOFatSlice(cpuType, offset, size));
            }
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            // Keep the slices parsed so far.
        }

        return slices;
    }

    /// <summary>Reads the image's <c>LC_UUID</c> — the identity a dSYM must match.</summary>
    /// <param name="bytes">The raw image bytes.</param>
    /// <param name="uuid">The 16-byte UUID payload.</param>
    internal static bool TryReadUuid(ReadOnlySpan<byte> bytes, out byte[] uuid)
    {
        uuid = [];
        foreach (var (cmd, offset, size) in Commands(bytes))
        {
            if (cmd != LcUuid || size < 24) continue;
            uuid = bytes.Slice(offset + 8, 16).ToArray();
            return true;
        }

        return false;
    }

    /// <summary>One Mach-O section's identity, location, and flags.</summary>
    /// <param name="Name">The section name (e.g. <c>__text</c>).</param>
    /// <param name="Segment">The owning segment's name (e.g. <c>__TEXT</c>).</param>
    /// <param name="Address">The section's virtual address.</param>
    /// <param name="FileOffset">The section's file offset.</param>
    /// <param name="Size">The section's byte size.</param>
    /// <param name="Flags">The section flags (instruction attributes mark executable code).</param>
    /// <param name="Ordinal">The 1-based ordinal <c>n_sect</c> refers to, across all segments in load order.</param>
    internal readonly record struct MachOSection(
        string Name, string Segment, ulong Address, long FileOffset, long Size, uint Flags, int Ordinal)
    {
        /// <summary>
        /// Whether the section holds code: ILC uses <c>__text</c>, <c>__managedcode</c>, and the
        /// <c>__unbox</c> stubs, so the instruction attributes decide, not the name.
        /// </summary>
        public bool IsExecutable => (Flags & 0x8000_0400) != 0; // S_ATTR_PURE_INSTRUCTIONS | S_ATTR_SOME_INSTRUCTIONS
    }

    /// <summary>
    /// Walks every <c>LC_SEGMENT_64</c> into its sections, in load order, with the running
    /// 1-based ordinals the symbol table's <c>n_sect</c> field references.
    /// </summary>
    /// <param name="bytes">The raw image bytes.</param>
    internal static IReadOnlyList<MachOSection> ReadSectionList(ReadOnlySpan<byte> bytes)
    {
        var sections = new List<MachOSection>();
        try
        {
            var ordinal = 0;
            foreach (var (cmd, offset, size) in Commands(bytes))
            {
                if (cmd != LcSegment64 || size < Segment64HeaderSize) continue;
                var segmentName = ReadFixedName(bytes, offset + 8);
                var sectionCount = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(offset + 64)..]);

                for (var i = 0; i < sectionCount; i++)
                {
                    var s = offset + Segment64HeaderSize + i * Section64Size;
                    if (s + Section64Size > offset + size || s + Section64Size > bytes.Length) break;
                    ordinal++;
                    sections.Add(new MachOSection(
                        Name: ReadFixedName(bytes, s),
                        Segment: segmentName,
                        Address: BinaryPrimitives.ReadUInt64LittleEndian(bytes[(s + 32)..]),
                        FileOffset: BinaryPrimitives.ReadUInt32LittleEndian(bytes[(s + 48)..]),
                        Size: (long)BinaryPrimitives.ReadUInt64LittleEndian(bytes[(s + 40)..]),
                        Flags: BinaryPrimitives.ReadUInt32LittleEndian(bytes[(s + 64)..]),
                        Ordinal: ordinal));
                }
            }
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            // Keep the sections parsed so far.
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
        foreach (var (cmd, offset, size) in Commands(bytes))
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
        foreach (var (cmd, offset, cmdSize) in Commands(bytes))
        {
            if (cmd != LcFunctionStarts || cmdSize < 16) continue;
            fileOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[(offset + 8)..]);
            size = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[(offset + 12)..]);
            return fileOffset >= 0 && size > 0 && fileOffset + size <= bytes.Length;
        }

        return false;
    }

    /// <summary>Finds the <c>LC_SYMTAB</c> table locations.</summary>
    /// <param name="bytes">The raw image bytes.</param>
    /// <param name="symtab">The nlist array's offset and count, and the string table's offset.</param>
    internal static bool TryGetSymtab(ReadOnlySpan<byte> bytes, out (int Offset, int Count, int StringOffset) symtab)
    {
        symtab = default;
        foreach (var (cmd, offset, size) in Commands(bytes))
        {
            if (cmd != LcSymtab || size < 24) continue;
            symtab = (
                (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[(offset + 8)..]),
                (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[(offset + 12)..]),
                (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[(offset + 16)..]));
            return symtab.Count > 0;
        }

        return false;
    }

    /// <summary>Enumerates the load commands of a thin image as (cmd, offset, size) triples.</summary>
    private static List<(uint Cmd, int Offset, int Size)> Commands(ReadOnlySpan<byte> bytes)
    {
        var commands = new List<(uint, int, int)>();
        if (!IsMachO(bytes)) return commands;

        var commandCount = BinaryPrimitives.ReadUInt32LittleEndian(bytes[16..]);
        if (commandCount > MaxCommands) return commands;

        var command = HeaderSize;
        for (var i = 0; i < commandCount; i++)
        {
            if (command + 8 > bytes.Length) break;
            var cmd = BinaryPrimitives.ReadUInt32LittleEndian(bytes[command..]);
            var cmdSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[(command + 4)..]);
            if (cmdSize < 8 || command + cmdSize > bytes.Length) break;
            commands.Add((cmd, command, cmdSize));
            command += cmdSize;
        }

        return commands;
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
        try
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
                    var name = ReadString(bytes, symtab.StringOffset, nameOffset);
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
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            return [];
        }
    }

    /// <summary>Reads the defined external symbols (the exported symbols).</summary>
    internal static IReadOnlyList<ExportedFunctionInfo> ReadExports(ReadOnlySpan<byte> bytes)
    {
        try
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
                var name = ReadString(bytes, symtab.StringOffset, nameOffset);
                if (string.IsNullOrEmpty(name)) continue;

                exports.Add(new ExportedFunctionInfo(
                    Ordinal: i, Name: name, Rva: (int)value, ForwardedTo: null));
            }

            return exports;
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            return [];
        }
    }

    private static MachOImage? Parse(ReadOnlySpan<byte> bytes)
    {
        var commandCount = BinaryPrimitives.ReadUInt32LittleEndian(bytes[16..]);
        if (commandCount > MaxCommands) return null;

        var dylibs = new List<string>();
        (int Offset, int Count, int StringOffset)? symbols = null;

        var command = HeaderSize;
        for (var i = 0; i < commandCount; i++)
        {
            if (command + 8 > bytes.Length) break;
            var cmd = BinaryPrimitives.ReadUInt32LittleEndian(bytes[command..]);
            var cmdSize = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[(command + 4)..]);
            if (cmdSize < 8 || command + cmdSize > bytes.Length) break;

            switch (cmd)
            {
                case LcLoadDylib or LcLoadWeakDylib or LcReexportDylib or LcLoadUpwardDylib:
                {
                    var nameOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(command + 8)..]);
                    var name = ReadString(bytes, command, nameOffset);
                    dylibs.Add(string.IsNullOrEmpty(name) ? "(unknown)" : ShortDylibName(name));
                    break;
                }

                case LcSymtab:
                {
                    var symOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[(command + 8)..]);
                    var symCount = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[(command + 12)..]);
                    var stringOffset = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[(command + 16)..]);
                    symbols = (symOffset, symCount, stringOffset);
                    break;
                }
            }

            command += cmdSize;
        }

        return new MachOImage(dylibs, symbols);
    }

    private static string ShortDylibName(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash >= 0 && slash < path.Length - 1 ? path[(slash + 1)..] : path;
    }

    private static string? ReadString(ReadOnlySpan<byte> bytes, long tableOffset, uint offset)
    {
        var start = tableOffset + offset;
        if (start < 0 || start >= bytes.Length) return null;

        var slice = bytes[(int)start..];
        var end = slice.IndexOf((byte)0);
        if (end < 0) end = Math.Min(slice.Length, MaxStringLength);
        if (end > MaxStringLength) return null;

        return Encoding.UTF8.GetString(slice[..end]);
    }

    private readonly record struct MachOImage(
        IReadOnlyList<string> Dylibs, (int Offset, int Count, int StringOffset)? Symbols);
}
