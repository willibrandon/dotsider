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

        // ELF and Mach-O dispatch land with their readers.
        return new NativeSymbolInfo([], NativeSymbolSource.PdataFallback,
            NativeSymbolStatus.NotApplicable, null, "unrecognized image format");
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
