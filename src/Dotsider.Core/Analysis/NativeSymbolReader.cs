using Dotsider.Core.Analysis.Dwarf;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Reads a native binary's symbols — function names, addresses, and sizes — from its debug
/// information, demangling ILC names back to managed names and merging the overlapping records
/// that different symbol sources produce. Windows native PDBs, Linux DWARF, and macOS dSYM/nlist
/// each feed the same merge and demangle pipeline through <see cref="Build"/>; when no symbols
/// exist, unwind data still yields function boundaries at lower fidelity. The public entry points
/// that dispatch on image format are added as each reader lands.
/// </summary>
public static class NativeSymbolReader
{
    /// <summary>
    /// Reads the native symbols of a binary, dispatching on image format. Managed and
    /// unrecognized images return an empty result marked <see cref="NativeSymbolStatus.NotApplicable"/>.
    /// </summary>
    /// <param name="imagePath">The binary's path, used to probe for sidecar symbol files.</param>
    /// <param name="imageBytes">The binary's raw bytes.</param>
    /// <param name="recoveredTypes">Types recovered from the binary's own metadata, for demangling.</param>
    public static NativeSymbolInfo Read(
        string imagePath,
        ReadOnlyMemory<byte> imageBytes,
        IReadOnlyList<RecoveredType> recoveredTypes)
    {
        var demangler = new IlcNameDemangler(recoveredTypes);
        var span = imageBytes.Span;

        if (span.Length >= 2 && span[0] == (byte)'M' && span[1] == (byte)'Z')
            return ReadPe(imagePath, imageBytes, demangler);

        if (ElfImageReader.IsElf(span))
            return ReadElf(imagePath, imageBytes, demangler);

        if (MachOImageReader.IsMachO(span) || MachOImageReader.IsFat(span))
            return ReadMachO(imagePath, imageBytes, demangler);

        return new NativeSymbolInfo([], NativeSymbolSource.PdataFallback,
            NativeSymbolStatus.NotApplicable, null, "unrecognized image format");
    }

    private static NativeSymbolInfo ReadMachO(string imagePath, ReadOnlyMemory<byte> imageBytes, IlcNameDemangler demangler)
    {
        var span = imageBytes.Span;
        var dsymPath = FindSidecarDirectory(imagePath);
        var dsymBytes = dsymPath is not null ? File.ReadAllBytes(dsymPath) : null;

        // Fat archives: pick the slice the dSYM identifies, else the one carrying the Native
        // AOT signal — never silently the host architecture.
        long sliceShift = 0;
        if (MachOImageReader.IsFat(span))
        {
            var slices = MachOImageReader.ReadFatSlices(span);
            var chosen = ChooseFatSlice(span, slices, dsymBytes);
            if (chosen < 0)
            {
                var names = string.Join(", ", slices.Select(s => $"0x{s.CpuType:x}"));
                return new NativeSymbolInfo([], NativeSymbolSource.MachONlist, NativeSymbolStatus.AmbiguousImage,
                    null, $"fat archive: no slice disambiguated by dSYM UUID or Native AOT signal (cputypes {names})");
            }

            sliceShift = slices[chosen].Offset;
            span = span.Slice((int)slices[chosen].Offset, (int)slices[chosen].Size);
        }

        var imageSections = MachOImageReader.ReadSectionList(span);
        var hasImageUuid = MachOImageReader.TryReadUuid(span, out var imageUuid);

        if (dsymBytes is not null)
        {
            // The UUID is the identity: when the image has one, the dSYM must carry the same.
            if (hasImageUuid
                && (!MachOImageReader.TryReadUuid(dsymBytes, out var dsymUuid)
                    || !imageUuid.AsSpan().SequenceEqual(dsymUuid)))
            {
                return FunctionStartsFallback(span, sliceShift, demangler, NativeSymbolStatus.IdMismatch,
                    $"dSYM '{Path.GetFileName(dsymPath)}' does not match the image (UUID)");
            }

            // A dSYM contributes both its DWARF and its nlist — merged, not either/or.
            var raw = new List<RawNativeSymbol>();
            AppendMachODwarfFunctions(dsymBytes, imageSections, sliceShift, raw);
            foreach (var symbol in MachOSymbolReader.ReadSymbols(dsymBytes, demangler))
                raw.Add(RemapToImage(symbol, imageSections, sliceShift));

            if (raw.Count > 0)
            {
                var diagnostic = hasImageUuid ? null : "dSYM matched without UUIDs (none present)";
                return Build(raw, demangler, NativeSymbolSource.Dsym, NativeSymbolStatus.Loaded, dsymPath, diagnostic);
            }

            return FunctionStartsFallback(span, sliceShift, demangler, NativeSymbolStatus.CorruptSymbolFile,
                $"'{Path.GetFileName(dsymPath)}' matched but contains no readable symbols");
        }

        // No dSYM: the image's own symbol table is still a named primary source.
        var own = MachOSymbolReader.ReadSymbols(span, demangler);
        if (own.Count > 0)
        {
            var shifted = sliceShift == 0 ? own : [.. own.Select(s => Shift(s, sliceShift))];
            return Build(shifted, demangler, NativeSymbolSource.MachONlist, NativeSymbolStatus.Loaded,
                imagePath, "no dSYM bundle; names from the image's symbol table");
        }

        return FunctionStartsFallback(span, sliceShift, demangler, NativeSymbolStatus.FallbackOnly,
            "no dSYM bundle and no symbol table");
    }

