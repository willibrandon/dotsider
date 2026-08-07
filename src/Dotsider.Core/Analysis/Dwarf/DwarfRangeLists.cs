namespace Dotsider.Core.Analysis.Dwarf;

/// <summary>
/// Resolves a range-based subprogram's <c>DW_AT_ranges</c> reference into a start address and a
/// covered byte size: the v4 <c>.debug_ranges</c> begin/end pair walk (with <c>(-1, base)</c>
/// base-address escape entries), and the v5 <c>.debug_rnglists</c> RLE opcode walk (with
/// <c>rnglistx</c> index indirection through the CU's offset array and <c>startx</c> forms
/// through <c>.debug_addr</c>). The start is the lowest range begin; the size is the sum of
/// covered bytes. Malformed or empty lists yield false.
/// </summary>
internal static class DwarfRangeLists
{
    // .debug_rnglists entry opcodes (DWARF5 §7.25).
    private const byte RleEndOfList = 0x00;
    private const byte RleBaseAddressx = 0x01;
    private const byte RleStartxEndx = 0x02;
    private const byte RleStartxLength = 0x03;
    private const byte RleOffsetPair = 0x04;
    private const byte RleBaseAddress = 0x05;
    private const byte RleStartEnd = 0x06;
    private const byte RleStartLength = 0x07;

    private const int MaxEntries = 65_536;

    /// <summary>
    /// Resolves the ranges at <paramref name="rangesOffset"/> to the function's start (lowest
    /// range begin) and size (covered byte sum).
    /// </summary>
    /// <param name="sections">The DWARF section bytes.</param>
    /// <param name="rangesOffset">The <c>DW_AT_ranges</c> value: a section offset, or a <c>rnglistx</c> index.</param>
    /// <param name="isRnglistx">Whether <paramref name="rangesOffset"/> is a <c>rnglistx</c> index into the CU's offset array.</param>
    /// <param name="unit">The owning compilation unit's context.</param>
    /// <param name="start">The lowest range start.</param>
    /// <param name="size">The covered byte sum.</param>
    public static bool TryResolve(
        DwarfSections sections, long rangesOffset, bool isRnglistx,
        DwarfReader.UnitContext unit, out ulong start, out ulong size)
    {
        start = 0;
        size = 0;
        try
        {
            return unit.Version >= 5
                ? TryResolveRnglists(sections, rangesOffset, isRnglistx, unit, out start, out size)
                : TryResolveRanges(sections, rangesOffset, unit, out start, out size);
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static bool TryResolveRanges(
        DwarfSections sections, long offset, DwarfReader.UnitContext unit,
        out ulong start, out ulong size)
    {
        start = 0;
        size = 0;
        if (offset < 0 || offset >= sections.Ranges.Length) return false;

        var reader = new DwarfDataReader(sections.Ranges) { Position = (int)offset };
        var baseAddress = unit.BaseAddress;
        var escape = unit.AddressSize == 8 ? ulong.MaxValue : uint.MaxValue;
        var lowest = ulong.MaxValue;
        ulong sum = 0;

        for (var i = 0; i < MaxEntries; i++)
        {
            var begin = reader.ReadAddress(unit.AddressSize);
            var end = reader.ReadAddress(unit.AddressSize);
            if (begin == escape)
            {
                baseAddress = end;
                continue;
            }

            if (begin == 0 && end == 0) break;
            Accumulate(baseAddress + begin, baseAddress + end, ref lowest, ref sum);
        }

        return Finish(lowest, sum, ref start, ref size);
    }

    private static bool TryResolveRnglists(
        DwarfSections sections, long rangesOffset, bool isRnglistx,
        DwarfReader.UnitContext unit, out ulong start, out ulong size)
    {
        start = 0;
        size = 0;

        if (isRnglistx)
        {
            // The index selects an entry in the CU's offset array; the entry is an offset from
            // the array's base to the target list.
            var offsetSize = unit.Is64 ? 8 : 4;
            var position = unit.RnglistsBase + rangesOffset * offsetSize;
            if (position < 0 || position + offsetSize > sections.RngLists.Length) return false;
            var entryReader = new DwarfDataReader(sections.RngLists) { Position = (int)position };
            rangesOffset = unit.RnglistsBase + (long)entryReader.ReadSectionOffset(unit.Is64);
        }

        if (rangesOffset < 0 || rangesOffset >= sections.RngLists.Length) return false;

        var reader = new DwarfDataReader(sections.RngLists) { Position = (int)rangesOffset };
        var baseAddress = unit.BaseAddress;
        var lowest = ulong.MaxValue;
        ulong sum = 0;

        for (var i = 0; i < MaxEntries; i++)
        {
            var opcode = reader.ReadU8();
            switch (opcode)
            {
                case RleEndOfList:
                    return Finish(lowest, sum, ref start, ref size);

                case RleBaseAddressx:
                    if (!TryReadAddr(sections, reader.ReadULeb128(), unit, out baseAddress)) return false;
                    break;

                case RleStartxEndx:
                    {
                        if (!TryReadAddr(sections, reader.ReadULeb128(), unit, out var s)) return false;
                        if (!TryReadAddr(sections, reader.ReadULeb128(), unit, out var e)) return false;
                        Accumulate(s, e, ref lowest, ref sum);
                        break;
                    }

                case RleStartxLength:
                    {
                        if (!TryReadAddr(sections, reader.ReadULeb128(), unit, out var s)) return false;
                        Accumulate(s, s + reader.ReadULeb128(), ref lowest, ref sum);
                        break;
                    }

                case RleOffsetPair:
                    {
                        var s = baseAddress + reader.ReadULeb128();
                        var e = baseAddress + reader.ReadULeb128();
                        Accumulate(s, e, ref lowest, ref sum);
                        break;
                    }

                case RleBaseAddress:
                    baseAddress = reader.ReadAddress(unit.AddressSize);
                    break;

                case RleStartEnd:
                    {
                        var s = reader.ReadAddress(unit.AddressSize);
                        var e = reader.ReadAddress(unit.AddressSize);
                        Accumulate(s, e, ref lowest, ref sum);
                        break;
                    }

                case RleStartLength:
                    {
                        var s = reader.ReadAddress(unit.AddressSize);
                        Accumulate(s, s + reader.ReadULeb128(), ref lowest, ref sum);
                        break;
                    }

                default:
                    return false; // unknown opcode: operand size unknowable
            }
        }

        return false;
    }

    private static void Accumulate(ulong rangeStart, ulong rangeEnd, ref ulong lowest, ref ulong sum)
    {
        if (rangeEnd <= rangeStart) return;
        sum += rangeEnd - rangeStart;
        if (rangeStart < lowest) lowest = rangeStart;
    }

    private static bool Finish(ulong lowest, ulong sum, ref ulong start, ref ulong size)
    {
        if (lowest == ulong.MaxValue) return false;
        start = lowest;
        size = sum;
        return true;
    }

    private static bool TryReadAddr(
        DwarfSections sections, ulong index, DwarfReader.UnitContext unit, out ulong address)
    {
        address = 0;
        var position = unit.AddrBase + (long)index * unit.AddressSize;
        if (position < 0 || position + unit.AddressSize > sections.Addr.Length) return false;
        var reader = new DwarfDataReader(sections.Addr) { Position = (int)position };
        address = reader.ReadAddress(unit.AddressSize);
        return true;
    }
}
