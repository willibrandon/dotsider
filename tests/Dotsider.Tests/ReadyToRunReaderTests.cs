using Dotsider.Core.Analysis;
using System.Runtime.InteropServices;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the ReadyToRun section walker, exercised through
/// <see cref="AssemblyAnalyzer.ReadyToRunSections"/> against the real Native AOT fixture
/// (PE on Windows, ELF on Linux, Mach-O on macOS — each CI runner builds its own format).
/// </summary>
[TestClass]
public class ReadyToRunReaderTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// Verifies the section table parses and contains the frozen object region and the
    /// embedded metadata blob.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadyToRunSections_NativeAotExe_ContainsKnownSections()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        using var analyzer = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);

        var sections = analyzer.ReadyToRunSections;

        Assert.IsNotEmpty(sections);
        Assert.Contains(s => s.SectionId == 206, sections); // FrozenObjectRegion
        Assert.Contains(s => s.SectionId == 313, sections); // EmbeddedMetadata
        Assert.Contains(s => s.SectionId == 201, sections); // GCStaticRegion
    }

    /// <summary>
    /// Verifies section ids are sorted ascending and the embedded metadata section is
    /// file-backed with the NativeFormat signature.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadyToRunSections_NativeAotExe_MetadataSectionIsFileBacked()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var bytes = File.ReadAllBytes(Samples.NativeAotConsoleExe!);
        using var analyzer = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);

        var sections = analyzer.ReadyToRunSections;

        var ids = sections.Select(s => s.SectionId).ToList();
        Assert.AreSequenceEqual([.. ids.OrderBy(i => i)], ids);

        var metadata = Assert.ContainsSingle(s => s.SectionId == 313, sections);
        Assert.IsNotNull(metadata.FileOffset);
        Assert.IsGreaterThan(0, metadata.Size);

        // NativeFormat metadata magic 0xDEADDFFD at the section start.
        var magic = BitConverter.ToUInt32(bytes, metadata.FileOffset!.Value);
        Assert.AreEqual(0xDEADDFFDu, magic);
    }

    /// <summary>
    /// Verifies the frozen object region is present, and that its file backing matches the
    /// platform: file-backed on Windows, a NOBITS region the runtime fills at startup on
    /// Linux (paired with a dehydrated data section that rebuilds it).
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadyToRunSections_FrozenRegion_FileBackingMatchesPlatform()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        using var analyzer = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);

        var frozen = Assert.ContainsSingle(s => s.SectionId == 206, analyzer.ReadyToRunSections);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.IsNotNull(frozen.FileOffset);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            Assert.IsNull(frozen.FileOffset);
            Assert.Contains(s => s.SectionId == 207, analyzer.ReadyToRunSections); // DehydratedData
        }
    }

    /// <summary>
    /// Verifies a managed assembly has no ReadyToRun sections.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadyToRunSections_ManagedDll_Empty()
    {
        using var analyzer = new AssemblyAnalyzer(Samples.RichLibraryDll);

        Assert.IsEmpty(analyzer.ReadyToRunSections);
    }
}