    /// <summary>
    /// Picks a fat slice: a single slice stands alone; otherwise the dSYM's UUID decides, then
    /// the Native AOT signal; -1 when nothing disambiguates.
    /// </summary>
    private static int ChooseFatSlice(
        ReadOnlySpan<byte> archive, IReadOnlyList<MachOImageReader.MachOFatSlice> slices, byte[]? dsymBytes)
    {
        if (slices.Count == 1) return 0;

        if (dsymBytes is not null && MachOImageReader.TryReadUuid(dsymBytes, out var dsymUuid))
        {
            for (var i = 0; i < slices.Count; i++)
            {
                var slice = archive.Slice((int)slices[i].Offset, (int)slices[i].Size);
                if (MachOImageReader.TryReadUuid(slice, out var uuid) && uuid.AsSpan().SequenceEqual(dsymUuid))
                    return i;
            }
        }

        for (var i = 0; i < slices.Count; i++)
        {
            var slice = archive.Slice((int)slices[i].Offset, (int)slices[i].Size);
            if (NativeAotDetector.Detect(slice) is not null) return i;
        }

        return -1;
    }

    /// <summary>
    /// Recovers boundaries from <c>LC_FUNCTION_STARTS</c> under the given failure status; a
    /// status of <see cref="NativeSymbolStatus.FallbackOnly"/> degrades to
    /// <see cref="NativeSymbolStatus.NoSymbolFile"/> when there are no boundaries either.
    /// </summary>
    private static NativeSymbolInfo FunctionStartsFallback(
        ReadOnlySpan<byte> imageBytes, long sliceShift, IlcNameDemangler demangler,
        NativeSymbolStatus status, string baseDiagnostic)
    {
        var boundaries = MachOSymbolReader.ReadFunctionStartBoundaries(imageBytes);
        if (boundaries.Count > 0)
        {
            var shifted = sliceShift == 0 ? boundaries : [.. boundaries.Select(b => Shift(b, sliceShift))];
            return Build(shifted, demangler, NativeSymbolSource.FunctionStartsFallback, status, null,
                baseDiagnostic + "; recovered function boundaries from LC_FUNCTION_STARTS");
        }

        var empty = status == NativeSymbolStatus.FallbackOnly ? NativeSymbolStatus.NoSymbolFile : status;
        return new NativeSymbolInfo([], NativeSymbolSource.FunctionStartsFallback, empty, null,
            baseDiagnostic + "; no LC_FUNCTION_STARTS data");
    }

    /// <summary>
    /// Probes for the dSYM bundle next to the image and returns its inner DWARF file:
    /// <c>&lt;image&gt;.dSYM/Contents/Resources/DWARF/&lt;name&gt;</c>.
    /// </summary>
    private static string? FindSidecarDirectory(string imagePath)
    {
        var name = Path.GetFileName(imagePath);
        if (string.IsNullOrEmpty(name)) return null;
        var inner = Path.Combine(imagePath + ".dSYM", "Contents", "Resources", "DWARF", name);
        return File.Exists(inner) ? inner : null;
    }

