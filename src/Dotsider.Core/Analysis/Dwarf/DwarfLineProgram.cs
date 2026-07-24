namespace Dotsider.Core.Analysis.Dwarf;

/// <summary>
/// One compilation unit's <c>.debug_line</c> program: the file table (v4 directory/file lists or
/// v5 form-described entry tables, plus <c>DW_LNE_define_file</c> additions) and the decoded
/// row machine. Source attribution treats <c>DW_AT_decl_file</c>/<c>DW_AT_decl_line</c> as
/// primary — <see cref="FileName"/> resolves the index with the version's numbering — and the
/// row covering an address (<see cref="TryFindLine"/>) as the fallback. A malformed program body
/// keeps the rows decoded before the damage; a malformed header yields no program.
/// </summary>
internal sealed class DwarfLineProgram
{
    // Standard opcodes (DWARF5 §6.2.5.2).
    private const byte LnsCopy = 1;
    private const byte LnsAdvancePc = 2;
    private const byte LnsAdvanceLine = 3;
    private const byte LnsSetFile = 4;
    private const byte LnsSetColumn = 5;
    private const byte LnsConstAddPc = 8;
    private const byte LnsFixedAdvancePc = 9;
    private const byte LnsSetIsa = 12;

    // Extended opcodes (DWARF5 §6.2.5.3).
    private const byte LneEndSequence = 1;
    private const byte LneSetAddress = 2;
    private const byte LneDefineFile = 3;

    // v5 entry-format content types (DWARF5 §6.2.4.1).
    private const ulong LnctPath = 1;
    private const ulong LnctDirectoryIndex = 2;

    private const int MaxRows = 1 << 20;
    private const int MaxV5TableEntries = 65_536;

    private readonly record struct Row(ulong Address, int File, int Line, bool EndSequence);

    private readonly string[] _files;
    private readonly bool _oneBasedFiles;
    private readonly Row[] _rows; // sorted by address, end-sequence rows first on ties

    private DwarfLineProgram(string[] files, bool oneBasedFiles, Row[] rows)
    {
        _files = files;
        _oneBasedFiles = oneBasedFiles;
        _rows = rows;
    }

    /// <summary>
    /// Resolves a file-table index to its directory-joined name, honoring the version's
    /// numbering (v4 tables are 1-based, v5 are 0-based), or null when out of range.
    /// </summary>
    /// <param name="index">The <c>DW_AT_decl_file</c> or row-machine file value.</param>
    public string? FileName(int index)
    {
        var i = _oneBasedFiles ? index - 1 : index;
        return i >= 0 && i < _files.Length ? _files[i] : null;
    }

