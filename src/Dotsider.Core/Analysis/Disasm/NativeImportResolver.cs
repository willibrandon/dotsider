using System.Reflection.PortableExecutable;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis.Disasm;

/// <summary>
/// Resolves an indirect call/branch target that lands on an import slot to the imported symbol name,
/// so a <c>call [rip+disp]</c> through the PE Import Address Table renders as
/// <c>KERNEL32!GetProcAddress</c> rather than an unresolved address. Built once per image from the
/// import directory (PE IAT today; ELF PLT/GOT and Mach-O stubs are the planned extensions), it maps
/// each IAT slot's virtual address to its <c>MODULE!Function</c> name. <see cref="TryResolve"/>
/// composes after the symbol resolver in <see cref="NativeDisassembler.DisassembleSymbol"/>.
/// </summary>
public sealed class NativeImportResolver
{
    private readonly IReadOnlyDictionary<ulong, string> _slots;

    private NativeImportResolver(IReadOnlyDictionary<ulong, string> slots) => _slots = slots;

    /// <summary>The mapped IAT slots (virtual address → <c>MODULE!Function</c>), for diagnostics and tests.</summary>
    internal IReadOnlyDictionary<ulong, string> Slots => _slots;

    /// <summary>Resolves an import-slot virtual address to its imported name.</summary>
    /// <param name="targetVirtualAddress">The address the indirect target points at (the IAT slot).</param>
    /// <param name="import">The resolved import symbol on success.</param>
    public bool TryResolve(ulong targetVirtualAddress, out NativeSymbolRef import)
    {
        if (_slots.TryGetValue(targetVirtualAddress, out var name))
        {
            import = new NativeSymbolRef(targetVirtualAddress, name, NativeSymbolKind.Data, 0);
            return true;
        }

        import = default;
        return false;
    }

    /// <summary>
    /// Builds the resolver from a binary's raw bytes, or null when it is not a PE image or has no
    /// imports. Each IAT slot virtual address is <c>imageBase + importAddressTableRva + i*ptrSize</c>.
    /// </summary>
    /// <param name="rawBytes">The image's raw bytes.</param>
    public static NativeImportResolver? Build(ReadOnlyMemory<byte> rawBytes)
    {
        try
        {
            using var pe = new PEReader(new MemoryStream(rawBytes.ToArray()));
            var header = pe.PEHeaders.PEHeader;
            if (header is null) return null;

            var directory = header.ImportTableDirectory;
            if (directory.RelativeVirtualAddress == 0 || directory.Size == 0) return null;

            var is64 = header.Magic == PEMagic.PE32Plus;
            var ptrSize = is64 ? 8 : 4;
            var imageBase = header.ImageBase;
            var slots = new Dictionary<ulong, string>();

            for (var i = 0; i < 4096; i++)
            {
                var descriptorRva = directory.RelativeVirtualAddress + i * 20;
                var d = ReaderAt(pe, descriptorRva, 20);
                if (d is null) break;

                var reader = d.Value;
                var importNameTableRva = reader.ReadUInt32();
                reader.Offset += 8; // TimeDateStamp + ForwarderChain
                var nameRva = reader.ReadUInt32();
                var iatRva = reader.ReadUInt32();
                if (importNameTableRva == 0 && nameRva == 0 && iatRva == 0) break;

                var module = ReadAscii(pe, (int)nameRva);
                if (module is null) continue;

                MapModuleSlots(pe, module, importNameTableRva != 0 ? importNameTableRva : iatRva, iatRva,
                    imageBase, ptrSize, is64, slots);
            }

            return slots.Count > 0 ? new NativeImportResolver(slots) : null;
        }
        catch (Exception)
        {
            // Best-effort: a malformed import table or an out-of-range read must never crash the
            // disassembly it composes into — the targets simply stay unresolved.
            return null;
        }
    }

    private static void MapModuleSlots(
        PEReader pe, string module, uint thunkRva, uint iatRva, ulong imageBase, int ptrSize, bool is64,
        Dictionary<ulong, string> slots)
    {
        for (var i = 0; i < 8192; i++)
        {
            var entry = ReaderAt(pe, (int)thunkRva + i * ptrSize, ptrSize);
            if (entry is null) break;

            var reader = entry.Value;
            var value = is64 ? reader.ReadUInt64() : reader.ReadUInt32();
            if (value == 0) break;

            var slotVa = imageBase + iatRva + (ulong)(i * ptrSize);
            var isOrdinal = is64 ? (value & 0x8000_0000_0000_0000) != 0 : (value & 0x8000_0000) != 0;
            if (isOrdinal)
            {
                slots[slotVa] = $"{module}!#{value & 0xFFFF}";
                continue;
            }

            // The low bits are the RVA of an IMAGE_IMPORT_BY_NAME (2-byte hint + ASCII name).
            var name = ReadAscii(pe, (int)(value & 0x7FFF_FFFF) + 2);
            if (name is not null)
                slots[slotVa] = $"{module}!{name}";
        }
    }

    private static PEMemoryBlockReader? ReaderAt(PEReader pe, int rva, int size)
    {
        var image = pe.GetEntireImage();
        var offset = RvaToOffset(pe, rva);
        if (offset < 0 || offset + size > image.Length) return null;
        return new PEMemoryBlockReader(image, offset);
    }

    private static string? ReadAscii(PEReader pe, int rva)
    {
        var image = pe.GetEntireImage();
        var offset = RvaToOffset(pe, rva);
        if (offset < 0 || offset >= image.Length) return null;

        var span = image.GetContent(offset, Math.Min(512, image.Length - offset)).AsSpan();
        var end = span.IndexOf((byte)0);
        return end <= 0 ? null : System.Text.Encoding.ASCII.GetString(span[..end]);
    }

    private static int RvaToOffset(PEReader pe, int rva)
    {
        foreach (var section in pe.PEHeaders.SectionHeaders)
        {
            if (rva >= section.VirtualAddress && rva < section.VirtualAddress + section.VirtualSize)
                return section.PointerToRawData + (rva - section.VirtualAddress);
        }

        return -1;
    }

    private struct PEMemoryBlockReader(PEMemoryBlock block, int offset)
    {
        private readonly PEMemoryBlock _block = block;
        public int Offset { get; set; } = offset;

        public uint ReadUInt32()
        {
            var content = _block.GetContent(Offset, 4).AsSpan();
            Offset += 4;
            return (uint)(content[0] | content[1] << 8 | content[2] << 16 | content[3] << 24);
        }

        public ulong ReadUInt64() => ReadUInt32() | ((ulong)ReadUInt32() << 32);
    }
}
