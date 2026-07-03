using System.Buffers.Binary;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Recovers function boundaries from a PE's <c>.pdata</c> exception directory when no PDB names
/// the functions. Each <c>RUNTIME_FUNCTION</c> covers one function's address range; the names are
/// gone, so these are boundaries, not a symbol table, and — as unwind data — they can miss leaf or
/// thunk functions. Supports x64 and ARM64 layouts.
/// </summary>
internal static class PdataReader
{
    private const ushort MachineAmd64 = 0x8664;
    private const ushort MachineArm64 = 0xAA64;
    private const int ExceptionDirectoryIndex = 3;
    private const byte UnwFlagChainInfo = 0x4;

    /// <summary>
    /// Reads function boundaries from the PE image, or an empty list when it has no usable
    /// exception directory.
    /// </summary>
    /// <param name="peBytes">The raw PE image bytes.</param>
    public static IReadOnlyList<RawNativeSymbol> ReadBoundaries(ReadOnlyMemory<byte> peBytes)
    {
        try
        {
            var pe = peBytes.Span;
            if (pe.Length < 0x40 || pe[0] != (byte)'M' || pe[1] != (byte)'Z') return [];
            var peHeader = BinaryPrimitives.ReadInt32LittleEndian(pe[0x3C..]);
            if (peHeader <= 0 || peHeader + 24 > pe.Length) return [];
            if (BinaryPrimitives.ReadUInt32LittleEndian(pe[peHeader..]) != 0x0000_4550) return [];

            var machine = BinaryPrimitives.ReadUInt16LittleEndian(pe[(peHeader + 4)..]);
            if (machine is not (MachineAmd64 or MachineArm64)) return [];

            var optional = peHeader + 24;
            var magic = BinaryPrimitives.ReadUInt16LittleEndian(pe[optional..]);
            var imageBase = magic == 0x20B
                ? BinaryPrimitives.ReadUInt64LittleEndian(pe[(optional + 24)..])
                : BinaryPrimitives.ReadUInt32LittleEndian(pe[(optional + 28)..]);
            var directoriesStart = optional + (magic == 0x20B ? 112 : 96);
            var entry = directoriesStart + ExceptionDirectoryIndex * 8;
            if (entry + 8 > pe.Length) return [];

            var directoryRva = BinaryPrimitives.ReadUInt32LittleEndian(pe[entry..]);
            var directorySize = BinaryPrimitives.ReadUInt32LittleEndian(pe[(entry + 4)..]);
            if (directoryRva == 0 || directorySize == 0) return [];

            var addressSpace = NativeAddressSpace.Create(pe);
            if (addressSpace is null
                || !addressSpace.TryGetFileOffset(imageBase + directoryRva, out var tableOffset, out _))
            {
                return [];
            }

            return machine == MachineAmd64
                ? ReadAmd64(pe, tableOffset, (int)directorySize, imageBase, addressSpace)
                : ReadArm64(pe, tableOffset, (int)directorySize, imageBase, addressSpace);
        }
        catch (ArgumentOutOfRangeException)
        {
            return [];
        }
    }

    private static List<RawNativeSymbol> ReadAmd64(
        ReadOnlySpan<byte> pe, int tableOffset, int directorySize, ulong imageBase, NativeAddressSpace addressSpace)
    {
        var result = new List<RawNativeSymbol>();
        for (var offset = tableOffset; offset + 12 <= pe.Length && offset < tableOffset + directorySize; offset += 12)
        {
            var beginRva = BinaryPrimitives.ReadUInt32LittleEndian(pe[offset..]);
            var endRva = BinaryPrimitives.ReadUInt32LittleEndian(pe[(offset + 4)..]);
            var unwindInfoRva = BinaryPrimitives.ReadUInt32LittleEndian(pe[(offset + 8)..]);
            if (endRva <= beginRva) continue;

            // A chained entry is a fragment of a function whose primary entry appears elsewhere;
            // folding it in avoids counting one function as several boundaries.
            if (addressSpace.TryGetFileOffset(imageBase + unwindInfoRva, out var unwindOffset, out var avail)
                && avail >= 1 && ((pe[unwindOffset] >> 3) & UnwFlagChainInfo) != 0)
            {
                continue;
            }

            result.Add(Boundary(beginRva, endRva - beginRva, imageBase, addressSpace));
        }

        return result;
    }

    private static List<RawNativeSymbol> ReadArm64(
        ReadOnlySpan<byte> pe, int tableOffset, int directorySize, ulong imageBase, NativeAddressSpace addressSpace)
    {
        var result = new List<RawNativeSymbol>();
        for (var offset = tableOffset; offset + 8 <= pe.Length && offset < tableOffset + directorySize; offset += 8)
        {
            var beginRva = BinaryPrimitives.ReadUInt32LittleEndian(pe[offset..]);
            var unwindData = BinaryPrimitives.ReadUInt32LittleEndian(pe[(offset + 4)..]);

            uint functionLength;
            if ((unwindData & 0x3) != 0)
            {
                // Packed unwind: FunctionLength is bits 2..12, in 4-byte words.
                functionLength = ((unwindData >> 2) & 0x7FF) * 4;
            }
            else
            {
                // Full .xdata record: the first word's low 18 bits are FunctionLength in words.
                if (!addressSpace.TryGetFileOffset(imageBase + unwindData, out var xdataOffset, out var avail)
                    || avail < 4)
                {
                    continue;
                }

                functionLength = (BinaryPrimitives.ReadUInt32LittleEndian(pe[xdataOffset..]) & 0x3_FFFF) * 4;
            }

            if (functionLength == 0) continue;
            result.Add(Boundary(beginRva, functionLength, imageBase, addressSpace));
        }

        return result;
    }

    private static RawNativeSymbol Boundary(
        uint rva, uint size, ulong imageBase, NativeAddressSpace addressSpace)
    {
        var va = imageBase + rva;
        long? fileOffset = addressSpace.TryGetFileOffset(va, out var fo, out _) ? fo : null;
        return new RawNativeSymbol(
            Name: $"sub_{rva:x}",
            VirtualAddress: va,
            Rva: rva,
            FileOffset: fileOffset,
            Section: null,
            Size: size,
            IsData: false,
            IsBoundary: true,
            SourceFile: null,
            Line: null);
    }
}