    /// <summary>Finds the row covering <paramref name="address"/> and returns its file and line.</summary>
    /// <param name="address">The address to attribute, typically a function's <c>low_pc</c>.</param>
    /// <param name="file">The covering row's file name, or null when its index is bad.</param>
    /// <param name="line">The covering row's line.</param>
    public bool TryFindLine(ulong address, out string? file, out int line)
    {
        file = null;
        line = 0;

        var lo = 0;
        var hi = _rows.Length - 1;
        var best = -1;
        while (lo <= hi)
        {
            var mid = lo + ((hi - lo) >> 1);
            if (_rows[mid].Address <= address)
            {
                best = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        if (best < 0 || _rows[best].EndSequence) return false;
        file = FileName(_rows[best].File);
        line = _rows[best].Line;
        return true;
    }

    /// <summary>
    /// Attributes a function to a source file and line: the declaration attributes are primary,
    /// and the row at <paramref name="address"/> fills whichever of them is absent.
    /// </summary>
    /// <param name="declFile">The <c>DW_AT_decl_file</c> index, or -1 when the DIE carried none.</param>
    /// <param name="declLine">The <c>DW_AT_decl_line</c> value, or 0 when absent.</param>
    /// <param name="address">The function's start address, for the row fallback.</param>
    public (string? File, int? Line) ResolveSource(int declFile, int declLine, ulong address)
    {
        var file = declFile >= 0 ? FileName(declFile) : null;
        int? line = declLine > 0 ? declLine : null;
        if ((file is null || line is null) && TryFindLine(address, out var rowFile, out var rowLine))
        {
            file ??= rowFile;
            line ??= rowLine > 0 ? rowLine : null;
        }

        return (file, line);
    }

    /// <summary>
    /// Parses the line program at <paramref name="offset"/> in <c>.debug_line</c>, or null when
    /// the header is malformed or the offset is out of range.
    /// </summary>
    /// <param name="sections">The DWARF section bytes.</param>
    /// <param name="offset">The CU's <c>DW_AT_stmt_list</c> offset.</param>
    public static DwarfLineProgram? Parse(DwarfSections sections, long offset)
    {
        if (offset < 0 || offset >= sections.Line.Length) return null;
        try
        {
            return ParseCore(sections, offset);
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static DwarfLineProgram? ParseCore(DwarfSections sections, long offset)
    {
        var reader = new DwarfDataReader(sections.Line) { Position = (int)offset };
        var unitLength = reader.ReadInitialLength(out var is64);
        var end = reader.Position + (long)unitLength;
        if (unitLength == 0 || end > sections.Line.Length) return null;

        var version = reader.ReadU16();
        if (version is < 2 or > 5) return null;
        if (version >= 5) reader.Skip(2); // address_size, segment_selector_size

        var headerLength = (long)reader.ReadSectionOffset(is64);
        var programStart = reader.Position + headerLength;
        if (programStart < reader.Position || programStart > end) return null;

        // Parse the prologue through a bounded reader so malformed tables cannot consume bytes
        // from the line-number program that follows.
        var headerReader = new DwarfDataReader(sections.Line.AsSpan(0, (int)programStart))
        {
            Position = reader.Position,
        };
        var minimumInstructionLength = headerReader.ReadU8();
        if (version >= 4) headerReader.Skip(1); // maximum_operations_per_instruction
        headerReader.Skip(1); // default_is_stmt
        var lineBase = (sbyte)headerReader.ReadU8();
        var lineRange = headerReader.ReadU8();
        var opcodeBase = headerReader.ReadU8();
        if (lineRange == 0 || opcodeBase == 0) return null;
        var standardLengths = new byte[opcodeBase];
        for (var i = 1; i < opcodeBase; i++) standardLengths[i] = headerReader.ReadU8();

        var directories = new List<string>();
        var files = new List<string>();
        bool oneBased;
        if (version >= 5)
        {
            oneBased = false;
            if (!TryReadV5Table(
                    sections, ref headerReader, is64, directories, isFileTable: false, files)
                || !TryReadV5Table(
                    sections, ref headerReader, is64, directories, isFileTable: true, files))
            {
                return null;
            }
        }
        else
        {
            oneBased = true;
            directories.Add(""); // index 0 = the compilation directory, unknown here
            while (true)
            {
                var dir = headerReader.ReadCString();
                if (dir.Length == 0) break;
                directories.Add(dir);
            }

            while (true)
            {
                var name = headerReader.ReadCString();
                if (name.Length == 0) break;
                var dirIndex = (int)headerReader.ReadULeb128();
                headerReader.ReadULeb128(); // mtime
                headerReader.ReadULeb128(); // length
                files.Add(Join(directories, dirIndex, name));
            }
        }

        if (headerReader.Position != programStart) return null;

        reader.Position = (int)programStart;
        var rows = RunMachine(ref reader, (int)end, minimumInstructionLength, lineBase, lineRange,
            opcodeBase, standardLengths, directories, files);

        rows.Sort(static (x, y) => x.Address != y.Address
            ? x.Address.CompareTo(y.Address)
            : y.EndSequence.CompareTo(x.EndSequence));
        return new DwarfLineProgram([.. files], oneBased, [.. rows]);
    }

    private static List<Row> RunMachine(
        ref DwarfDataReader reader, int end, byte minimumInstructionLength, sbyte lineBase,
        byte lineRange, byte opcodeBase, byte[] standardLengths,
        List<string> directories, List<string> files)
    {
        var rows = new List<Row>();
        ulong address = 0;
        var file = 1;
        var line = 1;

        try
        {
            while (reader.Position < end && rows.Count < MaxRows)
            {
                var opcode = reader.ReadU8();
                if (opcode >= opcodeBase)
                {
                    var adjusted = opcode - opcodeBase;
                    address += (ulong)(adjusted / lineRange) * minimumInstructionLength;
                    line += lineBase + adjusted % lineRange;
                    rows.Add(new Row(address, file, line, EndSequence: false));
                }
                else if (opcode == 0)
                {
                    var length = (long)reader.ReadULeb128();
                    var next = reader.Position + length;
                    var sub = length > 0 ? reader.ReadU8() : (byte)0;
                    switch (sub)
                    {
                        case LneEndSequence:
                            rows.Add(new Row(address, file, line, EndSequence: true));
                            address = 0;
                            file = 1;
                            line = 1;
                            break;

                        case LneSetAddress:
                            address = 0;
                            for (var i = 0; i < length - 1 && i < 8; i++)
                                address |= (ulong)reader.ReadU8() << (8 * i);
                            break;

                        case LneDefineFile:
                        {
                            var name = reader.ReadCString();
                            var dirIndex = (int)reader.ReadULeb128();
                            files.Add(Join(directories, dirIndex, name));
                            break;
                        }
                    }

                    if (next < reader.Position || next > end) break;
                    reader.Position = (int)next;
                }
                else
                {
                    switch (opcode)
                    {
                        case LnsCopy: rows.Add(new Row(address, file, line, EndSequence: false)); break;
                        case LnsAdvancePc: address += reader.ReadULeb128() * minimumInstructionLength; break;
                        case LnsAdvanceLine: line += (int)reader.ReadSLeb128(); break;
                        case LnsSetFile: file = (int)reader.ReadULeb128(); break;
                        case LnsSetColumn: reader.ReadULeb128(); break;
                        case LnsConstAddPc:
                            address += (ulong)((255 - opcodeBase) / lineRange) * minimumInstructionLength;
                            break;
                        case LnsFixedAdvancePc: address += reader.ReadU16(); break;
                        case LnsSetIsa: reader.ReadULeb128(); break;
                        default:
                            for (var i = 0; i < standardLengths[opcode]; i++) reader.ReadULeb128();
                            break;
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            // Keep the rows decoded before the damage.
        }

        return rows;
    }

    private static bool TryReadV5Table(
        DwarfSections sections, ref DwarfDataReader reader, bool is64,
        List<string> directories, bool isFileTable, List<string> files)
    {
        var formatCount = reader.ReadU8();
        var formats = new (ulong Content, ulong Form)[formatCount];
        var hasPath = false;
        var hasUnsupportedForm = false;
        var minimumEntrySize = 0;
        for (var i = 0; i < formatCount; i++)
        {
            var content = reader.ReadULeb128();
            var form = reader.ReadULeb128();
            formats[i] = (content, form);
            hasPath |= content == LnctPath;

            var minimumValueSize = MinimumEntryValueSize(form, is64);
            if (minimumValueSize == 0)
                hasUnsupportedForm = true;
            else
                minimumEntrySize += minimumValueSize;
        }

        var count = reader.ReadULeb128();
        if (count == 0) return true;
        if (!hasPath || hasUnsupportedForm || count > MaxV5TableEntries)
            return false;
        if (count > (ulong)(reader.Remaining / minimumEntrySize))
            return false;

        for (ulong i = 0; i < count; i++)
        {
            var entryStart = reader.Position;
            string? path = null;
            var dirIndex = 0;
            foreach (var (content, form) in formats)
            {
                if (ReadEntryValue(sections, ref reader, form, is64) is not { } value)
                    return false;
                if (content == LnctPath)
                {
                    if (value.S is null) return false;
                    path = value.S;
                }
                else if (content == LnctDirectoryIndex)
                {
                    if (value.U > int.MaxValue) return false;
                    dirIndex = (int)value.U;
                }
            }

            if (reader.Position <= entryStart || reader.Remaining < 0 || path is null)
                return false;

            if (isFileTable) files.Add(Join(directories, dirIndex, path));
            else directories.Add(path);
        }

        return true;
    }

    private static int MinimumEntryValueSize(ulong form, bool is64) =>
        form switch
        {
            DwarfForm.String or DwarfForm.Udata or DwarfForm.Data1 or DwarfForm.Block => 1,
            DwarfForm.Data2 => 2,
            DwarfForm.Data4 => 4,
            DwarfForm.Data8 => 8,
            DwarfForm.Data16 => 16,
            DwarfForm.Strp or DwarfForm.LineStrp => is64 ? 8 : 4,
            _ => 0,
        };

    private static (ulong U, string? S)? ReadEntryValue(
        DwarfSections sections, ref DwarfDataReader reader, ulong form, bool is64)
    {
        switch (form)
        {
            case DwarfForm.String: return (0, reader.ReadCString());
            case DwarfForm.Strp:
            {
                var offset = (long)reader.ReadSectionOffset(is64);
                return (0, new DwarfDataReader(sections.Str).ReadCStringAt(offset));
            }

            case DwarfForm.LineStrp:
            {
                var offset = (long)reader.ReadSectionOffset(is64);
                return (0, new DwarfDataReader(sections.LineStr).ReadCStringAt(offset));
            }

            case DwarfForm.Udata: return (reader.ReadULeb128(), null);
            case DwarfForm.Data1: return (reader.ReadU8(), null);
            case DwarfForm.Data2: return (reader.ReadU16(), null);
            case DwarfForm.Data4: return (reader.ReadU32(), null);
            case DwarfForm.Data8: return (reader.ReadU64(), null);
            case DwarfForm.Data16: reader.Skip(16); return (0, null);
            case DwarfForm.Block: reader.Skip((int)reader.ReadULeb128()); return (0, null);
            default: return null; // unknown form: entry sizes unknowable from here on
        }
    }

    private static string Join(List<string> directories, int dirIndex, string name)
    {
        if (name.StartsWith('/') || name.Contains(':')) return name; // already rooted
        var dir = dirIndex >= 0 && dirIndex < directories.Count ? directories[dirIndex] : "";
        return dir.Length == 0 ? name : $"{dir}/{name}";
    }
}
