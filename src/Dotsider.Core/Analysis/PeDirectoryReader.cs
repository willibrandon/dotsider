using Dotsider.Core.Analysis.Models;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Parses the PE import, export, and load configuration data directories, none of
/// which <see cref="PEReader"/> exposes as structured data and none of which need a
/// CLR header — they light up for apphosts, Native AOT binaries, and managed PEs
/// alike. Malformed directories never throw; they yield empty results.
/// </summary>
internal static class PeDirectoryReader
{
    private const int MaxImportDescriptors = 4_096;
    private const int MaxFunctionsPerModule = 65_536;
    private const int MaxExports = 65_536;
    private const int MaxStringLength = 4_096;

    /// <summary>Named IMAGE_GUARD_* bits for the load-config flags summary.</summary>
    private static readonly (uint Bit, string Name)[] GuardFlagNames =
    [
        (0x0000_0100, "CF Instrumented"),
        (0x0000_0200, "CFW Instrumented"),
        (0x0000_0400, "CF Function Table Present"),
        (0x0000_0800, "Security Cookie Unused"),
        (0x0000_1000, "Protect Delayload IAT"),
        (0x0000_4000, "CF Export Suppression Info Present"),
        (0x0000_8000, "CF Export Suppression Enabled"),
        (0x0001_0000, "CF Long Jump Table Present"),
        (0x0040_0000, "EH Continuation Table Present"),
    ];

    /// <summary>
    /// Reads the import table: one entry per referenced module with its imported functions.
    /// </summary>
    internal static IReadOnlyList<ImportedModuleInfo> ReadImports(PEReader peReader)
    {
        try
        {
            var peHeader = peReader.PEHeaders.PEHeader;
            if (peHeader is null) return [];

            var directory = peHeader.ImportTableDirectory;
            if (directory.RelativeVirtualAddress == 0 || directory.Size == 0) return [];

            var is64Bit = peHeader.Magic == PEMagic.PE32Plus;
            var modules = new List<ImportedModuleInfo>();

            for (var i = 0; i < MaxImportDescriptors; i++)
            {
                // IMAGE_IMPORT_DESCRIPTOR: 20 bytes, all-zero entry terminates
                var descriptor = GetReaderAt(peReader, directory.RelativeVirtualAddress + i * 20, 20);
                if (descriptor is not { } d) break;

                var importNameTableRva = d.ReadUInt32();
                d.Offset += 8; // TimeDateStamp + ForwarderChain
                var nameRva = d.ReadUInt32();
                var importAddressTableRva = d.ReadUInt32();

                if (importNameTableRva == 0 && nameRva == 0 && importAddressTableRva == 0) break;

                var moduleName = ReadAsciiString(peReader, (int)nameRva);
                if (moduleName is null) continue;

                // Prefer the import name table; bound images may zero it out
                var thunkRva = importNameTableRva != 0 ? importNameTableRva : importAddressTableRva;
                modules.Add(new ImportedModuleInfo(
                    moduleName, ReadThunks(peReader, (int)thunkRva, is64Bit)));
            }

            return modules;
        }
        catch (BadImageFormatException)
        {
            return [];
        }
    }

