using Dotsider.Core.Analysis.NativePdb;

namespace Dotsider.Tests;

/// <summary>
/// Tests for <see cref="NativePdbReader"/> against the real PDB published beside the NativeAOT
/// sample on Windows. These run on the Windows CI leg, where the symbol file is a <c>.pdb</c>;
/// the block-math and container tests in <see cref="MsfFileTests"/> cover the other platforms.
/// </summary>
[Collection("SampleAssemblies")]
public class NativePdbReaderTests(SampleAssemblyFixture samples)
{
    private bool HasPdb =>
        samples.NativeAotConsoleSymbols is not null
        && samples.NativeAotConsoleSymbols.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Verifies the PDB's GUID reads through the cheap probe and matches the GUID embedded in the
    /// executable's RSDS debug directory (the bytes appear verbatim in the image).
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void TryReadPdbId_MatchesExeRsdsGuid()
    {
        Assert.SkipWhen(!HasPdb, "native PDB not present on this platform");

        Assert.True(NativePdbReader.TryReadPdbId(samples.NativeAotConsoleSymbols!, out var guid, out var age));
        Assert.NotEqual(Guid.Empty, guid);
        Assert.True(age > 0);

        var exe = File.ReadAllBytes(samples.NativeAotConsoleExe!);
        Assert.True(IndexOf(exe, guid.ToByteArray()) >= 0, "PDB GUID not found in the exe RSDS entry");
    }

    /// <summary>
    /// Verifies the reader recovers function symbols with resolved addresses and names, including
    /// the app's entry point, and that C13 line data attributes at least one function to a source
    /// file and line.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Read_FixturePdb_RecoversFunctionsWithLineData()
    {
        Assert.SkipWhen(!HasPdb, "native PDB not present on this platform");

        var pdb = File.ReadAllBytes(samples.NativeAotConsoleSymbols!);
        var exe = File.ReadAllBytes(samples.NativeAotConsoleExe!);

        var symbols = NativePdbReader.Read(pdb, exe);

        Assert.NotEmpty(symbols);
        // Addresses resolved: every symbol has a non-zero VA and an RVA.
        Assert.All(symbols, s =>
        {
            Assert.True(s.VirtualAddress > 0);
            Assert.NotNull(s.Rva);
        });
        // The entry point is present in mangled form.
        Assert.Contains(symbols, s => s.Name.Contains("Program___Main__", StringComparison.Ordinal));
        // C13 line data attributed at least one function to a source location.
        Assert.Contains(symbols, s => s.SourceFile is not null && s.Line is > 0);
    }

    /// <summary>
    /// Verifies a non-PDB file returns no symbols rather than throwing.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Read_NotAPdb_ReturnsEmpty()
    {
        Assert.Empty(NativePdbReader.Read([0xDE, 0xAD, 0xBE, 0xEF], new byte[64]));
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
