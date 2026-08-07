namespace Dotsider.Core.Analysis.Dwarf;

/// <summary>
/// Walks <c>.debug_info</c> compilation units for <c>DW_TAG_subprogram</c> DIEs — the functions —
/// resolving names through every string form (inline, <c>strp</c>, <c>line_strp</c>, and the v5
/// <c>strx</c> indirection), addresses through <c>addr</c>/<c>addrx</c>, and sizes from
/// <c>high_pc</c> in either its address or offset class. DWARF32 and DWARF64 units both parse;
/// unit types other than compile/partial units (skeletons, split units) are skipped. A DIE whose
/// name lives on a referenced declaration resolves one <c>specification</c>/<c>abstract_origin</c>
/// hop. Malformed units yield the functions parsed before the damage.
/// </summary>
internal static class DwarfReader
{
    private const int MaxUnits = 4096;
    private const int MaxDies = 1 << 22;

    /// <summary>One subprogram recovered from the DIE tree.</summary>
    /// <param name="Name">The best name found: linkage name when present, else the source name.</param>
    /// <param name="LowPc">The function's start address.</param>
    /// <param name="Size">The function's byte size, or 0 when the DIE recorded none.</param>
    /// <param name="DeclFile">The <c>DW_AT_decl_file</c> index into the CU's line-program file table, or -1 when the DIE recorded none.</param>
    /// <param name="DeclLine">The <c>DW_AT_decl_line</c> value, or 0.</param>
    /// <param name="StmtListOffset">The CU's <c>.debug_line</c> program offset, or -1 when absent.</param>
    /// <param name="RangesOffset">The <c>DW_AT_ranges</c> offset when the function is range-based, or -1.</param>
    /// <param name="RangesIsRnglistx">Whether <paramref name="RangesOffset"/> is a <c>rnglistx</c> index rather than a section offset.</param>
    internal readonly record struct DwarfFunction(
        string Name, ulong LowPc, ulong Size, int DeclFile, int DeclLine,
        long StmtListOffset, long RangesOffset, bool RangesIsRnglistx);

    /// <summary>The per-CU context needed to resolve indexed forms and ranges.</summary>
    /// <param name="Version">The DWARF version.</param>
    /// <param name="Is64">Whether the unit is DWARF64.</param>
    /// <param name="AddressSize">The unit's address size.</param>
    /// <param name="BaseAddress">The CU's base address (its own low_pc), for range lists.</param>
    /// <param name="StrOffsetsBase">The <c>.debug_str_offsets</c> base for <c>strx</c>.</param>
    /// <param name="AddrBase">The <c>.debug_addr</c> base for <c>addrx</c>.</param>
    /// <param name="RnglistsBase">The <c>.debug_rnglists</c> base for <c>rnglistx</c>.</param>
    /// <param name="StmtListOffset">The CU's line-program offset, or -1.</param>
    internal readonly record struct UnitContext(
        ushort Version, bool Is64, int AddressSize, ulong BaseAddress,
        long StrOffsetsBase, long AddrBase, long RnglistsBase, long StmtListOffset);

    /// <summary>
    /// Reads every subprogram with an address from the DWARF sections, together with the unit
    /// context needed to resolve its ranges and file table.
    /// </summary>
    /// <param name="sections">The DWARF section bytes.</param>
    public static List<(DwarfFunction Function, UnitContext Unit)> ReadFunctions(DwarfSections sections)
    {
        var result = new List<(DwarfFunction, UnitContext)>();
        if (!sections.HasInfo) return result;

        try
        {
            var reader = new DwarfDataReader(sections.Info);
            var units = 0;
            while (reader.Remaining > 12 && units++ < MaxUnits)
            {
                var unitStart = reader.Position;
                var length = reader.ReadInitialLength(out var is64);
                var nextUnit = reader.Position + (long)length;
                if (length == 0 || nextUnit > sections.Info.Length) break;

                ReadUnit(sections, ref reader, unitStart, is64, (int)nextUnit, result);
                reader.Position = (int)nextUnit;
            }
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            // Keep the functions parsed before the damage.
        }

        return result;
    }