    /// <summary>
    /// Reads the export table, including ordinal-only exports and forwarders.
    /// </summary>
    internal static IReadOnlyList<ExportedFunctionInfo> ReadExports(PEReader peReader)
    {
        try
        {
            var peHeader = peReader.PEHeaders.PEHeader;
            if (peHeader is null) return [];

            var directory = peHeader.ExportTableDirectory;
            if (directory.RelativeVirtualAddress == 0 || directory.Size == 0) return [];

            // IMAGE_EXPORT_DIRECTORY: 40 bytes
            if (GetReaderAt(peReader, directory.RelativeVirtualAddress, 40) is not { } d) return [];

            d.Offset += 16; // Characteristics, TimeDateStamp, Major/MinorVersion, NameRVA
            var ordinalBase = d.ReadUInt32();
            var functionCount = d.ReadUInt32();
            var nameCount = d.ReadUInt32();
            var functionsRva = d.ReadUInt32();
            var namesRva = d.ReadUInt32();
            var nameOrdinalsRva = d.ReadUInt32();

            if (functionCount is 0 or > MaxExports || nameCount > MaxExports) return [];

            // Overlay the name and name-ordinal tables onto function indices
            var namesByIndex = new Dictionary<uint, string>();
            for (var i = 0u; i < nameCount; i++)
            {
                if (GetReaderAt(peReader, (int)(namesRva + i * 4), 4) is not { } nameReader) return [];
                if (GetReaderAt(peReader, (int)(nameOrdinalsRva + i * 2), 2) is not { } ordinalReader)
                    return [];

                var name = ReadAsciiString(peReader, (int)nameReader.ReadUInt32());
                if (name is not null)
                    namesByIndex[ordinalReader.ReadUInt16()] = name;
            }

            var directoryStart = directory.RelativeVirtualAddress;
            var directoryEnd = directoryStart + directory.Size;
            var exports = new List<ExportedFunctionInfo>();

            for (var i = 0u; i < functionCount; i++)
            {
                if (GetReaderAt(peReader, (int)(functionsRva + i * 4), 4) is not { } functionReader)
                    return [];

                var rva = (int)functionReader.ReadUInt32();
                if (rva == 0) continue; // gap in the ordinal range

                // A function RVA inside the export directory points at a forwarder string
                var forwardedTo = rva >= directoryStart && rva < directoryEnd
                    ? ReadAsciiString(peReader, rva)
                    : null;

                exports.Add(new ExportedFunctionInfo(
                    (int)(ordinalBase + i),
                    namesByIndex.GetValueOrDefault(i),
                    rva,
                    forwardedTo));
            }

            return exports;
        }
        catch (BadImageFormatException)
        {
            return [];
        }
    }

