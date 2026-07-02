using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for frozen string recovery from the Native AOT frozen object region, exercised
/// through <see cref="AssemblyAnalyzer.FrozenStrings"/>.
/// </summary>
[Collection("SampleAssemblies")]
public class FrozenObjectReaderTests(SampleAssemblyFixture samples)
{
    /// <summary>
    /// Verifies frozen strings are recovered from the sample and include the literal it
    /// prints. On Windows and macOS the region is file-backed; on Linux it is rebuilt from
    /// the dehydrated data — either way the literals are recovered.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void FrozenStrings_NativeAotExe_RecoversLiterals()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        using var analyzer = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);

        var frozen = analyzer.FrozenStrings;

        if (!frozen.Any(s => s.Value.Contains("Hello from NativeAOT!", StringComparison.Ordinal)))
        {
            var sections = string.Join(", ",
                analyzer.ReadyToRunSections.Select(s => $"{s.SectionId}:off={s.FileOffset?.ToString() ?? "null"}:sz={s.Size}"));
            Assert.Fail($"frozen={frozen.Count}; sections=[{sections}]");
        }

        Assert.All(frozen, s => Assert.Equal(StringSource.FrozenObject, s.Source));
    }

    /// <summary>
    /// Verifies recovered frozen strings carry offsets inside the file.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void FrozenStrings_NativeAotExe_HaveValidOffsets()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var size = new FileInfo(samples.NativeAotConsoleExe!).Length;
        using var analyzer = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);

        var frozen = analyzer.FrozenStrings;

        Assert.NotEmpty(frozen);
        Assert.All(frozen, s => Assert.InRange(s.Offset, 0, (int)size - 1));
    }

    /// <summary>
    /// Verifies a managed assembly has no frozen strings.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void FrozenStrings_ManagedDll_Empty()
    {
        using var analyzer = new AssemblyAnalyzer(samples.RichLibraryDll);

        Assert.Empty(analyzer.FrozenStrings);
    }
}
