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
