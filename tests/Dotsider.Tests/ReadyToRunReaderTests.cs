using Dotsider.Core.Analysis;
using System.Runtime.InteropServices;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the ReadyToRun section walker, exercised through
/// <see cref="AssemblyAnalyzer.ReadyToRunSections"/> against the real Native AOT fixture
/// (PE on Windows, ELF on Linux, Mach-O on macOS — each CI runner builds its own format).
/// </summary>
[Collection("SampleAssemblies")]
public class ReadyToRunReaderTests(SampleAssemblyFixture samples)
{
    /// <summary>
    /// Verifies the section table parses and contains the frozen object region and the
    /// embedded metadata blob.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ReadyToRunSections_NativeAotExe_ContainsKnownSections()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        using var analyzer = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);

        var sections = analyzer.ReadyToRunSections;

        Assert.NotEmpty(sections);
        Assert.Contains(sections, s => s.SectionId == 206); // FrozenObjectRegion
        Assert.Contains(sections, s => s.SectionId == 313); // EmbeddedMetadata
        Assert.Contains(sections, s => s.SectionId == 201); // GCStaticRegion
    }

    /// <summary>
    /// Verifies section ids are sorted ascending and the embedded metadata section is
    /// file-backed with the NativeFormat signature.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ReadyToRunSections_NativeAotExe_MetadataSectionIsFileBacked()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var bytes = File.ReadAllBytes(samples.NativeAotConsoleExe!);
        using var analyzer = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);

        var sections = analyzer.ReadyToRunSections;

        var ids = sections.Select(s => s.SectionId).ToList();
        Assert.Equal(ids.OrderBy(i => i), ids);

        var metadata = Assert.Single(sections, s => s.SectionId == 313);
        Assert.NotNull(metadata.FileOffset);
        Assert.True(metadata.Size > 0);

        // NativeFormat metadata magic 0xDEADDFFD at the section start.
        var magic = BitConverter.ToUInt32(bytes, metadata.FileOffset!.Value);
        Assert.Equal(0xDEADDFFDu, magic);
    }

    /// <summary>
    /// Verifies the frozen object region is present, and that its file backing matches the
    /// platform: file-backed on Windows, a NOBITS region the runtime fills at startup on
    /// Linux (paired with a dehydrated data section that rebuilds it).
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ReadyToRunSections_FrozenRegion_FileBackingMatchesPlatform()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        using var analyzer = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);

        var frozen = Assert.Single(analyzer.ReadyToRunSections, s => s.SectionId == 206);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.NotNull(frozen.FileOffset);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Assert.Null(frozen.FileOffset);
            Assert.Contains(analyzer.ReadyToRunSections, s => s.SectionId == 207); // DehydratedData
        }
    }

    /// <summary>
    /// Verifies a managed assembly has no ReadyToRun sections.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ReadyToRunSections_ManagedDll_Empty()
    {
        using var analyzer = new AssemblyAnalyzer(samples.RichLibraryDll);

        Assert.Empty(analyzer.ReadyToRunSections);
    }
}
