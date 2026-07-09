using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Core.Analysis.NativePdb;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="NativePdbReader"/> against the real PDB published beside the NativeAOT
/// sample on Windows. These run on the Windows CI leg, where the symbol file is a <c>.pdb</c>;
/// the block-math and container tests in <see cref="MsfFileTests"/> cover the other platforms.
/// </summary>
[TestClass]
public class NativePdbReaderTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private static bool HasPdb =>
        Samples.NativeAotConsoleSymbols is not null
        && Samples.NativeAotConsoleSymbols.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Verifies the PDB's GUID reads through the cheap probe and matches the GUID embedded in the
    /// executable's RSDS debug directory (the bytes appear verbatim in the image).
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TryReadPdbId_MatchesExeRsdsGuid()
    {
        TestSkip.When(!HasPdb, "native PDB not present on this platform");

        Assert.IsTrue(NativePdbReader.TryReadPdbId(Samples.NativeAotConsoleSymbols!, out var guid, out var age));
        Assert.AreNotEqual(Guid.Empty, guid);
        Assert.IsGreaterThan(0, age);

        var exe = File.ReadAllBytes(Samples.NativeAotConsoleExe!);
        Assert.IsGreaterThanOrEqualTo(0, IndexOf(exe, guid.ToByteArray()), "PDB GUID not found in the exe RSDS entry");
    }

    /// <summary>
    /// Verifies the reader recovers function symbols with resolved addresses and names, including
    /// the app's entry point, and that C13 line data attributes at least one function to a source
    /// file and line.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_FixturePdb_RecoversFunctionsWithLineData()
    {
        TestSkip.When(!HasPdb, "native PDB not present on this platform");

        var pdb = File.ReadAllBytes(Samples.NativeAotConsoleSymbols!);
        var exe = File.ReadAllBytes(Samples.NativeAotConsoleExe!);

        var symbols = NativePdbReader.Read(pdb, exe);

        Assert.IsNotEmpty(symbols);
        // Addresses resolved: every symbol has a non-zero VA and an RVA.
        TestAssert.All(symbols, s =>
        {
            Assert.IsGreaterThan(0UL, s.VirtualAddress);
            Assert.IsNotNull(s.Rva);
        });
        // The entry point is present in mangled form.
        Assert.Contains(s => s.Name.Contains("Program___Main__", StringComparison.Ordinal), symbols);
        // C13 line data attributed at least one function to a source location.
        Assert.Contains(s => s.SourceFile is not null && s.Line is > 0, symbols);
    }

    /// <summary>
    /// Verifies a non-PDB file returns no symbols rather than throwing.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Read_NotAPdb_ReturnsEmpty()
    {
        Assert.IsEmpty(NativePdbReader.Read([0xDE, 0xAD, 0xBE, 0xEF], new byte[64]));
    }

    /// <summary>
    /// Verifies the full PDB pipeline — module records, publics, and the data pass — merged and
    /// demangled: addresses are unique (no double count), managed names join, and the compiler's
    /// data symbols (MethodTables) surface as their own kind.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Build_FixturePdb_MergesDemanglesAndClassifies()
    {
        TestSkip.When(!HasPdb, "native PDB not present on this platform");

        using var analyzer = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);
        var raw = NativePdbReader.Read(
            File.ReadAllBytes(Samples.NativeAotConsoleSymbols!), analyzer.RawBytes.ToArray());
        var demangler = new IlcNameDemangler(analyzer.RecoveredTypes);

        var info = NativeSymbolReader.Build(
            raw, demangler,
            NativeSymbolSource.NativePdb,
            NativeSymbolStatus.Loaded,
            Samples.NativeAotConsoleSymbols, null,
            NativeArchitecture.X64);

        Assert.IsNotEmpty(info.Symbols);
        // No two symbols share an address after the merge.
        Assert.HasCount(info.Symbols.Count, info.Symbols.Select(s => s.VirtualAddress).Distinct());
        // Managed names joined for some functions.
        Assert.Contains(s => s.IsExactMatch && s.ManagedName is not null, info.Symbols);
        // The compiler's MethodTable data symbols are recovered and classified.
        Assert.Contains(s => s.Kind == NativeSymbolKind.MethodTable, info.Symbols);
    }



    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (var i = 0; i + needle.Length <= haystack.Length; i++)
        {
            var j = 0;
            for (; j < needle.Length; j++)
                if (haystack[i + j] != needle[j]) break;
            if (j == needle.Length) return i;
        }

        return -1;
    }
}