    /// <summary>
    /// Reads the load configuration directory. Each field is read only when it lies
    /// within the directory's declared size, so truncated historical layouts parse cleanly.
    /// </summary>
    internal static LoadConfigInfo? ReadLoadConfig(PEReader peReader)
    {
        try
        {
            var peHeader = peReader.PEHeaders.PEHeader;
            if (peHeader is null) return null;

            var directory = peHeader.LoadConfigTableDirectory;
            if (directory.RelativeVirtualAddress == 0 || directory.Size == 0) return null;

            var block = peReader.GetSectionData(directory.RelativeVirtualAddress);
            if (block.Length < 4) return null;

            var reader = block.GetReader();
            var declaredSize = reader.ReadUInt32();
            var available = Math.Min(declaredSize == 0 ? (uint)directory.Size : declaredSize,
                (uint)block.Length);

            var is64Bit = peHeader.Magic == PEMagic.PE32Plus;

            uint ReadU32At(int offset)
            {
                if (offset + 4 > available) return 0;
                var r = block.GetReader();
                r.Offset = offset;
                return r.ReadUInt32();
            }

            ushort ReadU16At(int offset)
            {
                if (offset + 2 > available) return 0;
                var r = block.GetReader();
                r.Offset = offset;
                return r.ReadUInt16();
            }

            ulong ReadPointerAt(int offset)
            {
                if (!is64Bit) return ReadU32At(offset);
                if (offset + 8 > available) return 0;
                var r = block.GetReader();
                r.Offset = offset;
                return r.ReadUInt64();
            }

            // Field offsets from IMAGE_LOAD_CONFIG_DIRECTORY32/64 (winnt.h)
            var timeDateStamp = ReadU32At(4);
            var majorVersion = ReadU16At(8);
            var minorVersion = ReadU16At(10);
            var dependentLoadFlags = ReadU16At(is64Bit ? 78 : 54);
            var securityCookie = ReadPointerAt(is64Bit ? 88 : 60);
            var sehHandlerCount = ReadPointerAt(is64Bit ? 104 : 68);
            var guardCfCheckFunctionPointer = ReadPointerAt(is64Bit ? 112 : 72);
            var guardCfFunctionCount = ReadPointerAt(is64Bit ? 136 : 84);
            var guardFlags = ReadU32At(is64Bit ? 144 : 88);

            return new LoadConfigInfo(
                declaredSize, timeDateStamp, majorVersion, minorVersion,
                dependentLoadFlags, securityCookie, sehHandlerCount,
                guardCfCheckFunctionPointer, guardCfFunctionCount,
                guardFlags, DescribeGuardFlags(guardFlags));
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Walks an import thunk table until its zero terminator, decoding ordinal and
    /// hint/name imports.
    /// </summary>
    private static List<ImportedFunctionInfo> ReadThunks(
        PEReader peReader, int thunkRva, bool is64Bit)
    {
        if (thunkRva == 0) return [];

        var thunkSize = is64Bit ? 8 : 4;
        var functions = new List<ImportedFunctionInfo>();

        for (var i = 0; i < MaxFunctionsPerModule; i++)
        {
            if (GetReaderAt(peReader, thunkRva + i * thunkSize, thunkSize) is not { } d) break;

            var thunk = is64Bit ? d.ReadUInt64() : d.ReadUInt32();
            if (thunk == 0) break;

            var ordinalBit = is64Bit ? 0x8000_0000_0000_0000UL : 0x8000_0000UL;
            if ((thunk & ordinalBit) != 0)
            {
                functions.Add(new ImportedFunctionInfo(
                    Name: null, Ordinal: (ushort)(thunk & 0xFFFF), Hint: null));
                continue;
            }

            // IMAGE_IMPORT_BY_NAME: u16 hint followed by a NUL-terminated name
            var byNameRva = (int)(thunk & 0x7FFF_FFFF);
            if (GetReaderAt(peReader, byNameRva, 2) is not { } hintReader) continue;

            var hint = hintReader.ReadUInt16();
            var name = ReadAsciiString(peReader, byNameRva + 2);
            if (name is not null)
                functions.Add(new ImportedFunctionInfo(name, Ordinal: null, Hint: hint));
        }

        return functions;
    }

    /// <summary>
    /// Returns a reader positioned at <paramref name="rva"/> when the RVA maps into a
    /// section with at least <paramref name="minSize"/> bytes remaining; otherwise null.
    /// </summary>
    private static System.Reflection.Metadata.BlobReader? GetReaderAt(
        PEReader peReader, int rva, int minSize)
    {
        if (rva <= 0) return null;

        var block = peReader.GetSectionData(rva);
        if (block.Length < minSize) return null;

        return block.GetReader();
    }

    /// <summary>
    /// Reads a NUL-terminated ASCII string at the given RVA, or null when the RVA is
    /// unmapped or the string is unterminated within <see cref="MaxStringLength"/> chars.
    /// </summary>
    private static string? ReadAsciiString(PEReader peReader, int rva)
    {
        if (rva <= 0) return null;

        var block = peReader.GetSectionData(rva);
        if (block.Length == 0) return null;

        var reader = block.GetReader();
        var length = Math.Min(block.Length, MaxStringLength);
        var builder = new StringBuilder();
        for (var i = 0; i < length; i++)
        {
            var b = reader.ReadByte();
            if (b == 0) return builder.ToString();
            builder.Append((char)b);
        }

        return null;
    }

    /// <summary>Formats the named IMAGE_GUARD_* bits set in <paramref name="guardFlags"/>.</summary>
    private static string DescribeGuardFlags(uint guardFlags)
    {
        var names = GuardFlagNames
            .Where(f => (guardFlags & f.Bit) != 0)
            .Select(f => f.Name)
            .ToList();
        return names.Count > 0 ? string.Join(", ", names) : "(none)";
    }
}
