using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the ELF arm of <see cref="NativeSymbolReader"/>: every probe outcome with synthetic
/// images on all platforms — unstripped self, matched/loose/mismatched sidecars, corrupt debug
/// info, and the <c>.eh_frame</c> fallback chain — plus the real NativeAOT fixture on the Linux
/// leg (where its symbol file is a <c>.dbg</c>).
/// </summary>
[Collection("SampleAssemblies")]
public class NativeSymbolReaderElfTests(SampleAssemblyFixture samples)
{
    private static readonly byte[] IdA = [0xDE, 0xAD, 0xBE, 0xEF, 1, 2, 3, 4];
    private static readonly byte[] IdB = [0xCA, 0xFE, 0xBA, 0xBE, 5, 6, 7, 8];

    private bool HasDbg =>
        samples.NativeAotConsoleSymbols is not null
        && samples.NativeAotConsoleSymbols.EndsWith(".dbg", StringComparison.OrdinalIgnoreCase);

    /// <summary>Builds one-function .debug_info/.debug_abbrev blobs (v4, name + low_pc + size).</summary>
    private static (byte[] Info, byte[] Abbrev) MinimalDwarf(string name, ulong lowPc, uint size)
    {
        var abbrev = new DwarfBlob()
            .ULeb(1).ULeb(0x11).U8(1).ULeb(0).ULeb(0)                     // compile_unit, children
            .ULeb(2).ULeb(0x2E).U8(0)                                     // subprogram
            .ULeb(0x03).ULeb(0x08)                                        //   name: string
            .ULeb(0x11).ULeb(0x01)                                        //   low_pc: addr
            .ULeb(0x12).ULeb(0x06)                                        //   high_pc: data4
            .ULeb(0).ULeb(0)
            .ULeb(0);

        var dies = new DwarfBlob()
            .ULeb(1)
            .ULeb(2).CStr(name).U64(lowPc).U32(size)
            .ULeb(0);
        var body = new DwarfBlob().U16(4).U32(0).U8(8).Bytes(dies.ToArray());
        var info = new DwarfBlob().U32((uint)body.Length).Bytes(body.ToArray()).ToArray();
        return (info, abbrev.ToArray());
    }

