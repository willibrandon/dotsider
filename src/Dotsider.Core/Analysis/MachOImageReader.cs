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

    private const byte NStab = 0xE0;
    private const byte NType = 0x0E;
    private const byte NExt = 0x01;
    private const byte NUndef = 0x0;
    private const byte NSect = 0xE;

    /// <summary>Returns true if the bytes are a thin little-endian 64-bit Mach-O image.</summary>
    internal static bool IsMachO(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= HeaderSize
        && BinaryPrimitives.ReadUInt32LittleEndian(bytes) == Magic64LittleEndian;

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