    /// <summary>Walks the dSYM's <c>__DWARF</c> sections into function records.</summary>
    private static void AppendMachODwarfFunctions(
        byte[] dsymBytes, IReadOnlyList<MachOImageReader.MachOSection> imageSections,
        long sliceShift, List<RawNativeSymbol> raw)
    {
        var sections = MachOImageReader.ReadSectionList(dsymBytes);
        var dwarf = DwarfSections.Collect(name =>
        {
            // Mach-O section names cap at 16 chars: __debug_str_offsets -> __debug_str_offs.
            var wanted = "__debug_" + name;
            if (wanted.Length > 16) wanted = wanted[..16];
            foreach (var s in sections)
            {
                if (s.Name == wanted && s.Size > 0 && s.FileOffset >= 0
                    && s.FileOffset + s.Size <= dsymBytes.Length)
                {
                    return dsymBytes.AsSpan((int)s.FileOffset, (int)s.Size).ToArray();
                }
            }

            return null;
        });

        AppendDwarfFunctions(dwarf, va => MapMachOAddress(imageSections, va, sliceShift), raw);
    }

    /// <summary>Re-anchors a dSYM-derived symbol's section and file offset onto the analyzed image.</summary>
    private static RawNativeSymbol RemapToImage(
        RawNativeSymbol symbol, IReadOnlyList<MachOImageReader.MachOSection> imageSections, long sliceShift)
    {
        var (section, fileOffset) = MapMachOAddress(imageSections, symbol.VirtualAddress, sliceShift);
        return symbol with { Section = section ?? symbol.Section, FileOffset = fileOffset };
    }

    private static (string? Section, long? FileOffset) MapMachOAddress(
        IReadOnlyList<MachOImageReader.MachOSection> sections, ulong va, long sliceShift)
    {
        foreach (var section in sections)
        {
            if (section.Address != 0 && va >= section.Address && va < section.Address + (ulong)section.Size)
                return (section.Name, sliceShift + section.FileOffset + (long)(va - section.Address));
        }

        return (null, null);
    }

    /// <summary>Shifts a fat-slice-relative file offset to the whole archive.</summary>
    private static RawNativeSymbol Shift(RawNativeSymbol symbol, long sliceShift) =>
        symbol.FileOffset is { } offset ? symbol with { FileOffset = offset + sliceShift } : symbol;

    private static NativeSymbolInfo ReadElf(string imagePath, ReadOnlyMemory<byte> imageBytes, IlcNameDemangler demangler)
    {
        var span = imageBytes.Span;
        var imageSections = ElfImageReader.ReadSections(span);

        // Choose the symbol source: the image itself when it still carries DWARF, else a
        // sidecar validated by build id / debuglink CRC.
        byte[] symbolBytes;
        string symbolPath;
        string? diagnostic = null;
        if (ElfImageReader.TryGetSection(span, ".debug_info", out _))
        {
            symbolBytes = imageBytes.ToArray();
            symbolPath = imagePath;
        }
        else if (FindDbgSidecar(imagePath, span) is { } sidecar)
        {
            if (sidecar.Match == ElfSidecarMatch.Mismatched)
            {
                return BoundaryFallback(span, demangler, NativeSymbolStatus.IdMismatch,
                    $"debug sidecar '{Path.GetFileName(sidecar.Path)}' does not match the image (build id or debuglink CRC)");
            }

            symbolBytes = sidecar.Bytes;
            symbolPath = sidecar.Path;
            if (sidecar.Match == ElfSidecarMatch.LooseMatch)
                diagnostic = "sidecar matched by machine and debug info only (no build id or debuglink)";
        }
        else
        {
            return BoundaryFallback(span, demangler, NativeSymbolStatus.FallbackOnly,
                "no debug sidecar");
        }

        var raw = new List<RawNativeSymbol>();
        ReadDwarfFunctions(symbolBytes, imageSections, raw);
        raw.AddRange(ElfSymtabReader.ReadDataSymbols(symbolBytes, imageSections));

        return raw.Count > 0
            ? Build(raw, demangler, NativeSymbolSource.Dwarf, NativeSymbolStatus.Loaded, symbolPath, diagnostic)
            : BoundaryFallback(span, demangler, NativeSymbolStatus.CorruptSymbolFile,
                $"'{Path.GetFileName(symbolPath)}' matched but contains no readable symbols");
    }

