using System.Buffers.Binary;
using System.Text;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Reads native symbols from a thin 64-bit Mach-O image's <c>nlist</c> table. Functions are the
/// symbols in executable sections — any section flagged with instruction attributes, which on
/// ILC output means <c>__managedcode</c> and the <c>__unbox</c> stubs as well as <c>__text</c> —
/// plus <c>N_FUN</c> stab pairs (dSYMs), whose end entries carry explicit sizes. Data symbols
/// come from non-executable sections when the demangler recognizes an ILC data node in the name.
/// nlist records no sizes, so unsized functions take the distance to the next symbol, clamped to
/// their containing section's end. The Mach-O leading underscore is stripped from every name.
/// </summary>
internal static class MachOSymbolReader
{
    private const int NListSize = 16;
    private const int MaxSymbols = 1 << 20;

    private const byte NStab = 0xE0;
    private const byte NType = 0x0E;
    private const byte NSect = 0xE;
    private const byte NFun = 0x24;

    /// <summary>
    /// Reads the image's named symbols: executable-section functions, <c>N_FUN</c> stab pairs,
    /// and recognized data nodes, sized and section-attributed.
    /// </summary>
    /// <param name="bytes">The raw thin image bytes.</param>
    /// <param name="demangler">The demangler that recognizes ILC data-node names.</param>
    public static IReadOnlyList<RawNativeSymbol> ReadSymbols(ReadOnlySpan<byte> bytes, IlcNameDemangler demangler)
    {
        var result = new List<RawNativeSymbol>();
        try
        {
            if (!MachOImageReader.TryGetSymtab(bytes, out var symtab)) return result;
            var sections = MachOImageReader.ReadSectionList(bytes);
            var functions = new List<(string Name, ulong Va, int Ordinal, long Size)>();

            string? openStabName = null;
            ulong openStabVa = 0;
            var openStabOrdinal = 0;

            var count = Math.Min(symtab.Count, MaxSymbols);
            for (var i = 0; i < count; i++)
            {
                var entry = symtab.Offset + i * NListSize;

                var type = bytes[entry + 4];
                var ordinal = bytes[entry + 5];
                var value = BinaryPrimitives.ReadUInt64LittleEndian(bytes[(entry + 8)..]);
                var nameOffset = BinaryPrimitives.ReadUInt32LittleEndian(bytes[entry..]);
                var name = ReadName(
                    bytes,
                    symtab.StringOffset,
                    symtab.StringSize,
                    nameOffset);

                if ((type & NStab) != 0)
                {
                    if (type != NFun) continue;
                    if (name.Length > 0)
                    {
                        // Opening N_FUN: the function's start address.
                        openStabName = name;
                        openStabVa = value;
                        openStabOrdinal = ordinal;
                    }
                    else if (openStabName is not null)
                    {
                        // Closing N_FUN: n_value is the function's size.
                        functions.Add((openStabName, openStabVa, openStabOrdinal, (long)value));
                        openStabName = null;
                    }

                    continue;
                }

                if ((type & NType) != NSect || name.Length == 0 || value == 0) continue;

                var section = FindSection(sections, ordinal);
                if (section is { IsExecutable: true })
                {
                    functions.Add((name, value, ordinal, 0));
                }
                else if (section is not null
                    && demangler.Demangle(name).Kind != Models.NativeSymbolKind.Function)
                {
                    // Non-executable section: keep only recognized ILC data nodes.
                    result.Add(Raw(name, value, section.Value, size: 0, isData: true));
                }
            }

            AppendSizedFunctions(functions, sections, result);
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            // Keep the symbols parsed before the damage.
        }

        return result;
    }

    /// <summary>
    /// Reads <c>LC_FUNCTION_STARTS</c> into nameless boundaries: ULEB128 deltas relative to the
    /// <c>__TEXT</c> segment's <c>vmaddr</c>, sized by successive starts with the last clamped
    /// to the end of its containing executable section.
    /// </summary>
    /// <param name="bytes">The raw thin image bytes.</param>
    public static IReadOnlyList<RawNativeSymbol> ReadFunctionStartBoundaries(ReadOnlySpan<byte> bytes)
    {
        var result = new List<RawNativeSymbol>();
        try
        {
            if (!MachOImageReader.TryGetFunctionStarts(bytes, out var offset, out var size)) return result;
            if (!MachOImageReader.TryGetTextBase(bytes, out var address)) return result;
            var sections = MachOImageReader.ReadSectionList(bytes);

            var starts = new List<ulong>();
            var data = bytes.Slice(offset, size);
            var position = 0;
            while (position < data.Length && starts.Count < MaxSymbols)
            {
                ulong delta = 0;
                var shift = 0;
                byte b;
                do
                {
                    if (position >= data.Length || shift > 63) return Finish(starts, sections, result);
                    b = data[position++];
                    delta |= (ulong)(b & 0x7F) << shift;
                    shift += 7;
                }
                while ((b & 0x80) != 0);

                if (delta == 0) break; // padding terminates the stream
                if (!NativeImageRange.TryAdd(address, delta, out address))
                    return Finish(starts, sections, result);
                starts.Add(address);
            }

            return Finish(starts, sections, result);
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            return result;
        }
    }