    private static void ReadUnit(
        DwarfSections sections, ref DwarfDataReader reader, int unitStart, bool is64, int unitEnd,
        List<(DwarfFunction, UnitContext)> result)
    {
        var version = reader.ReadU16();
        if (version is < 2 or > 5) return;

        long abbrevOffset;
        int addressSize;
        if (version >= 5)
        {
            var unitType = reader.ReadU8();
            addressSize = reader.ReadU8();
            abbrevOffset = (long)reader.ReadSectionOffset(is64);
            if (unitType is not (1 or 3)) return; // DW_UT_compile / DW_UT_partial only
        }
        else
        {
            abbrevOffset = (long)reader.ReadSectionOffset(is64);
            addressSize = reader.ReadU8();
        }

        if (addressSize is not (4 or 8)) return;
        var abbrevs = DwarfAbbrevTable.Parse(sections.Abbrev, abbrevOffset);

        // Defaults per DWARF5: the base attributes point just past each section's header.
        var unit = new UnitContext(version, is64, addressSize, BaseAddress: 0,
            StrOffsetsBase: is64 ? 16 : 8, AddrBase: 8, RnglistsBase: is64 ? 20 : 12, StmtListOffset: -1);

        var dies = 0;
        var depth = 0;
        var isRoot = true;
        while (reader.Position < unitEnd && dies++ < MaxDies)
        {
            var code = reader.ReadULeb128();
            if (code == 0)
            {
                if (--depth < 0) break;
                continue;
            }

            if (!abbrevs.TryGet(code, out var decl)) break;

            if (isRoot && decl.Tag == DwarfForm.TagCompileUnit)
            {
                unit = ReadUnitRoot(sections, ref reader, decl, unit);
            }
            else if (decl.Tag == DwarfForm.TagSubprogram)
            {
                if (ReadSubprogram(sections, ref reader, unitStart, decl, unit, abbrevs) is { } function)
                    result.Add((function, unit));
            }
            else
            {
                SkipAttributes(ref reader, decl, unit);
            }

            isRoot = false;
            if (decl.HasChildren) depth++;
        }
    }

    private static UnitContext ReadUnitRoot(
        DwarfSections sections, ref DwarfDataReader reader,
        DwarfAbbrevTable.Declaration decl, UnitContext unit)
    {
        foreach (var spec in decl.Attributes)
        {
            var value = ReadValue(sections, ref reader, spec, unit);
            switch (spec.Attribute)
            {
                case DwarfForm.AtLowPc: unit = unit with { BaseAddress = value.U }; break;
                case DwarfForm.AtStmtList: unit = unit with { StmtListOffset = (long)value.U }; break;
                case DwarfForm.AtStrOffsetsBase: unit = unit with { StrOffsetsBase = (long)value.U }; break;
                case DwarfForm.AtAddrBase: unit = unit with { AddrBase = (long)value.U }; break;
                case DwarfForm.AtRnglistsBase: unit = unit with { RnglistsBase = (long)value.U }; break;
            }
        }

        return unit;
    }

    private static DwarfFunction? ReadSubprogram(
        DwarfSections sections, ref DwarfDataReader reader, int unitStart,
        DwarfAbbrevTable.Declaration decl, UnitContext unit, DwarfAbbrevTable abbrevs)
    {
        string? name = null;
        string? linkageName = null;
        ulong lowPc = 0;
        var hasLowPc = false;
        ulong highPc = 0;
        var highPcIsAddress = false;
        var hasHighPc = false;
        long rangesOffset = -1;
        var rangesIsIndex = false;
        var declFile = -1; // v5 file tables are 0-based, so 0 is a real index
        var declLine = 0;
        long referencedDie = -1;

        foreach (var spec in decl.Attributes)
        {
            var value = ReadValue(sections, ref reader, spec, unit);
            switch (spec.Attribute)
            {
                case DwarfForm.AtName: name = value.S; break;
                case DwarfForm.AtLinkageName:
                case DwarfForm.AtMipsLinkageName: linkageName = value.S; break;
                case DwarfForm.AtLowPc: lowPc = value.U; hasLowPc = true; break;
                case DwarfForm.AtHighPc:
                    highPc = value.U;
                    highPcIsAddress = spec.Form is DwarfForm.Addr or DwarfForm.Addrx
                        or DwarfForm.Addrx1 or DwarfForm.Addrx2 or DwarfForm.Addrx3 or DwarfForm.Addrx4;
                    hasHighPc = true;
                    break;
                case DwarfForm.AtRanges:
                    rangesOffset = (long)value.U;
                    rangesIsIndex = spec.Form == DwarfForm.Rnglistx;
                    break;
                case DwarfForm.AtDeclFile: declFile = (int)value.U; break;
                case DwarfForm.AtDeclLine: declLine = (int)value.U; break;
                case DwarfForm.AtSpecification:
                case DwarfForm.AtAbstractOrigin:
                    referencedDie = value.IsSectionRelativeRef ? (long)value.U : unitStart + (long)value.U;
                    break;
            }
        }

        // Names may live on the referenced declaration; resolve one hop.
        if (name is null && linkageName is null && referencedDie >= 0)
            (name, linkageName) = ReadReferencedNames(sections, referencedDie, unit, abbrevs);

        var bestName = linkageName ?? name;
        if (bestName is null) return null;
        if (!hasLowPc && rangesOffset < 0) return null;

        var size = hasHighPc
            ? (highPcIsAddress ? (highPc > lowPc ? highPc - lowPc : 0) : highPc)
            : 0;

        return new DwarfFunction(bestName, lowPc, size, declFile, declLine,
            unit.StmtListOffset, rangesOffset, rangesIsIndex);
    }