    private static string Write(string directory, string name, byte[] bytes)
    {
        var path = Path.Combine(directory, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>Verifies an unstripped image reads its own DWARF: Loaded, path = the image.</summary>
    [Fact(Timeout = 30_000)]
    public void Read_UnstrippedElf_ReadsOwnDwarf()
    {
        var dir = Directory.CreateTempSubdirectory("dotsider-elf-");
        try
        {
            var (info, abbrev) = MinimalDwarf("frost_main", 0x1010, 0x40);
            var exePath = Write(dir.FullName, "app", SyntheticImageBuilders.BuildElf(
                (".text", 0x1000, new byte[0x100]),
                (".debug_info", 0, info),
                (".debug_abbrev", 0, abbrev)));

            var result = NativeSymbolReader.Read(exePath, File.ReadAllBytes(exePath), []);

            Assert.Equal(NativeSymbolSource.Dwarf, result.Source);
            Assert.Equal(NativeSymbolStatus.Loaded, result.Status);
            Assert.Equal(exePath, result.Path);
            var symbol = Assert.Single(result.Symbols);
            Assert.Equal("frost_main", symbol.Name);
            Assert.Equal(0x1010UL, symbol.VirtualAddress);
            Assert.Equal(0x40, symbol.Size);
            Assert.Equal(".text", symbol.Section);
            Assert.NotNull(symbol.FileOffset);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies a build-id-matched sidecar loads: DWARF functions plus symtab data with exact
    /// sizes, addresses mapped through the image's sections.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Read_StrippedWithMatchingSidecar_LoadsDwarfAndSymtabData()
    {
        var dir = Directory.CreateTempSubdirectory("dotsider-elf-");
        try
        {
            var exePath = Write(dir.FullName, "app", SyntheticImageBuilders.BuildElf(
                (".text", 0x1000, new byte[0x100]),
                (".data", 0x2000, new byte[0x100]),
                (".note.gnu.build-id", 0, SyntheticImageBuilders.GnuBuildIdNote(IdA))));

            var (info, abbrev) = MinimalDwarf("frost_main", 0x1010, 0x40);
            var strtab = "\0_ZTV6Widget\0"u8.ToArray();
            var symtab = new byte[48]; // null entry + one object
            System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(symtab.AsSpan(24), 1);
            symtab[28] = 1; // STT_OBJECT
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(symtab.AsSpan(30), 1);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(symtab.AsSpan(32), 0x2010);
            System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(symtab.AsSpan(40), 0x18);
            Write(dir.FullName, "app.dbg", SyntheticImageBuilders.BuildElf(
                (".note.gnu.build-id", 0, SyntheticImageBuilders.GnuBuildIdNote(IdA), 1u, 0u),
                (".debug_info", 0, info, 1u, 0u),
                (".debug_abbrev", 0, abbrev, 1u, 0u),
                (".symtab", 0, symtab, 2u, 5u),
                (".strtab", 0, strtab, 3u, 0u)));

            var result = NativeSymbolReader.Read(exePath, File.ReadAllBytes(exePath), []);

            Assert.Equal(NativeSymbolStatus.Loaded, result.Status);
            Assert.Equal(NativeSymbolSource.Dwarf, result.Source);
            Assert.EndsWith("app.dbg", result.Path);
            Assert.Null(result.Diagnostic);

            var function = Assert.Single(result.Symbols, s => s.Kind == NativeSymbolKind.Function);
            Assert.Equal("frost_main", function.Name);
            Assert.Equal(".text", function.Section);

            var data = Assert.Single(result.Symbols, s => s.Kind == NativeSymbolKind.MethodTable);
            Assert.Equal("_ZTV6Widget", data.Name);
            Assert.Equal(0x18, data.Size); // exact st_size
            Assert.Equal(".data", data.Section);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>Verifies a <c>.gnu_debuglink</c>-named sidecar is found and CRC-validated.</summary>
    [Fact(Timeout = 30_000)]
    public void Read_DebugLinkNamedSidecar_FoundAndCrcValidated()
    {
        var dir = Directory.CreateTempSubdirectory("dotsider-elf-");
        try
        {
            var (info, abbrev) = MinimalDwarf("fn", 0x1010, 0x10);
            var sidecarBytes = SyntheticImageBuilders.BuildElf(
                (".debug_info", 0, info),
                (".debug_abbrev", 0, abbrev));
            Write(dir.FullName, "custom.dbg", sidecarBytes);

            var exePath = Write(dir.FullName, "app", SyntheticImageBuilders.BuildElf(
                (".text", 0x1000, new byte[0x100]),
                (".gnu_debuglink", 0, SyntheticImageBuilders.GnuDebugLink(
                    "custom.dbg", Crc32.Compute(sidecarBytes)))));

            var result = NativeSymbolReader.Read(exePath, File.ReadAllBytes(exePath), []);

            Assert.Equal(NativeSymbolStatus.Loaded, result.Status);
            Assert.EndsWith("custom.dbg", result.Path);
            Assert.Null(result.Diagnostic); // CRC is a real signal, not a loose match
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies a signal-free image accepts its sidecar loosely, with a diagnostic saying so.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Read_NoSignals_LooseMatchCarriesDiagnostic()
    {
        var dir = Directory.CreateTempSubdirectory("dotsider-elf-");
        try
        {
            var exePath = Write(dir.FullName, "app", SyntheticImageBuilders.BuildElf(
                (".text", 0x1000, new byte[0x100])));
            var (info, abbrev) = MinimalDwarf("fn", 0x1010, 0x10);
            Write(dir.FullName, "app.dbg", SyntheticImageBuilders.BuildElf(
                (".debug_info", 0, info),
                (".debug_abbrev", 0, abbrev)));

            var result = NativeSymbolReader.Read(exePath, File.ReadAllBytes(exePath), []);

            Assert.Equal(NativeSymbolStatus.Loaded, result.Status);
            Assert.NotNull(result.Diagnostic);
            Assert.Contains("machine and debug info only", result.Diagnostic);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies a mismatching sidecar is rejected as <see cref="NativeSymbolStatus.IdMismatch"/>,
    /// naming the sidecar, with boundaries recovered when <c>.eh_frame</c> allows.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Read_MismatchedSidecar_ReportsIdMismatch()
    {
        var dir = Directory.CreateTempSubdirectory("dotsider-elf-");
        try
        {
            var exePath = Write(dir.FullName, "app", SyntheticImageBuilders.BuildElf(
                (".text", 0x1000, new byte[0x100]),
                (".note.gnu.build-id", 0, SyntheticImageBuilders.GnuBuildIdNote(IdA)),
                (".eh_frame", 0x3000, SyntheticImageBuilders.EhFrame((0x1010, 0x40)))));
            var (info, abbrev) = MinimalDwarf("fn", 0x1010, 0x10);
            Write(dir.FullName, "app.dbg", SyntheticImageBuilders.BuildElf(
                (".note.gnu.build-id", 0, SyntheticImageBuilders.GnuBuildIdNote(IdB)),
                (".debug_info", 0, info),
                (".debug_abbrev", 0, abbrev)));

            var result = NativeSymbolReader.Read(exePath, File.ReadAllBytes(exePath), []);

            Assert.Equal(NativeSymbolStatus.IdMismatch, result.Status);
            Assert.Equal(NativeSymbolSource.EhFrameFallback, result.Source);
            Assert.Contains("app.dbg", result.Diagnostic);
            var boundary = Assert.Single(result.Symbols);
            Assert.Equal(NativeSymbolKind.Boundary, boundary.Kind);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies the no-sidecar chain: boundaries from <c>.eh_frame</c> as
    /// <see cref="NativeSymbolStatus.FallbackOnly"/>, and
    /// <see cref="NativeSymbolStatus.NoSymbolFile"/> when there is no unwind data either.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Read_NoSidecar_FallsBackToEhFrameThenNoSymbolFile()
    {
        var dir = Directory.CreateTempSubdirectory("dotsider-elf-");
        try
        {
            var withUnwind = Write(dir.FullName, "app", SyntheticImageBuilders.BuildElf(
                (".text", 0x1000, new byte[0x100]),
                (".eh_frame", 0x3000, SyntheticImageBuilders.EhFrame((0x1010, 0x40), (0x1050, 0x20)))));
            var result = NativeSymbolReader.Read(withUnwind, File.ReadAllBytes(withUnwind), []);

            Assert.Equal(NativeSymbolStatus.FallbackOnly, result.Status);
            Assert.Equal(NativeSymbolSource.EhFrameFallback, result.Source);
            Assert.Equal(2, result.Symbols.Count);
            Assert.All(result.Symbols, s => Assert.Equal(NativeSymbolKind.Boundary, s.Kind));

            var bare = Write(dir.FullName, "bare", SyntheticImageBuilders.BuildElf(
                (".text", 0x1000, new byte[0x100])));
            var empty = NativeSymbolReader.Read(bare, File.ReadAllBytes(bare), []);

            Assert.Equal(NativeSymbolStatus.NoSymbolFile, empty.Status);
            Assert.Empty(empty.Symbols);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies a sidecar whose debug sections are <c>SHF_COMPRESSED</c> — the GNU toolchain
    /// default that produces zlib payloads behind an <c>Elf64_Chdr</c> — inflates and loads.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Read_CompressedDebugSections_InflateAndLoad()
    {
        var dir = Directory.CreateTempSubdirectory("dotsider-elf-");
        try
        {
            var exePath = Write(dir.FullName, "app", SyntheticImageBuilders.BuildElf(
                (".text", 0x1000, new byte[0x100]),
                (".note.gnu.build-id", 0, SyntheticImageBuilders.GnuBuildIdNote(IdA))));

            var (info, abbrev) = MinimalDwarf("frost_main", 0x1010, 0x40);
            Write(dir.FullName, "app.dbg", SyntheticImageBuilders.BuildElf(
                (".note.gnu.build-id", 0, SyntheticImageBuilders.GnuBuildIdNote(IdA), 1u, 0u, 0UL),
                (".debug_info", 0, SyntheticImageBuilders.CompressDebugSection(info), 1u, 0u, 0x800UL),
                (".debug_abbrev", 0, SyntheticImageBuilders.CompressDebugSection(abbrev), 1u, 0u, 0x800UL)));

            var result = NativeSymbolReader.Read(exePath, File.ReadAllBytes(exePath), []);

            Assert.Equal(NativeSymbolStatus.Loaded, result.Status);
            Assert.Equal(NativeSymbolSource.Dwarf, result.Source);
            var symbol = Assert.Single(result.Symbols);
            Assert.Equal("frost_main", symbol.Name);
            Assert.Equal(0x40, symbol.Size);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies a matched sidecar with unreadable debug data reports
    /// <see cref="NativeSymbolStatus.CorruptSymbolFile"/>.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Read_MatchedButUnreadableSidecar_ReportsCorrupt()
    {
        var dir = Directory.CreateTempSubdirectory("dotsider-elf-");
        try
        {
            var exePath = Write(dir.FullName, "app", SyntheticImageBuilders.BuildElf(
                (".text", 0x1000, new byte[0x100]),
                (".note.gnu.build-id", 0, SyntheticImageBuilders.GnuBuildIdNote(IdA))));
            Write(dir.FullName, "app.dbg", SyntheticImageBuilders.BuildElf(
                (".note.gnu.build-id", 0, SyntheticImageBuilders.GnuBuildIdNote(IdA)),
                (".debug_info", 0, "\t\t\t"u8.ToArray()))); // no abbrev, unreadable

            var result = NativeSymbolReader.Read(exePath, File.ReadAllBytes(exePath), []);

            Assert.Equal(NativeSymbolStatus.CorruptSymbolFile, result.Status);
            Assert.Contains("no readable symbols", result.Diagnostic);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies the real Linux fixture: the <c>.dbg</c> loads as the DWARF source, demangles to
    /// managed names, attributes a user function to a source file and line, and carries data
    /// categories from the symtab pass.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Read_NativeAotExeWithDbg_UsesDwarfSource()
    {
        Assert.SkipWhen(!HasDbg, "native .dbg not present on this platform");

        using var analyzer = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);
        var info = NativeSymbolReader.Read(
            samples.NativeAotConsoleExe!, analyzer.RawBytes.ToArray(), analyzer.RecoveredTypes);

        Assert.Equal(NativeSymbolSource.Dwarf, info.Source);
        Assert.Equal(NativeSymbolStatus.Loaded, info.Status);
        Assert.True(info.Symbols.Count > 1000,
            $"expected a real symbol population, got {info.Symbols.Count}");
        Assert.Contains(info.Symbols, s => s.ManagedName is not null && s.IsExactMatch);
        Assert.Contains(info.Symbols,
            s => s.Kind == NativeSymbolKind.Function && s.SourceFile is not null && s.Line > 0);
        Assert.Contains(info.Symbols, s => s.Kind is NativeSymbolKind.MethodTable
            or NativeSymbolKind.Statics or NativeSymbolKind.FrozenObject);
    }

    /// <summary>
    /// Verifies the real exe copied away from its <c>.dbg</c> falls back to <c>.eh_frame</c>
    /// boundaries.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Read_NativeAotExeCopiedAway_FallsBackToEhFrame()
    {
        Assert.SkipWhen(!HasDbg, "native .dbg not present on this platform");

        var exeBytes = File.ReadAllBytes(samples.NativeAotConsoleExe!);
        Assert.SkipWhen(ElfImageReader.TryGetSection(exeBytes, ".debug_info", out _),
            "exe is unstripped; it would read its own DWARF");

        var dir = Directory.CreateTempSubdirectory("dotsider-ehframe-");
        try
        {
            var exeCopy = Path.Combine(dir.FullName, Path.GetFileName(samples.NativeAotConsoleExe!));
            File.Copy(samples.NativeAotConsoleExe!, exeCopy);

            var info = NativeSymbolReader.Read(exeCopy, File.ReadAllBytes(exeCopy), []);

            Assert.Equal(NativeSymbolSource.EhFrameFallback, info.Source);
            Assert.NotEmpty(info.Symbols);
            Assert.All(info.Symbols, s => Assert.Equal(NativeSymbolKind.Boundary, s.Kind));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
