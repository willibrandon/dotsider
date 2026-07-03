using System.Buffers.Binary;
using System.Text;
using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Disasm;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="NativeImportResolver"/>: it maps a PE image's Import Address Table slots, an
/// ELF image's PLT/GOT slots, and a Mach-O image's stubs to their imported symbol names, so an
/// indirect call or PLT/stub jump resolves to the import and composes into
/// <see cref="NativeDisassembler.DisassembleSymbol"/>.
/// </summary>
[Collection("SampleAssemblies")]
public class NativeImportResolverTests(SampleAssemblyFixture samples)
{
    /// <summary>Verifies the resolver reads the AOT binary's imports and names an IAT slot.</summary>
    [Fact(Timeout = 60_000)]
    public void Build_NativeAot_MapsImportSlots()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null || !File.Exists(samples.NativeAotConsoleExe),
            "NativeAOT publish did not run on this leg.");

        using var analyzer = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);
        var resolver = NativeImportResolver.Build(analyzer.RawBytes);

        Assert.NotNull(resolver); // a NativeAOT exe imports a handful of OS APIs
        Assert.NotEmpty(resolver.Slots);

        // Each mapped slot round-trips through TryResolve to its MODULE!Function name.
        var (slotVa, name) = resolver.Slots.First();
        Assert.True(resolver.TryResolve(slotVa, out var import));
        Assert.Equal(name, import.Name);
        Assert.Contains('!', import.Name);
    }

    /// <summary>Verifies the import resolver composes into DisassembleSymbol's target naming.</summary>
    [Fact(Timeout = 60_000)]
    public void DisassembleSymbol_ComposesImportResolver()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null || !File.Exists(samples.NativeAotConsoleExe),
            "NativeAOT publish did not run on this leg.");

        using var analyzer = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);
        var resolver = NativeImportResolver.Build(analyzer.RawBytes);
        Assert.NotNull(resolver);

        // A synthetic call [rip+0] whose slot is the first IAT entry names the import.
        var (slotVa, expected) = resolver.Slots.First();
        var callAddress = slotVa - 6; // call [rip+0] is 6 bytes; next-IP == slot
        byte[] code = [0xFF, 0x15, 0x00, 0x00, 0x00, 0x00];
        bool Compose(ulong va, out NativeSymbolRef sym) => resolver.TryResolve(va, out sym);

        var insn = NativeDisassembler.Disassemble(code, callAddress, NativeArchitecture.X64, Compose)[0];
        Assert.Equal(expected, insn.TargetName);
    }

    /// <summary>Verifies a binary with no import table yields no resolver.</summary>
    [Fact(Timeout = 30_000)]
    public void Build_NoImports_ReturnsNull()
    {
        Assert.Null(NativeImportResolver.Build(new byte[] { 0x00, 0x01, 0x02, 0x03 }));
    }

    /// <summary>
    /// A synthetic ELF with a <c>.rela.plt</c> JUMP_SLOT relocation binds its GOT slot to a
    /// <c>.dynsym</c> symbol, so the resolver names the slot the PLT stub jumps through.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Build_Elf_MapsPltGotSlotToDynamicSymbol()
    {
        const ulong gotSlotVa = 0x2018;
        var image = BuildSyntheticElf(gotSlotVa, "malloc");

        var resolver = NativeImportResolver.Build(image);

        Assert.NotNull(resolver);
        Assert.True(resolver.TryResolve(gotSlotVa, out var import));
        Assert.Equal("malloc", import.Name);
    }

    /// <summary>Verifies the ELF resolver composes into DisassembleSymbol: a PLT stub's inner jmp through the GOT slot names the import.</summary>
    [Fact(Timeout = 30_000)]
    public void DisassembleSymbol_ComposesElfImportResolver()
    {
        const ulong stubVa = 0x1020;
        const ulong gotSlotVa = 0x2018;
        var resolver = NativeImportResolver.Build(BuildSyntheticElf(gotSlotVa, "malloc"));
        Assert.NotNull(resolver);

        // jmp [rip+disp]; next-IP (stub + 6) + disp == the GOT slot.
        var disp = (uint)(gotSlotVa - (stubVa + 6));
        byte[] stub = [0xFF, 0x25, (byte)disp, (byte)(disp >> 8), (byte)(disp >> 16), (byte)(disp >> 24)];
        bool Compose(ulong va, out NativeSymbolRef sym) => resolver.TryResolve(va, out sym);

        var insn = NativeDisassembler.Disassemble(stub, stubVa, NativeArchitecture.X64, Compose)[0];
        Assert.Equal("malloc", insn.TargetName);
    }

    /// <summary>
    /// Builds a minimal ELF64 carrying a single import: <c>.dynstr</c> holds the name, <c>.dynsym</c>
    /// has one UNDEF symbol referencing it, and <c>.rela.plt</c> binds <paramref name="gotSlotVa"/> to
    /// that symbol with an R_X86_64_JUMP_SLOT relocation.
    /// </summary>
    private static byte[] BuildSyntheticElf(ulong gotSlotVa, string importName)
    {
        var dynstr = new byte[1 + importName.Length + 1];
        Encoding.ASCII.GetBytes(importName).CopyTo(dynstr, 1); // index 0 is the empty string

        var dynsym = new byte[48]; // null entry + one 24-byte Elf64_Sym
        BinaryPrimitives.WriteUInt32LittleEndian(dynsym.AsSpan(24), 1); // st_name -> offset 1 in .dynstr
        dynsym[28] = 0x12;                                              // st_info = STB_GLOBAL | STT_FUNC
        BinaryPrimitives.WriteUInt16LittleEndian(dynsym.AsSpan(30), 0); // st_shndx = SHN_UNDEF (an import)

        var rela = new byte[24]; // one Elf64_Rela
        BinaryPrimitives.WriteUInt64LittleEndian(rela.AsSpan(0), gotSlotVa);            // r_offset = GOT slot VA
        BinaryPrimitives.WriteUInt64LittleEndian(rela.AsSpan(8), (1UL << 32) | 7);      // sym 1, R_X86_64_JUMP_SLOT
        BinaryPrimitives.WriteUInt64LittleEndian(rela.AsSpan(16), 0);                   // r_addend

        // Section indices are 1-based after the null section: .dynsym=1, .dynstr=2, .rela.plt=3.
        return SyntheticImageBuilders.BuildElf(
            (".dynsym", 0, dynsym, 11u, 2u, 0u),   // SHT_DYNSYM, sh_link -> .dynstr
            (".dynstr", 0, dynstr, 3u, 0u, 0u),    // SHT_STRTAB
            (".rela.plt", 0, rela, 4u, 1u, 0u));   // SHT_RELA, sh_link -> .dynsym
    }

    /// <summary>
    /// A synthetic Mach-O <c>__stubs</c> section resolves each stub through the indirect symbol table
    /// to its imported symbol, so the stub's virtual address names the import.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Build_MachO_MapsStubToImportedSymbol()
    {
        const ulong stubVa = 0x1000;
        var resolver = NativeImportResolver.Build(BuildSyntheticMachO(stubVa, "_malloc"));

        Assert.NotNull(resolver);
        Assert.True(resolver.TryResolve(stubVa, out var import));
        Assert.Equal("_malloc", import.Name);
    }

    /// <summary>Verifies the Mach-O resolver composes into DisassembleSymbol: a direct call to the stub names the import.</summary>
    [Fact(Timeout = 30_000)]
    public void DisassembleSymbol_ComposesMachOImportResolver()
    {
        const ulong stubVa = 0x1000;
        const ulong callVa = 0x800;
        var resolver = NativeImportResolver.Build(BuildSyntheticMachO(stubVa, "_malloc"));
        Assert.NotNull(resolver);

        // call rel32; next-IP (call + 5) + rel == the stub.
        var rel = (uint)(stubVa - (callVa + 5));
        byte[] call = [0xE8, (byte)rel, (byte)(rel >> 8), (byte)(rel >> 16), (byte)(rel >> 24)];
        bool Compose(ulong va, out NativeSymbolRef sym) => resolver.TryResolve(va, out sym);

        var insn = NativeDisassembler.Disassemble(call, callVa, NativeArchitecture.X64, Compose)[0];
        Assert.Equal("_malloc", insn.TargetName);
    }

    /// <summary>
    /// Builds a minimal thin 64-bit Mach-O with one <c>__stubs</c> entry at <paramref name="stubVa"/>
    /// wired — through <c>LC_DYSYMTAB</c>'s indirect symbol table and <c>LC_SYMTAB</c> — to a single
    /// undefined external symbol named <paramref name="importName"/>.
    /// </summary>
    private static byte[] BuildSyntheticMachO(ulong stubVa, string importName)
    {
        const int headerSize = 32;
        const int segCmdSize = 72 + 2 * 80;        // LC_SEGMENT_64 with __text + __stubs
        const int symtabCmdSize = 24;
        const int dysymtabCmdSize = 80;
        const int sizeofcmds = segCmdSize + symtabCmdSize + dysymtabCmdSize;
        const int stubSize = 6;

        var stubsOffset = headerSize + sizeofcmds;
        var stringsOffset = stubsOffset + stubSize;
        var strings = new byte[1 + importName.Length + 1];
        Encoding.ASCII.GetBytes(importName).CopyTo(strings, 1); // name at string-table offset 1
        var nlistOffset = stringsOffset + strings.Length;
        var indirectOffset = nlistOffset + 16;

        var image = new byte[indirectOffset + 4];
        var b = image.AsSpan();

        // Mach-O header (thin, 64-bit LE), CPU_TYPE_X86_64, MH_EXECUTE, 3 load commands.
        BinaryPrimitives.WriteUInt32LittleEndian(b, 0xFEEDFACF);
        BinaryPrimitives.WriteUInt32LittleEndian(b[4..], 0x01000007);
        BinaryPrimitives.WriteUInt32LittleEndian(b[12..], 2);
        BinaryPrimitives.WriteUInt32LittleEndian(b[16..], 3);
        BinaryPrimitives.WriteUInt32LittleEndian(b[20..], sizeofcmds);

        // LC_SEGMENT_64 __TEXT with __text (regular) and __stubs (S_SYMBOL_STUBS).
        var cmd = headerSize;
        BinaryPrimitives.WriteUInt32LittleEndian(b[cmd..], 0x19);
        BinaryPrimitives.WriteUInt32LittleEndian(b[(cmd + 4)..], segCmdSize);
        Encoding.ASCII.GetBytes("__TEXT").CopyTo(b[(cmd + 8)..]);
        BinaryPrimitives.WriteUInt64LittleEndian(b[(cmd + 24)..], stubVa - 0x100); // vmaddr
        BinaryPrimitives.WriteUInt64LittleEndian(b[(cmd + 32)..], 0x4000);         // vmsize
        BinaryPrimitives.WriteUInt32LittleEndian(b[(cmd + 64)..], 2);              // nsects

        var text = cmd + 72;
        Encoding.ASCII.GetBytes("__text").CopyTo(b[text..]);
        Encoding.ASCII.GetBytes("__TEXT").CopyTo(b[(text + 16)..]);
        BinaryPrimitives.WriteUInt64LittleEndian(b[(text + 32)..], stubVa - 0x100);
        BinaryPrimitives.WriteUInt32LittleEndian(b[(text + 64)..], 0x80000400); // pure instructions (regular)

        var stubs = text + 80;
        Encoding.ASCII.GetBytes("__stubs").CopyTo(b[stubs..]);
        Encoding.ASCII.GetBytes("__TEXT").CopyTo(b[(stubs + 16)..]);
        BinaryPrimitives.WriteUInt64LittleEndian(b[(stubs + 32)..], stubVa);          // addr
        BinaryPrimitives.WriteUInt64LittleEndian(b[(stubs + 40)..], stubSize);        // size = one stub
        BinaryPrimitives.WriteUInt32LittleEndian(b[(stubs + 48)..], (uint)stubsOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(b[(stubs + 64)..], 0x80000408);      // S_SYMBOL_STUBS
        BinaryPrimitives.WriteUInt32LittleEndian(b[(stubs + 72)..], 0);               // reserved1 = indirect base
        BinaryPrimitives.WriteUInt32LittleEndian(b[(stubs + 76)..], stubSize);        // reserved2 = stub size

        // LC_SYMTAB.
        cmd += segCmdSize;
        BinaryPrimitives.WriteUInt32LittleEndian(b[cmd..], 0x2);
        BinaryPrimitives.WriteUInt32LittleEndian(b[(cmd + 4)..], symtabCmdSize);
        BinaryPrimitives.WriteUInt32LittleEndian(b[(cmd + 8)..], (uint)nlistOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(b[(cmd + 12)..], 1);
        BinaryPrimitives.WriteUInt32LittleEndian(b[(cmd + 16)..], (uint)stringsOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(b[(cmd + 20)..], (uint)strings.Length);

        // LC_DYSYMTAB carrying the one-entry indirect symbol table.
        cmd += symtabCmdSize;
        BinaryPrimitives.WriteUInt32LittleEndian(b[cmd..], 0xB);
        BinaryPrimitives.WriteUInt32LittleEndian(b[(cmd + 4)..], dysymtabCmdSize);
        BinaryPrimitives.WriteUInt32LittleEndian(b[(cmd + 56)..], (uint)indirectOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(b[(cmd + 60)..], 1);

        // Content: string table, one undefined-external nlist, and indirect[0] -> symbol 0.
        strings.CopyTo(b[stringsOffset..]);
        BinaryPrimitives.WriteUInt32LittleEndian(b[nlistOffset..], 1); // n_strx -> "_malloc"
        b[nlistOffset + 4] = 0x01;                                     // n_type = N_EXT (undefined external)
        BinaryPrimitives.WriteUInt32LittleEndian(b[indirectOffset..], 0);
        return image;
    }
}
