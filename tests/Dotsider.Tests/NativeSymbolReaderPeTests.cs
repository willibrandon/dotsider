using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the PE arm of <see cref="NativeSymbolReader"/> and the lazy
/// <see cref="AssemblyAnalyzer.NativeSymbols"/> property, against the real NativeAOT fixture on
/// the Windows leg (where its symbol file is a PDB).
/// </summary>
[Collection("SampleAssemblies")]
public class NativeSymbolReaderPeTests(SampleAssemblyFixture samples)
{
    private bool HasPdb =>
        samples.NativeAotConsoleSymbols is not null
        && samples.NativeAotConsoleSymbols.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Verifies the facade reads the matching PDB as the NativePdb source and demangles the entry
    /// point.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Read_NativeAotExeWithPdb_UsesNativePdbSource()
    {
        Assert.SkipWhen(!HasPdb, "native PDB not present on this platform");

        using var analyzer = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);
        var info = NativeSymbolReader.Read(
            samples.NativeAotConsoleExe!, analyzer.RawBytes.ToArray(), analyzer.RecoveredTypes);

        Assert.Equal(NativeSymbolSource.NativePdb, info.Source);
        Assert.Equal(NativeSymbolStatus.Loaded, info.Status);
        Assert.NotEmpty(info.Symbols);
        Assert.Contains(info.Symbols, s => s.ManagedName is not null && s.IsExactMatch);
    }

    /// <summary>
    /// Verifies a Native AOT binary copied away from its PDB falls back to real .pdata boundaries.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Read_NativeAotExeWithoutPdb_FallsBackToPdataBoundaries()
    {
        Assert.SkipWhen(!HasPdb, "native PDB not present on this platform");

        var dir = Directory.CreateTempSubdirectory("dotsider-pdata-");
        try
        {
            var exeCopy = Path.Combine(dir.FullName, Path.GetFileName(samples.NativeAotConsoleExe!));
            File.Copy(samples.NativeAotConsoleExe!, exeCopy);
            var bytes = File.ReadAllBytes(exeCopy);

            var info = NativeSymbolReader.Read(exeCopy, bytes, []);

            Assert.Equal(NativeSymbolSource.PdataFallback, info.Source);
            Assert.NotEmpty(info.Symbols);
            Assert.All(info.Symbols, s => Assert.Equal(NativeSymbolKind.Boundary, s.Kind));
            Assert.All(info.Symbols, s => Assert.True(s.Size > 0));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies a stale PDB — right name, wrong GUID — reports
    /// <see cref="NativeSymbolStatus.IdMismatch"/> with boundaries, instead of hiding behind
    /// "no matching PDB".
    /// </summary>
    [Fact(Timeout = 30_000)]
    public async Task Read_StalePdbBesideExe_ReportsIdMismatch()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");
        var bytes = File.ReadAllBytes(samples.NativeAotConsoleExe!);
        Assert.SkipWhen(bytes.Length < 2 || bytes[0] != (byte)'M' || bytes[1] != (byte)'Z',
            "exe is not a PE on this platform");
        var id = PeCodeView.TryRead(bytes);
        Assert.SkipWhen(id is null, "exe carries no RSDS record");

        var dir = Directory.CreateTempSubdirectory("dotsider-stalepdb-");
        try
        {
            var exeCopy = Path.Combine(dir.FullName, Path.GetFileName(samples.NativeAotConsoleExe!));
            File.Copy(samples.NativeAotConsoleExe!, exeCopy);
            File.WriteAllBytes(
                Path.Combine(dir.FullName, Path.GetFileNameWithoutExtension(exeCopy) + ".pdb"),
                SyntheticImageBuilders.BuildMsf(4096, PdbInfoStream(Guid.NewGuid(), id!.Value.Age)));

            var info = NativeSymbolReader.Read(exeCopy, File.ReadAllBytes(exeCopy), []);

            Assert.Equal(NativeSymbolStatus.IdMismatch, info.Status);
            Assert.Equal(NativeSymbolSource.PdataFallback, info.Source);
            Assert.NotEmpty(info.Symbols);
            Assert.Contains(".pdb", info.Diagnostic);
        }
        finally
        {
            dir.Delete(recursive: true);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Verifies an unreadable PDB sidecar reports
    /// <see cref="NativeSymbolStatus.CorruptSymbolFile"/>, and a matching PDB with no readable
    /// symbol streams does too — neither masquerades as a missing file.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Read_CorruptOrEmptyPdb_ReportsCorruptSymbolFile()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");
        var bytes = File.ReadAllBytes(samples.NativeAotConsoleExe!);
        Assert.SkipWhen(bytes.Length < 2 || bytes[0] != (byte)'M' || bytes[1] != (byte)'Z',
            "exe is not a PE on this platform");
        var id = PeCodeView.TryRead(bytes);
        Assert.SkipWhen(id is null, "exe carries no RSDS record");

        var dir = Directory.CreateTempSubdirectory("dotsider-badpdb-");
        try
        {
            var exeCopy = Path.Combine(dir.FullName, Path.GetFileName(samples.NativeAotConsoleExe!));
            File.Copy(samples.NativeAotConsoleExe!, exeCopy);
            var pdbPath = Path.Combine(dir.FullName, Path.GetFileNameWithoutExtension(exeCopy) + ".pdb");

            // Not an MSF container at all.
            File.WriteAllBytes(pdbPath, [0xDE, 0xAD, 0xBE, 0xEF]);
            var unreadable = NativeSymbolReader.Read(exeCopy, File.ReadAllBytes(exeCopy), []);
            Assert.Equal(NativeSymbolStatus.CorruptSymbolFile, unreadable.Status);
            Assert.NotEmpty(unreadable.Symbols);

            // Identity matches, but there is no DBI stream to read symbols from.
            File.WriteAllBytes(pdbPath,
                SyntheticImageBuilders.BuildMsf(4096, PdbInfoStream(id!.Value.Guid, id.Value.Age)));
            var empty = NativeSymbolReader.Read(exeCopy, File.ReadAllBytes(exeCopy), []);
            Assert.Equal(NativeSymbolStatus.CorruptSymbolFile, empty.Status);
            Assert.Contains("no readable symbols", empty.Diagnostic);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>Builds a PDB info stream: version, signature, age, GUID.</summary>
    private static byte[] PdbInfoStream(Guid guid, int age)
    {
        var stream = new byte[28];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(stream, 20000404);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(stream.AsSpan(4), 1);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(stream.AsSpan(8), age);
        guid.TryWriteBytes(stream.AsSpan(12));
        return stream;
    }

    /// <summary>
    /// Verifies the analyzer surfaces native symbols for a Native AOT binary and null for a
    /// managed assembly.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NativeSymbols_ManagedIsNull_NativeAotIsPresent()
    {
        using var managed = new AssemblyAnalyzer(samples.RichLibraryDll);
        Assert.Null(managed.NativeSymbols);

        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");
        using var aot = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);
        Assert.NotNull(aot.NativeSymbols);
    }
}
