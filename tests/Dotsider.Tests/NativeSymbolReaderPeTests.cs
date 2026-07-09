using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the PE arm of <see cref="NativeSymbolReader"/> and the lazy
/// <see cref="AssemblyAnalyzer.NativeSymbols"/> property, against the real NativeAOT fixture on
/// the Windows leg (where its symbol file is a PDB).
/// </summary>
[TestClass]
public class NativeSymbolReaderPeTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private static bool HasPdb =>
        Samples.NativeAotConsoleSymbols is not null
        && Samples.NativeAotConsoleSymbols.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Verifies the facade reads the matching PDB as the NativePdb source and demangles the entry
    /// point.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_NativeAotExeWithPdb_UsesNativePdbSource()
    {
        TestSkip.When(!HasPdb, "native PDB not present on this platform");

        using var analyzer = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);
        var info = NativeSymbolReader.Read(
            Samples.NativeAotConsoleExe!, analyzer.RawBytes.ToArray(), analyzer.RecoveredTypes);

        Assert.AreEqual(NativeSymbolSource.NativePdb, info.Source);
        Assert.AreEqual(NativeSymbolStatus.Loaded, info.Status);
        Assert.IsNotEmpty(info.Symbols);
        Assert.Contains(s => s.ManagedName is not null && s.IsExactMatch, info.Symbols);
    }

    /// <summary>
    /// Verifies a Native AOT binary copied away from its PDB falls back to real .pdata boundaries.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_NativeAotExeWithoutPdb_FallsBackToPdataBoundaries()
    {
        TestSkip.When(!HasPdb, "native PDB not present on this platform");

        var dir = Directory.CreateTempSubdirectory("dotsider-pdata-");
        try
        {
            var exeCopy = Path.Combine(dir.FullName, Path.GetFileName(Samples.NativeAotConsoleExe!));
            File.Copy(Samples.NativeAotConsoleExe!, exeCopy);
            var bytes = File.ReadAllBytes(exeCopy);

            var info = NativeSymbolReader.Read(exeCopy, bytes, []);

            Assert.AreEqual(NativeSymbolSource.PdataFallback, info.Source);
            Assert.IsNotEmpty(info.Symbols);
            TestAssert.All(info.Symbols, s => Assert.AreEqual(NativeSymbolKind.Boundary, s.Kind));
            TestAssert.All(info.Symbols, s => Assert.IsGreaterThan(0, s.Size));
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
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Read_StalePdbBesideExe_ReportsIdMismatch()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");
        var bytes = File.ReadAllBytes(Samples.NativeAotConsoleExe!);
        TestSkip.When(bytes.Length < 2 || bytes[0] != (byte)'M' || bytes[1] != (byte)'Z',
            "exe is not a PE on this platform");
        var id = PeCodeView.TryRead(bytes);
        TestSkip.When(id is null, "exe carries no RSDS record");

        var dir = Directory.CreateTempSubdirectory("dotsider-stalepdb-");
        try
        {
            var exeCopy = Path.Combine(dir.FullName, Path.GetFileName(Samples.NativeAotConsoleExe!));
            File.Copy(Samples.NativeAotConsoleExe!, exeCopy);
            File.WriteAllBytes(
                Path.Combine(dir.FullName, Path.GetFileNameWithoutExtension(exeCopy) + ".pdb"),
                SyntheticImageBuilders.BuildMsf(4096, PdbInfoStream(Guid.NewGuid(), id!.Value.Age)));

            var info = NativeSymbolReader.Read(exeCopy, File.ReadAllBytes(exeCopy), []);

            Assert.AreEqual(NativeSymbolStatus.IdMismatch, info.Status);
            Assert.AreEqual(NativeSymbolSource.PdataFallback, info.Source);
            Assert.IsNotEmpty(info.Symbols);
            Assert.Contains(".pdb", info.Diagnostic!);
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
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_CorruptOrEmptyPdb_ReportsCorruptSymbolFile()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");
        var bytes = File.ReadAllBytes(Samples.NativeAotConsoleExe!);
        TestSkip.When(bytes.Length < 2 || bytes[0] != (byte)'M' || bytes[1] != (byte)'Z',
            "exe is not a PE on this platform");
        var id = PeCodeView.TryRead(bytes);
        TestSkip.When(id is null, "exe carries no RSDS record");

        var dir = Directory.CreateTempSubdirectory("dotsider-badpdb-");
        try
        {
            var exeCopy = Path.Combine(dir.FullName, Path.GetFileName(Samples.NativeAotConsoleExe!));
            File.Copy(Samples.NativeAotConsoleExe!, exeCopy);
            var pdbPath = Path.Combine(dir.FullName, Path.GetFileNameWithoutExtension(exeCopy) + ".pdb");

            // Not an MSF container at all.
            File.WriteAllBytes(pdbPath, [0xDE, 0xAD, 0xBE, 0xEF]);
            var unreadable = NativeSymbolReader.Read(exeCopy, File.ReadAllBytes(exeCopy), []);
            Assert.AreEqual(NativeSymbolStatus.CorruptSymbolFile, unreadable.Status);
            Assert.IsNotEmpty(unreadable.Symbols);

            // Identity matches, but there is no DBI stream to read symbols from.
            File.WriteAllBytes(pdbPath,
                SyntheticImageBuilders.BuildMsf(4096, PdbInfoStream(id!.Value.Guid, id.Value.Age)));
            var empty = NativeSymbolReader.Read(exeCopy, File.ReadAllBytes(exeCopy), []);
            Assert.AreEqual(NativeSymbolStatus.CorruptSymbolFile, empty.Status);
            Assert.Contains("no readable symbols", empty.Diagnostic!);
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
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeSymbols_ManagedIsNull_NativeAotIsPresent()
    {
        using var managed = new AssemblyAnalyzer(Samples.RichLibraryDll);
        Assert.IsNull(managed.NativeSymbols);

        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");
        using var aot = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);
        Assert.IsNotNull(aot.NativeSymbols);
    }
}
