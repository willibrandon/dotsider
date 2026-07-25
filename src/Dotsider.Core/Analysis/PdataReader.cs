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
        var pe = peBytes.Span;
        if (pe.Length < 0x40 || pe[0] != (byte)'M' || pe[1] != (byte)'Z') return [];
        var peHeaderValue = BinaryPrimitives.ReadUInt32LittleEndian(pe[0x3C..]);
        if (!NativeImageRange.TryGet(
                pe.Length,
                peHeaderValue,
                24,
                out var peHeader,
                out _)
            || BinaryPrimitives.ReadUInt32LittleEndian(pe[peHeader..]) != 0x0000_4550)
        {
            return [];
        }

        var machine = BinaryPrimitives.ReadUInt16LittleEndian(pe[(peHeader + 4)..]);
        if (machine is not (MachineAmd64 or MachineArm64)) return [];

        var optionalSize = BinaryPrimitives.ReadUInt16LittleEndian(pe[(peHeader + 20)..]);
        var optional = peHeader + 24;
        if (!NativeImageRange.TryGet(
                pe.Length,
                optional,
                optionalSize,
                out _,
                out _)
            || optionalSize < sizeof(ushort))
        {
            return [];
        }

        var magic = BinaryPrimitives.ReadUInt16LittleEndian(pe[optional..]);
        var directoriesOffset = magic switch
        {
            0x10B => 96,
            0x20B => 112,
            _ => 0,
        };
        const int directoryEntrySize = 8;
        var exceptionEntryOffset = directoriesOffset + ExceptionDirectoryIndex * directoryEntrySize;
        if (directoriesOffset == 0
            || optionalSize < exceptionEntryOffset + directoryEntrySize)
            return [];

        var imageBase = magic == 0x20B
            ? BinaryPrimitives.ReadUInt64LittleEndian(pe[(optional + 24)..])
            : BinaryPrimitives.ReadUInt32LittleEndian(pe[(optional + 28)..]);
        var entry = optional + exceptionEntryOffset;
        var directoryRva = BinaryPrimitives.ReadUInt32LittleEndian(pe[entry..]);
        var directorySize = BinaryPrimitives.ReadUInt32LittleEndian(pe[(entry + 4)..]);
        var recordSize = machine == MachineAmd64 ? 12U : 8U;
        if (directoryRva == 0
            || directorySize == 0
            || directorySize % recordSize != 0)
            return [];

        var addressSpace = NativeAddressSpace.Create(pe);
        if (addressSpace is null
            || !NativeImageRange.TryAdd(imageBase, directoryRva, out var directoryAddress)
            || !addressSpace.TryGetFileOffset(
                directoryAddress,
                out var tableOffset,
                out var available)
            || directorySize > (uint)available)
        {
            return [];
        }

        return machine == MachineAmd64
            ? ReadAmd64(pe, tableOffset, (int)directorySize, imageBase, addressSpace)
            : ReadArm64(pe, tableOffset, (int)directorySize, imageBase, addressSpace);
    }

    private static List<RawNativeSymbol> ReadAmd64(
        ReadOnlySpan<byte> pe, int tableOffset, int directorySize, ulong imageBase, NativeAddressSpace addressSpace)
    {
        var result = new List<RawNativeSymbol>();
        var table = pe.Slice(tableOffset, directorySize);
        for (var offset = 0; offset < table.Length; offset += 12)
        {
            var entry = table[offset..];
            var beginRva = BinaryPrimitives.ReadUInt32LittleEndian(entry);
            var endRva = BinaryPrimitives.ReadUInt32LittleEndian(entry[4..]);
            var unwindInfoRva = BinaryPrimitives.ReadUInt32LittleEndian(entry[8..]);
            if (endRva <= beginRva) continue;

            // A chained entry is a fragment of a function whose primary entry appears elsewhere;
            // folding it in avoids counting one function as several boundaries.
            if (NativeImageRange.TryAdd(imageBase, unwindInfoRva, out var unwindAddress)
                && addressSpace.TryGetFileOffset(unwindAddress, out var unwindOffset, out var avail)
                && avail >= 1 && ((pe[unwindOffset] >> 3) & UnwFlagChainInfo) != 0)
            {
                continue;
            }

            if (Boundary(beginRva, endRva - beginRva, imageBase, addressSpace) is { } boundary)
                result.Add(boundary);
        }

        return result;
    }

    private static List<RawNativeSymbol> ReadArm64(
        ReadOnlySpan<byte> pe, int tableOffset, int directorySize, ulong imageBase, NativeAddressSpace addressSpace)
    {
        var result = new List<RawNativeSymbol>();
        var table = pe.Slice(tableOffset, directorySize);
        for (var offset = 0; offset < table.Length; offset += 8)
        {
            var entry = table[offset..];
            var beginRva = BinaryPrimitives.ReadUInt32LittleEndian(entry);
            var unwindData = BinaryPrimitives.ReadUInt32LittleEndian(entry[4..]);

            uint functionLength;
            if ((unwindData & 0x3) != 0)
            {
                // Packed unwind: FunctionLength is bits 2..12, in 4-byte words.
                functionLength = ((unwindData >> 2) & 0x7FF) * 4;
            }
            else
            {
                // Full .xdata record: the first word's low 18 bits are FunctionLength in words.
                if (!NativeImageRange.TryAdd(imageBase, unwindData, out var unwindAddress)
                    || !addressSpace.TryGetFileOffset(unwindAddress, out var xdataOffset, out var avail)
                    || avail < 4)
                {
                    continue;
                }

                functionLength = (BinaryPrimitives.ReadUInt32LittleEndian(pe[xdataOffset..]) & 0x3_FFFF) * 4;
            }

            if (functionLength == 0) continue;
            if (Boundary(beginRva, functionLength, imageBase, addressSpace) is { } boundary)
                result.Add(boundary);
        }

        return result;
    }

    private static RawNativeSymbol? Boundary(
        uint rva, uint size, ulong imageBase, NativeAddressSpace addressSpace)
    {
        if (!NativeImageRange.TryAdd(imageBase, rva, out var va)) return null;
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