    private static List<RawNativeSymbol> Finish(
        List<ulong> starts, IReadOnlyList<MachOSection> sections, List<RawNativeSymbol> result)
    {
        for (var i = 0; i < starts.Count; i++)
        {
            var va = starts[i];
            var section = FindSectionByAddress(sections, va);
            var end = i + 1 < starts.Count
                ? starts[i + 1]
                : section is { } s && TryGetSectionEnd(s, out var sectionEnd) ? sectionEnd : va;
            if (section is { } clamp
                && TryGetSectionEnd(clamp, out var clampEnd)
                && end > clampEnd)
                end = clampEnd;
            if (end <= va) continue;

            long? fileOffset = section is { } fo
                && TryMapFileOffset(fo, va, out var mappedOffset)
                ? mappedOffset
                : null;
            result.Add(new RawNativeSymbol(
                Name: $"sub_{va:x}",
                VirtualAddress: va,
                Rva: null,
                FileOffset: fileOffset,
                Section: section?.Name,
                Size: (long)(end - va),
                IsData: false,
                IsBoundary: true,
                SourceFile: null,
                Line: null));
        }

        return result;
    }

    private static void AppendSizedFunctions(
        List<(string Name, ulong Va, int Ordinal, long Size)> functions,
        IReadOnlyList<MachOSection> sections, List<RawNativeSymbol> result)
    {
        // nlist carries no sizes: sort and size each unsized function by the next start,
        // clamped to its containing section's end so the last one never bleeds across sections.
        functions.Sort(static (x, y) => x.Va.CompareTo(y.Va));
        for (var i = 0; i < functions.Count; i++)
        {
            var (name, va, ordinal, size) = functions[i];
            var section = FindSection(sections, ordinal) ?? FindSectionByAddress(sections, va);
            if (size == 0)
            {
                var j = i + 1;
                while (j < functions.Count && functions[j].Va <= va) j++;
                var end = j < functions.Count
                    ? functions[j].Va
                    : section is { } s && TryGetSectionEnd(s, out var sectionEnd)
                        ? sectionEnd
                        : va;
                if (section is { } clamp
                    && TryGetSectionEnd(clamp, out var clampEnd)
                    && end > clampEnd)
                    end = clampEnd;
                size = end > va ? (long)(end - va) : 0;
            }

            if (section is { } within)
                result.Add(Raw(name, va, within, size, isData: false));
        }
    }

    private static RawNativeSymbol Raw(
        string name, ulong va, MachOSection section, long size, bool isData) =>
        new(
            Name: name,
            VirtualAddress: va,
            Rva: null,
            FileOffset: TryMapFileOffset(section, va, out var fileOffset) ? fileOffset : null,
            Section: section.Name,
            Size: size,
            IsData: isData,
            IsBoundary: false,
            SourceFile: null,
            Line: null);

    private static MachOSection? FindSection(
        IReadOnlyList<MachOSection> sections, int ordinal)
    {
        foreach (var section in sections)
        {
            if (section.Ordinal == ordinal) return section;
        }

        return null;
    }

    private static MachOSection? FindSectionByAddress(
        IReadOnlyList<MachOSection> sections, ulong va)
    {
        foreach (var section in sections)
        {
            if (section.Address != 0
                && va >= section.Address
                && TryGetSectionEnd(section, out var end)
                && va < end)
                return section;
        }

        return null;
    }

    /// <summary>Reads a symbol name, stripping the Mach-O leading underscore.</summary>
    private static string ReadName(
        ReadOnlySpan<byte> bytes,
        int stringOffset,
        int stringSize,
        uint nameOffset)
    {
        if (nameOffset >= (uint)stringSize
            || !NativeImageRange.TryAdd((ulong)stringOffset, nameOffset, out var startValue)
            || !NativeImageRange.TryGet(bytes.Length, startValue, 1, out var start, out _))
            return "";

        var available = stringSize - (int)nameOffset;
        var slice = bytes.Slice(start, available);
        var end = slice.IndexOf((byte)0);
        if (end < 0) return "";
        var name = Encoding.UTF8.GetString(slice[..end]);
        return name.StartsWith('_') ? name[1..] : name;
    }

    private static bool TryGetSectionEnd(MachOSection section, out ulong end)
    {
        if (section.Size < 0)
        {
            end = 0;
            return false;
        }

        return NativeImageRange.TryAdd(section.Address, (ulong)section.Size, out end);
    }

    private static bool TryMapFileOffset(MachOSection section, ulong address, out long fileOffset)
    {
        if (section.FileOffset < 0
            || section.Size <= 0
            || section.Type is 0x1 or 0xC or 0x12
            || address < section.Address)
        {
            fileOffset = 0;
            return false;
        }

        var delta = address - section.Address;
        if (delta >= (ulong)section.Size
            || !NativeImageRange.TryAdd((ulong)section.FileOffset, delta, out var offset)
            || offset > long.MaxValue)
        {
            fileOffset = 0;
            return false;
        }

        fileOffset = (long)offset;
        return true;
    }
}