    /// <summary>
    /// Recovers boundaries from <c>.eh_frame</c> under the given failure status; a status of
    /// <see cref="NativeSymbolStatus.FallbackOnly"/> degrades to
    /// <see cref="NativeSymbolStatus.NoSymbolFile"/> when there are no boundaries either.
    /// </summary>
    private static NativeSymbolInfo BoundaryFallback(
        ReadOnlySpan<byte> imageBytes, IlcNameDemangler demangler,
        NativeSymbolStatus status, string baseDiagnostic)
    {
        var boundaries = EhFrameReader.ReadBoundaries(imageBytes);
        if (boundaries.Count > 0)
        {
            return Build(boundaries, demangler, NativeSymbolSource.EhFrameFallback, status, null,
                baseDiagnostic + "; recovered function boundaries from .eh_frame");
        }

        var empty = status == NativeSymbolStatus.FallbackOnly ? NativeSymbolStatus.NoSymbolFile : status;
        return new NativeSymbolInfo([], NativeSymbolSource.EhFrameFallback, empty, null,
            baseDiagnostic + "; no .eh_frame data");
    }

    /// <summary>
    /// Probes for a debug sidecar next to the image — the <c>.gnu_debuglink</c>-named file
    /// first, then <c>&lt;name&gt;.dbg</c> — and validates its identity. The first matching
    /// candidate wins; when only mismatching candidates exist, the first is reported.
    /// </summary>
    private static (string Path, byte[] Bytes, ElfSidecarMatch Match)? FindDbgSidecar(
        string imagePath, ReadOnlySpan<byte> image)
    {
        var directory = Path.GetDirectoryName(imagePath);
        if (string.IsNullOrEmpty(directory)) return null;

        var candidates = new List<string>(2);
        if (ElfImageReader.TryReadDebugLink(image, out var linkName, out _) && linkName.Length > 0)
            candidates.Add(Path.Combine(directory, Path.GetFileName(linkName)));
        var conventional = Path.Combine(directory, Path.GetFileNameWithoutExtension(imagePath) + ".dbg");
        if (!candidates.Contains(conventional)) candidates.Add(conventional);

        (string, byte[], ElfSidecarMatch)? mismatch = null;
        foreach (var candidate in candidates)
        {
            if (!File.Exists(candidate)) continue;
            var bytes = File.ReadAllBytes(candidate);
            var match = ElfSidecarIdentity.Check(image, bytes);
            if (match != ElfSidecarMatch.Mismatched) return (candidate, bytes, match);
            mismatch ??= (candidate, bytes, match);
        }

        return mismatch;
    }

    /// <summary>
    /// Walks the ELF symbol source's DWARF into function records, mapping addresses through the
    /// analyzed image's sections, not the sidecar's.
    /// </summary>
    private static void ReadDwarfFunctions(
        byte[] symbolBytes, IReadOnlyList<ElfImageReader.ElfSection> imageSections, List<RawNativeSymbol> raw)
    {
        var dwarf = DwarfSections.Collect(name =>
            ElfImageReader.TryGetSection(symbolBytes, ".debug_" + name, out var s)
                ? ElfImageReader.ReadSectionBytes(symbolBytes, s)
                : null);
        AppendDwarfFunctions(dwarf, va =>
            ElfImageReader.TryMapAddress(imageSections, va, out var name, out var offset)
                ? (name, offset)
                : (null, null), raw);
    }

