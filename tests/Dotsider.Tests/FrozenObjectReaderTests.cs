using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for frozen string recovery from the Native AOT frozen object region, exercised
/// through <see cref="AssemblyAnalyzer.FrozenStrings"/>.
/// </summary>
[TestClass]
public class FrozenObjectReaderTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// Verifies frozen strings are recovered from the sample and include the literal it
    /// prints. On Windows and macOS the region is file-backed; on Linux it is rebuilt from
    /// the dehydrated data — either way the literals are recovered.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void FrozenStrings_NativeAotExe_RecoversLiterals()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        using var analyzer = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);

        var frozen = analyzer.FrozenStrings;

        if (!frozen.Any(s => s.Value.Contains("Hello from NativeAOT!", StringComparison.Ordinal)))
        {
            var sections = string.Join(", ",
                analyzer.ReadyToRunSections.Select(s => $"{s.SectionId}:off={s.FileOffset?.ToString() ?? "null"}:sz={s.Size}"));
            Assert.Fail($"frozen={frozen.Count}; sections=[{sections}]");
        }

        TestAssert.All(frozen, s => Assert.AreEqual(StringSource.FrozenObject, s.Source));
    }

    /// <summary>
    /// Verifies recovered frozen strings carry offsets inside the file.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void FrozenStrings_NativeAotExe_HaveValidOffsets()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var size = new FileInfo(Samples.NativeAotConsoleExe!).Length;
        using var analyzer = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);

        var frozen = analyzer.FrozenStrings;

        Assert.IsNotEmpty(frozen);
        TestAssert.All(frozen, s => Assert.IsInRange(0, (int)size - 1, s.Offset));
    }

    /// <summary>
    /// Verifies a managed assembly has no frozen strings.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void FrozenStrings_ManagedDll_Empty()
    {
        using var analyzer = new AssemblyAnalyzer(Samples.RichLibraryDll);

        Assert.IsEmpty(analyzer.FrozenStrings);
    }
}