    private static (string? Name, string? LinkageName) ReadReferencedNames(
        DwarfSections sections, long dieOffset, UnitContext unit, DwarfAbbrevTable abbrevs)
    {
        try
        {
            if (dieOffset < 0 || dieOffset >= sections.Info.Length) return (null, null);
            var reader = new DwarfDataReader(sections.Info) { Position = (int)dieOffset };
            var code = reader.ReadULeb128();
            if (code == 0 || !abbrevs.TryGet(code, out var decl)) return (null, null);

            string? name = null;
            string? linkage = null;
            foreach (var spec in decl.Attributes)
            {
                var value = ReadValue(sections, ref reader, spec, unit);
                if (spec.Attribute == DwarfForm.AtName) name = value.S;
                else if (spec.Attribute is DwarfForm.AtLinkageName or DwarfForm.AtMipsLinkageName) linkage = value.S;
            }

            return (name, linkage);
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            return (null, null);
        }
    }

    private static void SkipAttributes(
        ref DwarfDataReader reader, DwarfAbbrevTable.Declaration decl, UnitContext unit)
    {
        foreach (var spec in decl.Attributes)
            SkipValue(ref reader, spec.Form, unit);
    }

    /// <summary>A decoded attribute value: an unsigned number, a string, or both empty.</summary>
    private readonly record struct Value(ulong U, string? S, bool IsSectionRelativeRef);

