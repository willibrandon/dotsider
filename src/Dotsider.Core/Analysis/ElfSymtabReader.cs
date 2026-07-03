using System.Buffers.Binary;
using System.Text;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Reads the named data symbols — <c>STT_OBJECT</c> entries — from an ELF <c>.symtab</c>, with
/// their exact <c>st_size</c>. This is the data pass beside the DWARF function walk: method
/// tables, frozen objects, statics, and the other ILC data nodes live here under their mangled
/// names, which the facade's demangler classifies. Undefined, unnamed, and reserved-section
/// entries are skipped; a malformed table yields the symbols parsed before the damage.
/// </summary>
internal static class ElfSymtabReader
{
    private const int SymbolSize = 24;
    private const uint ShtSymTab = 2;
    private const ushort ShnLoReserve = 0xFF00;
    private const byte SttObject = 1;
    private const int MaxSymbols = 1 << 20;

    /// <summary>
    /// Reads the data symbols from <paramref name="symbolBytes"/>' symbol table, mapping each
    /// address through <paramref name="imageSections"/> — the analyzed image's sections — so
    /// file offsets point into the image even when the symbols come from a sidecar.
    /// </summary>
    /// <param name="symbolBytes">The bytes carrying <c>.symtab</c> (the image or its sidecar).</param>
    /// <param name="imageSections">The analyzed image's sections, for address mapping.</param>
    public static IReadOnlyList<RawNativeSymbol> ReadDataSymbols(
        ReadOnlySpan<byte> symbolBytes, IReadOnlyList<ElfImageReader.ElfSection> imageSections)
    {
        var result = new List<RawNativeSymbol>();
        try
        {
            var sections = ElfImageReader.ReadSections(symbolBytes);
            ElfImageReader.ElfSection symtab = default;
            foreach (var section in sections)
            {
                if (section.Type == ShtSymTab)
                {
                    symtab = section;
                    break;
                }
            }

            if (symtab.Type != ShtSymTab || symtab.Link >= sections.Count) return result;
            var strtab = sections[(int)symtab.Link];
            if (symtab.FileOffset < 0 || symtab.FileOffset + symtab.Size > symbolBytes.Length) return result;
            if (strtab.FileOffset < 0 || strtab.FileOffset + strtab.Size > symbolBytes.Length) return result;

            var strings = symbolBytes.Slice(strtab.FileOffset, strtab.Size);
            var count = Math.Min(symtab.Size / SymbolSize, MaxSymbols);
            for (var i = 1; i < count; i++) // entry 0 is the reserved null symbol
            {
                var entry = symbolBytes.Slice(symtab.FileOffset + i * SymbolSize, SymbolSize);
                if ((entry[4] & 0xF) != SttObject) continue;

                var sectionIndex = BinaryPrimitives.ReadUInt16LittleEndian(entry[6..]);
                if (sectionIndex == 0 || sectionIndex >= ShnLoReserve) continue;

                var va = BinaryPrimitives.ReadUInt64LittleEndian(entry[8..]);
                if (va == 0) continue;

                var nameOffset = BinaryPrimitives.ReadUInt32LittleEndian(entry);
                var name = ReadName(strings, nameOffset);
                if (name.Length == 0) continue;

                var size = (long)BinaryPrimitives.ReadUInt64LittleEndian(entry[16..]);
                var mapped = ElfImageReader.TryMapAddress(imageSections, va, out var sectionName, out var fileOffset);
                result.Add(new RawNativeSymbol(
                    Name: name,
                    VirtualAddress: va,
                    Rva: null,
                    FileOffset: mapped ? fileOffset : null,
                    Section: mapped ? sectionName : null,
                    Size: size,
                    IsData: true,
                    IsBoundary: false,
                    SourceFile: null,
                    Line: null));
            }
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            // Keep the symbols parsed before the damage.
        }

        return result;
    }

    private static string ReadName(ReadOnlySpan<byte> strings, uint offset)
    {
        if (offset >= strings.Length) return "";
        var slice = strings[(int)offset..];
        var end = slice.IndexOf((byte)0);
        if (end < 0) end = slice.Length;
        return Encoding.UTF8.GetString(slice[..end]);
    }
}
