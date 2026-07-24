using Dotsider.Core.Analysis.Models;
using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Dotsider.Core.Analysis.Disasm;

/// <summary>
/// Resolves an indirect call/branch target that lands on an import slot to the imported symbol name,
/// so a <c>call [rip+disp]</c> through the PE Import Address Table renders as
/// <c>KERNEL32!GetProcAddress</c>, an ELF PLT stub jumping through its GOT slot renders as the bound
/// dynamic symbol, and a Mach-O stub renders as its imported symbol — rather than an unresolved
/// address. Built once per image, it maps each import slot's virtual address to its name.
/// <see cref="TryResolve"/> composes after the symbol resolver in
/// <see cref="NativeDisassembler.DisassembleSymbol(AssemblyAnalyzer, Models.NativeSymbol)"/>.
/// </summary>
public sealed class NativeImportResolver
{
    private readonly IReadOnlyDictionary<ulong, string> _slots;

    private NativeImportResolver(IReadOnlyDictionary<ulong, string> slots) => _slots = slots;

    /// <summary>The mapped import slots (virtual address → symbol name), for diagnostics and tests.</summary>
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
    /// Builds the resolver from a binary's raw bytes, dispatching on the image format (PE, ELF, or
    /// Mach-O), or null when the format carries no resolvable import slots or its import data is
    /// malformed or oversized.
    /// </summary>
    /// <param name="rawBytes">The image's raw bytes.</param>
    /// <param name="architecture">The selected architecture, used to pick the slice of a fat Mach-O.</param>
    public static NativeImportResolver? Build(
        ReadOnlyMemory<byte> rawBytes, NativeArchitecture architecture = NativeArchitecture.Unknown)
    {
        var span = rawBytes.Span;
        if (ElfImageReader.IsElf(span)) return BuildElf(rawBytes);
        if (MachOImageReader.IsMachO(span) || MachOImageReader.IsFat(span)) return BuildMachO(rawBytes, architecture);
        return BuildPe(rawBytes);
    }

    /// <summary>
    /// Maps each PE Import Address Table slot to its <c>MODULE!Function</c> name. Each IAT slot virtual
    /// address is <c>imageBase + importAddressTableRva + i*ptrSize</c>.
    /// </summary>
    private static NativeImportResolver? BuildPe(ReadOnlyMemory<byte> rawBytes)
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