    private static Value ReadValue(
        DwarfSections sections, ref DwarfDataReader reader,
        DwarfAbbrevTable.AttributeSpec spec, UnitContext unit)
    {
        var form = spec.Form;
        while (form == DwarfForm.Indirect)
            form = reader.ReadULeb128();

        switch (form)
        {
            case DwarfForm.Addr: return new Value(reader.ReadAddress(unit.AddressSize), null, false);
            case DwarfForm.Data1: return new Value(reader.ReadU8(), null, false);
            case DwarfForm.Data2: return new Value(reader.ReadU16(), null, false);
            case DwarfForm.Data4: return new Value(reader.ReadU32(), null, false);
            case DwarfForm.Data8: return new Value(reader.ReadU64(), null, false);
            case DwarfForm.Data16: reader.Skip(16); return default;
            case DwarfForm.Udata: return new Value(reader.ReadULeb128(), null, false);
            case DwarfForm.Sdata: return new Value((ulong)reader.ReadSLeb128(), null, false);
            case DwarfForm.Flag: return new Value(reader.ReadU8(), null, false);
            case DwarfForm.FlagPresent: return new Value(1, null, false);
            case DwarfForm.ImplicitConst: return new Value((ulong)spec.ImplicitConst, null, false);
            case DwarfForm.SecOffset: return new Value(reader.ReadSectionOffset(unit.Is64), null, false);
            case DwarfForm.String: return new Value(0, reader.ReadCString(), false);

            case DwarfForm.Strp:
                {
                    var offset = (long)reader.ReadSectionOffset(unit.Is64);
                    return new Value(0, new DwarfDataReader(sections.Str).ReadCStringAt(offset), false);
                }

            case DwarfForm.LineStrp:
                {
                    var offset = (long)reader.ReadSectionOffset(unit.Is64);
                    return new Value(0, new DwarfDataReader(sections.LineStr).ReadCStringAt(offset), false);
                }

            case DwarfForm.Strx: return ResolveStrx(sections, reader.ReadULeb128(), unit);
            case DwarfForm.Strx1: return ResolveStrx(sections, reader.ReadU8(), unit);
            case DwarfForm.Strx2: return ResolveStrx(sections, reader.ReadU16(), unit);
            case DwarfForm.Strx3: return ResolveStrx(sections, (ulong)(reader.ReadU16() | (reader.ReadU8() << 16)), unit);
            case DwarfForm.Strx4: return ResolveStrx(sections, reader.ReadU32(), unit);

            case DwarfForm.Addrx: return ResolveAddrx(sections, reader.ReadULeb128(), unit);
            case DwarfForm.Addrx1: return ResolveAddrx(sections, reader.ReadU8(), unit);
            case DwarfForm.Addrx2: return ResolveAddrx(sections, reader.ReadU16(), unit);
            case DwarfForm.Addrx3: return ResolveAddrx(sections, (ulong)(reader.ReadU16() | (reader.ReadU8() << 16)), unit);
            case DwarfForm.Addrx4: return ResolveAddrx(sections, reader.ReadU32(), unit);

            case DwarfForm.Ref1: return new Value(reader.ReadU8(), null, false);
            case DwarfForm.Ref2: return new Value(reader.ReadU16(), null, false);
            case DwarfForm.Ref4: return new Value(reader.ReadU32(), null, false);
            case DwarfForm.Ref8: return new Value(reader.ReadU64(), null, false);
            case DwarfForm.RefUdata: return new Value(reader.ReadULeb128(), null, false);
            case DwarfForm.RefAddr: return new Value(reader.ReadSectionOffset(unit.Is64), null, true);
            case DwarfForm.RefSig8: reader.Skip(8); return default;
            case DwarfForm.RefSup4: reader.Skip(4); return default;
            case DwarfForm.RefSup8: reader.Skip(8); return default;
            case DwarfForm.StrpSup: reader.Skip(unit.Is64 ? 8 : 4); return default;

            case DwarfForm.Rnglistx:
            case DwarfForm.Loclistx: return new Value(reader.ReadULeb128(), null, false);

            case DwarfForm.Exprloc:
            case DwarfForm.Block: reader.Skip((int)reader.ReadULeb128()); return default;
            case DwarfForm.Block1: reader.Skip(reader.ReadU8()); return default;
            case DwarfForm.Block2: reader.Skip(reader.ReadU16()); return default;
            case DwarfForm.Block4: reader.Skip((int)reader.ReadU32()); return default;

            default:
                throw new ArgumentOutOfRangeException(nameof(spec), $"unknown DWARF form 0x{form:X}");
        }
    }

    private static void SkipValue(ref DwarfDataReader reader, ulong form, UnitContext unit)
    {
        // The decode already advances correctly for every form; reuse it with empty sections
        // since skipped values never dereference other sections' bytes for their side effects.
        var spec = new DwarfAbbrevTable.AttributeSpec(0, form, 0);
        ReadValue(EmptySections, ref reader, spec, unit);
    }

    private static readonly DwarfSections EmptySections = new([], [], [], [], [], [], [], [], []);

    private static Value ResolveStrx(DwarfSections sections, ulong index, UnitContext unit)
    {
        try
        {
            var offsetSize = unit.Is64 ? 8 : 4;
            var position = unit.StrOffsetsBase + (long)index * offsetSize;
            if (position < 0 || position + offsetSize > sections.StrOffsets.Length) return default;
            var reader = new DwarfDataReader(sections.StrOffsets) { Position = (int)position };
            var strOffset = (long)reader.ReadSectionOffset(unit.Is64);
            return new Value(0, new DwarfDataReader(sections.Str).ReadCStringAt(strOffset), false);
        }
        catch (ArgumentOutOfRangeException)
        {
            return default;
        }
    }

    private static Value ResolveAddrx(DwarfSections sections, ulong index, UnitContext unit)
    {
        try
        {
            var position = unit.AddrBase + (long)index * unit.AddressSize;
            if (position < 0 || position + unit.AddressSize > sections.Addr.Length) return default;
            var reader = new DwarfDataReader(sections.Addr) { Position = (int)position };
            return new Value(reader.ReadAddress(unit.AddressSize), null, false);
        }
        catch (ArgumentOutOfRangeException)
        {
            return default;
        }
    }
}