    /// <summary>
    /// Walks DWARF sections into function records: names from the DIE walk, extents from
    /// <c>low_pc</c>/<c>high_pc</c> or range lists, and source attribution from the decl
    /// attributes with the line-program row as fallback. The address map supplies the analyzed
    /// image's section names and file offsets.
    /// </summary>
    private static void AppendDwarfFunctions(
        DwarfSections dwarf, Func<ulong, (string? Section, long? FileOffset)> mapAddress,
        List<RawNativeSymbol> raw)
    {
        if (!dwarf.HasInfo) return;

        var lineCache = new Dictionary<long, DwarfLineProgram?>();
        foreach (var (function, unit) in DwarfReader.ReadFunctions(dwarf))
        {
            var lowPc = function.LowPc;
            var size = function.Size;
            if (function.RangesOffset >= 0
                && DwarfRangeLists.TryResolve(dwarf, function.RangesOffset, function.RangesIsRnglistx,
                    unit, out var rangeStart, out var rangeSize))
            {
                lowPc = rangeStart;
                size = rangeSize;
            }

            if (lowPc == 0) continue;

            string? sourceFile = null;
            int? line = null;
            if (GetLineProgram(dwarf, unit.StmtListOffset, lineCache) is { } program)
            {
                var (file, lineNumber) = program.ResolveSource(function.DeclFile, function.DeclLine, lowPc);
                sourceFile = file;
                line = lineNumber;
            }

            var (sectionName, fileOffset) = mapAddress(lowPc);
            raw.Add(new RawNativeSymbol(
                Name: function.Name,
                VirtualAddress: lowPc,
                Rva: null,
                FileOffset: fileOffset,
                Section: sectionName,
                Size: (long)size,
                IsData: false,
                IsBoundary: false,
                SourceFile: sourceFile,
                Line: line));
        }
    }

    private static DwarfLineProgram? GetLineProgram(
        DwarfSections dwarf, long stmtListOffset, Dictionary<long, DwarfLineProgram?> cache)
    {
        if (stmtListOffset < 0) return null;
        if (!cache.TryGetValue(stmtListOffset, out var program))
        {
            program = DwarfLineProgram.Parse(dwarf, stmtListOffset);
            cache[stmtListOffset] = program;
        }

        return program;
    }

    private static NativeSymbolInfo ReadPe(string imagePath, ReadOnlyMemory<byte> imageBytes, IlcNameDemangler demangler)
    {
        var codeView = PeCodeView.TryRead(imageBytes.Span);

        // A same-directory PDB whose GUID and age match is the rich source.
        if (codeView is { } id && FindMatchingPdb(imagePath, id) is { } pdbPath)
        {
            var raw = NativePdb.NativePdbReader.Read(File.ReadAllBytes(pdbPath), imageBytes);
            if (raw.Count > 0)
                return Build(raw, demangler, NativeSymbolSource.NativePdb, NativeSymbolStatus.Loaded, pdbPath, null);
        }

        // No usable PDB: recover function boundaries from .pdata.
        var boundaries = PdataReader.ReadBoundaries(imageBytes);
        return boundaries.Count > 0
            ? Build(boundaries, demangler, NativeSymbolSource.PdataFallback, NativeSymbolStatus.FallbackOnly,
                null, "no matching PDB; recovered function boundaries from .pdata")
            : new NativeSymbolInfo([], NativeSymbolSource.PdataFallback, NativeSymbolStatus.NoSymbolFile,
                null, "no PDB and no .pdata exception directory");
    }

    private static string? FindMatchingPdb(string imagePath, PeCodeView.CodeViewId id)
    {
        var directory = Path.GetDirectoryName(imagePath);
        if (string.IsNullOrEmpty(directory)) return null;

        var candidates = new List<string>(2);
        if (!string.IsNullOrEmpty(id.PdbPath))
            candidates.Add(Path.Combine(directory, Path.GetFileName(id.PdbPath)));
        candidates.Add(Path.Combine(directory, Path.GetFileNameWithoutExtension(imagePath) + ".pdb"));

        foreach (var path in candidates)
        {
            if (File.Exists(path)
                && NativePdb.NativePdbReader.TryReadPdbId(path, out var guid, out var age)
                && guid == id.Guid && age == id.Age)
            {
                return path;
            }
        }

        return null;
    }