    /// <summary>
    /// Maps each ELF GOT slot that a PLT stub jumps through to its bound dynamic symbol, by reading
    /// the <c>.rela.plt</c> and <c>.rela.dyn</c> JUMP_SLOT/GLOB_DAT relocations against <c>.dynsym</c>.
    /// A PLT stub's internal <c>jmp [rip+disp]</c> lands on the GOT slot, so resolving the slot names
    /// the call (each stub jumps through its GOT slot).
    /// </summary>
    private static NativeImportResolver? BuildElf(ReadOnlyMemory<byte> rawBytes)
    {
        try
        {
            var bytes = rawBytes.Span;
            if (!ElfImageReader.TryGetSection(bytes, ".dynsym", out var dynsymSection)
                || !ElfImageReader.TryGetSection(bytes, ".dynstr", out var dynstrSection))
                return null;

            var remainingBytes = NativeImageDataLimits.MaxMaterializedBytes;
            byte[]? Read(ElfSection section)
            {
                byte[]? materialized = ElfImageReader.ReadSectionBytes(
                    rawBytes,
                    section,
                    remainingBytes);
                if (materialized is not null)
                    remainingBytes -= materialized.Length;
                return materialized;
            }

            var dynsym = Read(dynsymSection);
            var dynstr = Read(dynstrSection);
            if (dynsym is null || dynstr is null) return null;

            var slots = new Dictionary<ulong, string>();
            foreach (var relocSection in (string[])[".rela.plt", ".rela.dyn"])
            {
                if (!ElfImageReader.TryGetSection(bytes, relocSection, out var section)) continue;
                var relocs = Read(section);
                if (relocs is not null) MapElfRelocations(relocs, dynsym, dynstr, slots);
            }

            return slots.Count > 0 ? new NativeImportResolver(slots) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads 24-byte <c>Elf64_Rela</c> entries, mapping each JUMP_SLOT/GLOB_DAT relocation's target
    /// (the GOT slot virtual address, <c>r_offset</c>) to the name of the <c>.dynsym</c> symbol it binds.
    /// </summary>
    private static void MapElfRelocations(
        byte[] relocs, byte[] dynsym, byte[] dynstr, Dictionary<ulong, string> slots)
    {
        // JUMP_SLOT / GLOB_DAT relocation types for x86-64 (7 / 6) and AArch64 (1026 / 1025); the two
        // sets do not collide, so accepting all four needs no architecture switch.
        for (var offset = 0; offset + 24 <= relocs.Length; offset += 24)
        {
            var slotVa = BinaryPrimitives.ReadUInt64LittleEndian(relocs.AsSpan(offset));
            var info = BinaryPrimitives.ReadUInt64LittleEndian(relocs.AsSpan(offset + 8));
            if ((uint)info is not (6u or 7u or 1025u or 1026u)) continue;

            var name = ReadElfSymbolName(dynsym, dynstr, (int)(info >> 32));
            if (name is not null) slots[slotVa] = name;
        }
    }

    private static string? ReadElfSymbolName(byte[] dynsym, byte[] dynstr, int symbolIndex)
    {
        var offset = symbolIndex * 24; // sizeof(Elf64_Sym); st_name is the leading uint32.
        if (symbolIndex < 0 || offset + 4 > dynsym.Length) return null;
        var stName = BinaryPrimitives.ReadUInt32LittleEndian(dynsym.AsSpan(offset));
        return ReadCString(dynstr, (int)stName);
    }

    private static string? ReadCString(byte[] data, int offset)
    {
        if (offset < 0 || offset >= data.Length) return null;
        var end = Array.IndexOf(data, (byte)0, offset);
        if (end < 0) end = data.Length;
        return end <= offset ? null : Encoding.ASCII.GetString(data, offset, end - offset);
    }

    /// <summary>
    /// Maps each Mach-O stub and symbol-pointer slot to its imported symbol. A stub or pointer section
    /// carries a base index (<c>reserved1</c>) into the indirect symbol table; entry <c>base + i</c>
    /// names slot <c>i</c> via the regular symbol table, so a <c>call stub</c> or a load through a
    /// <c>__got</c>/<c>__la_symbol_ptr</c> slot resolves to the bound symbol.
    /// </summary>
    private static NativeImportResolver? BuildMachO(ReadOnlyMemory<byte> rawBytes, NativeArchitecture architecture)
    {
        try
        {
            var bytes = SelectMachOSlice(rawBytes, architecture).Span;
            if (!MachOImageReader.IsMachO(bytes)
                || !MachOImageReader.TryGetSymtab(bytes, out var symtab)
                || !MachOImageReader.TryGetIndirectSymbolTable(bytes, out var indirect))
                return null;

            var slots = new Dictionary<ulong, string>();
            foreach (var section in MachOImageReader.ReadSectionList(bytes))
            {
                // S_SYMBOL_STUBS (0x8) strides by the stub size; the symbol-pointer sections
                // S_LAZY_SYMBOL_POINTERS (0x7) / S_NON_LAZY_SYMBOL_POINTERS (0x6) stride by a pointer.
                var stride = section.Type switch { 0x8 => section.StubSize, 0x6 or 0x7 => 8, _ => 0 };
                if (stride <= 0) continue;

                var count = (int)(section.Size / stride);
                for (var i = 0; i < count; i++)
                {
                    var symbolIndex = ReadIndirectSymbol(bytes, indirect, section.IndirectSymbolIndex + i);
                    // Skip INDIRECT_SYMBOL_LOCAL (0x80000000) / INDIRECT_SYMBOL_ABS (0x40000000) — not imports.
                    if (symbolIndex is null || (symbolIndex.Value & 0xC000_0000) != 0) continue;

                    var name = ReadMachOSymbolName(bytes, symtab, (int)symbolIndex.Value);
                    if (name is not null) slots[section.Address + (ulong)(i * stride)] = name;
                }
            }

            return slots.Count > 0 ? new NativeImportResolver(slots) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Selects the slice of a fat Mach-O matching the architecture, or returns the bytes unchanged for a thin image.</summary>
    private static ReadOnlyMemory<byte> SelectMachOSlice(ReadOnlyMemory<byte> rawBytes, NativeArchitecture architecture)
    {
        if (!MachOImageReader.IsFat(rawBytes.Span)) return rawBytes;

        var slices = MachOImageReader.ReadFatSlices(rawBytes.Span);
        if (slices.Count == 0) return rawBytes;

        var wanted = architecture switch
        {
            NativeArchitecture.X86 => 7u,             // CPU_TYPE_X86
            NativeArchitecture.Arm32 => 12u,          // CPU_TYPE_ARM
            NativeArchitecture.X64 => 0x0100_0007u,   // CPU_TYPE_X86_64
            NativeArchitecture.Arm64 => 0x0100_000Cu, // CPU_TYPE_ARM64
            _ => 0u,
        };
        var slice = slices.FirstOrDefault(s => s.CpuType == wanted, slices[0]);
        if (slice.Offset < 0 || slice.Offset >= rawBytes.Length) return rawBytes;

        var end = (int)Math.Min(slice.Offset + slice.Size, rawBytes.Length);
        return rawBytes[(int)slice.Offset..end];
    }

    private static uint? ReadIndirectSymbol(ReadOnlySpan<byte> bytes, (int Offset, int Count) indirect, int index)
    {
        if (index < 0 || index >= indirect.Count) return null;
        var offset = indirect.Offset + index * 4;
        if (offset < 0 || offset + 4 > bytes.Length) return null;
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
    }

    private static string? ReadMachOSymbolName(
        ReadOnlySpan<byte> bytes, (int Offset, int Count, int StringOffset) symtab, int symbolIndex)
    {
        if (symbolIndex < 0 || symbolIndex >= symtab.Count) return null;
        var nlist = symtab.Offset + symbolIndex * 16; // sizeof(nlist_64); n_strx is the leading uint32
        if (nlist < 0 || nlist + 4 > bytes.Length) return null;

        var nameOffset = symtab.StringOffset + (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes[nlist..]);
        if (nameOffset < 0 || nameOffset >= bytes.Length) return null;

        var slice = bytes[nameOffset..];
        var end = slice.IndexOf((byte)0);
        return end <= 0 ? null : Encoding.ASCII.GetString(slice[..end]);
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
