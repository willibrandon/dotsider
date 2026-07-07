using Dotsider.Core.Analysis;
using System.Buffers.Binary;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="ElfSymtabReader"/> — the <c>.symtab</c> data pass — verifying that named
/// <c>STT_OBJECT</c> entries surface with their exact <c>st_size</c> while functions, undefined,
/// unnamed, and reserved-section entries are skipped.
/// </summary>
public class ElfSymtabReaderTests
{
    private static byte[] Sym(uint nameOffset, byte type, ushort shndx, ulong value, ulong size)
    {
        var b = new byte[24];
        BinaryPrimitives.WriteUInt32LittleEndian(b, nameOffset);
        b[4] = type; // bind LOCAL << 4 | type
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(6), shndx);
        BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(8), value);
        BinaryPrimitives.WriteUInt64LittleEndian(b.AsSpan(16), size);
        return b;
    }

    private static byte[] Concat(params byte[][] parts)
    {
        var all = new List<byte>();
        foreach (var p in parts) all.AddRange(p);
        return [.. all];
    }

    /// <summary>
    /// Verifies the object pass: an <c>STT_OBJECT</c> keeps its exact size and maps to its
    /// section, while <c>STT_FUNC</c>, undefined, unnamed, and absolute entries are skipped.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ReadDataSymbols_KeepsNamedObjectsWithExactSizes()
    {
        var strtab = "\0_ZTV6Widget\0main\0"u8.ToArray();
        var symtab = Concat(
            Sym(0, 0, 0, 0, 0),                     // null entry
            Sym(1, 1, 2, 0x2010, 0x18),             // STT_OBJECT in .data -> kept
            Sym(13, 2, 1, 0x1000, 0x40),            // STT_FUNC -> skipped
            Sym(1, 1, 0, 0x3000, 8),                // undefined -> skipped
            Sym(0, 1, 2, 0x2040, 8),                // unnamed -> skipped
            Sym(1, 1, 0xFFF1, 0x2050, 8));          // SHN_ABS -> skipped

        var image = SyntheticImageBuilders.BuildElf(
            (".text", 0x1000, new byte[0x100], 1u, 0u),
            (".data", 0x2000, new byte[0x100], 1u, 0u),
            (".symtab", 0, symtab, 2u, 4u),
            (".strtab", 0, strtab, 3u, 0u));

        var symbols = ElfSymtabReader.ReadDataSymbols(image, ElfImageReader.ReadSections(image));

        var s = Assert.Single(symbols);
        Assert.Equal("_ZTV6Widget", s.Name);
        Assert.Equal(0x2010UL, s.VirtualAddress);
        Assert.Equal(0x18, s.Size); // exact st_size, not nearest-next
        Assert.Equal(".data", s.Section);
        Assert.True(s.IsData);
        Assert.NotNull(s.FileOffset);
    }

    /// <summary>Verifies malformed tables yield nothing rather than throwing.</summary>
    [Fact(Timeout = 30_000)]
    public void ReadDataSymbols_Malformed_ReturnsEmpty()
    {
        // No symbol table at all.
        var plain = SyntheticImageBuilders.BuildElf((".text", 0x1000, new byte[8]));
        Assert.Empty(ElfSymtabReader.ReadDataSymbols(plain, ElfImageReader.ReadSections(plain)));

        // Link points past the section table.
        var symtab = Concat(Sym(0, 0, 0, 0, 0), Sym(1, 1, 1, 0x1000, 8));
        var badLink = SyntheticImageBuilders.BuildElf(
            (".text", 0x1000, new byte[8], 1u, 0u),
            (".symtab", 0, symtab, 2u, 99u));
        Assert.Empty(ElfSymtabReader.ReadDataSymbols(badLink, ElfImageReader.ReadSections(badLink)));
    }
}