    /// <summary>
    /// Demangles, classifies, sizes, and merges raw reader output into the public symbol model.
    /// Records that share a virtual address collapse to one primary — the richest wins and the
    /// rest become aliases — so no byte is counted twice; unsized symbols take the distance to the
    /// next symbol as their size.
    /// </summary>
    /// <param name="raw">The symbols a format reader produced.</param>
    /// <param name="demangler">The demangler seeded from the binary's recovered metadata.</param>
    /// <param name="source">The source the symbols came from.</param>
    /// <param name="status">The probe status.</param>
    /// <param name="path">The symbol file path, or null.</param>
    /// <param name="diagnostic">A human-readable note on the outcome, or null.</param>
    internal static NativeSymbolInfo Build(
        IReadOnlyList<RawNativeSymbol> raw,
        IlcNameDemangler demangler,
        NativeSymbolSource source,
        NativeSymbolStatus status,
        string? path,
        string? diagnostic)
    {
        if (raw.Count == 0)
            return new NativeSymbolInfo([], source, status, path, diagnostic);

        // Order by address, then by richness so the primary of each address group comes first.
        var ordered = raw
            .OrderBy(r => r.VirtualAddress)
            .ThenByDescending(Richness)
            .ToList();

        // Collapse same-address records into a primary plus aliases.
        var primaries = new List<RawNativeSymbol>();
        var aliasesByIndex = new List<List<string>>();
        foreach (var symbol in ordered)
        {
            if (primaries.Count > 0 && primaries[^1].VirtualAddress == symbol.VirtualAddress)
            {
                if (!string.Equals(primaries[^1].Name, symbol.Name, StringComparison.Ordinal)
                    && !aliasesByIndex[^1].Contains(symbol.Name))
                {
                    aliasesByIndex[^1].Add(symbol.Name);
                }

                // Keep the richer record's size/line if the primary lacked them.
                if (primaries[^1].Size == 0 && symbol.Size > 0)
                    primaries[^1] = primaries[^1] with { Size = symbol.Size };
                continue;
            }

            primaries.Add(symbol);
            aliasesByIndex.Add([]);
        }

        // Size unsized symbols by the distance to the next symbol's address.
        for (var i = 0; i < primaries.Count; i++)
        {
            if (primaries[i].Size > 0) continue;
            var next = i + 1 < primaries.Count ? primaries[i + 1].VirtualAddress : primaries[i].VirtualAddress;
            var gap = (long)(next - primaries[i].VirtualAddress);
            primaries[i] = primaries[i] with { Size = gap > 0 ? gap : 0 };
        }

        var symbols = new List<NativeSymbol>(primaries.Count);
        for (var i = 0; i < primaries.Count; i++)
        {
            var p = primaries[i];
            NativeSymbolKind kind;
            string? managedName;
            bool exact;

            if (p.IsBoundary)
            {
                kind = NativeSymbolKind.Boundary;
                managedName = null;
                exact = false;
            }
            else
            {
                var demangled = demangler.Demangle(p.Name);
                kind = demangled.Kind;
                managedName = demangled.ManagedName;
                exact = demangled.IsExactMatch;
                // A symbol the demangler read as a function but that lives in a data section is data.
                if (kind == NativeSymbolKind.Function && p.IsData)
                    kind = NativeSymbolKind.Data;
            }

            symbols.Add(new NativeSymbol(
                Name: p.Name,
                ManagedName: managedName,
                VirtualAddress: p.VirtualAddress,
                Rva: p.Rva,
                FileOffset: p.FileOffset,
                Section: p.Section,
                Size: p.Size,
                Kind: kind,
                SourceFile: p.SourceFile,
                Line: p.Line,
                IsExactMatch: exact,
                Aliases: aliasesByIndex[i]));
        }

        return new NativeSymbolInfo(symbols, source, status, path, diagnostic);
    }

    // Rich records (procedures/data with sizes and line info) outrank named-only publics, which
    // outrank nameless boundaries — so the primary of an address group is the most informative.
    private static int Richness(RawNativeSymbol s)
    {
        if (s.IsBoundary) return 0;
        var rank = 1;
        if (s.Size > 0) rank += 2;
        if (s.SourceFile is not null) rank += 1;
        return rank;
    }
}
