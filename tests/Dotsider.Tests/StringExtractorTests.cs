using System.Runtime.InteropServices;
using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

[Collection("SampleAssemblies")]
public class StringExtractorTests(SampleAssemblyFixture samples)
{
    [Fact(Timeout = 30_000)]
    public void HelloWorld_UserStrings_ContainOutputText()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        var extractor = new StringExtractor(a);
        var strings = extractor.ExtractUserStrings();
        Assert.NotEmpty(strings);
        // HelloWorld prints messages that should appear as user strings
        Assert.Contains(strings, s => s.Source == StringSource.UserStrings);
    }

    [Fact(Timeout = 30_000)]
    public void RichLibrary_MetadataStrings_ContainTypeNames()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var extractor = new StringExtractor(a);
        var strings = extractor.ExtractMetadataStrings();
        Assert.NotEmpty(strings);
        Assert.Contains(strings, s => s.Value.Contains("UserService"));
    }

    [Fact(Timeout = 30_000)]
    public void ComplexApp_UserStrings_ContainPipelineText()
    {
        using var a = new AssemblyAnalyzer(samples.ComplexAppDll);
        var extractor = new StringExtractor(a);
        var strings = extractor.ExtractUserStrings();
        Assert.NotEmpty(strings);
    }

    [Fact(Timeout = 30_000)]
    public void MinimalApi_UserStrings_ContainEndpointPaths()
    {
        using var a = new AssemblyAnalyzer(samples.MinimalApiDll);
        var extractor = new StringExtractor(a);
        var strings = extractor.ExtractUserStrings();
        Assert.NotEmpty(strings);
        Assert.Contains(strings, s => s.Value.Contains("/hello") || s.Value.Contains("/echo"));
    }

    [Fact(Timeout = 30_000)]
    public void EmptyLib_MinimalStrings()
    {
        using var a = new AssemblyAnalyzer(samples.EmptyLibDll);
        var extractor = new StringExtractor(a);
        var userStrings = extractor.ExtractUserStrings();
        // EmptyLib should have few or no user strings
        Assert.True(userStrings.Count <= 5);
    }

    [Fact(Timeout = 30_000)]
    public void RawStrings_MinLength4_MoreThanMinLength16()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var extractor = new StringExtractor(a);
        var raw4 = extractor.ExtractRawStrings(4);
        var raw16 = extractor.ExtractRawStrings(16);
        Assert.True(raw4.Count >= raw16.Count);
    }

    [Fact(Timeout = 30_000)]
    public void RawStrings_Default_MinLength4()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        var extractor = new StringExtractor(a);
        var raw = extractor.ExtractRawStrings();
        Assert.NotEmpty(raw);
        Assert.All(raw, s => Assert.True(s.Value.Length >= 4));
    }

    [Fact(Timeout = 30_000)]
    public void MetadataStrings_ContainNamespaces()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var extractor = new StringExtractor(a);
        var strings = extractor.ExtractMetadataStrings();
        Assert.Contains(strings, s => s.Value.Contains("RichLibrary"));
    }

    [Fact(Timeout = 30_000)]
    public void UserStrings_AllHaveCorrectSource()
    {
        using var a = new AssemblyAnalyzer(samples.ComplexAppDll);
        var extractor = new StringExtractor(a);
        var strings = extractor.ExtractUserStrings();
        Assert.All(strings, s => Assert.Equal(StringSource.UserStrings, s.Source));
    }

    [Fact(Timeout = 30_000)]
    public void MetadataStrings_AllHaveCorrectSource()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var extractor = new StringExtractor(a);
        var strings = extractor.ExtractMetadataStrings();
        Assert.All(strings, s => Assert.Equal(StringSource.MetadataStrings, s.Source));
    }

    [Fact(Timeout = 30_000)]
    public void RawStrings_AllHaveCorrectSource()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        var extractor = new StringExtractor(a);
        var strings = extractor.ExtractRawStrings();
        Assert.All(strings, s => Assert.Equal(StringSource.RawBinary, s.Source));
    }

    [Fact(Timeout = 30_000)]
    public void NativeLib_HasMetadataStrings()
    {
        using var a = new AssemblyAnalyzer(samples.NativeLibDll);
        var extractor = new StringExtractor(a);
        var strings = extractor.ExtractMetadataStrings();
        Assert.NotEmpty(strings);
        Assert.Contains(strings, s => s.Value.Contains("NativeInterop") || s.Value.Contains("UnsafeOperations"));
    }

    [Fact(Timeout = 30_000)]
    public void RawStrings_MinLength8_AllMeetMinimum()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var extractor = new StringExtractor(a);
        var raw = extractor.ExtractRawStrings(8);
        Assert.NotEmpty(raw);
        Assert.All(raw, s => Assert.True(s.Value.Length >= 8));
    }

    [Fact(Timeout = 30_000)]
    public void StringEntries_HavePositiveOffsets()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var extractor = new StringExtractor(a);
        var userStrings = extractor.ExtractUserStrings();
        Assert.All(userStrings, s => Assert.True(s.Offset >= 0));
        var metaStrings = extractor.ExtractMetadataStrings();
        Assert.All(metaStrings, s => Assert.True(s.Offset >= 0));
    }

    [Fact(Timeout = 30_000)]
    public void SkippedCounts_ZeroForValidAssembly()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var extractor = new StringExtractor(a);
        extractor.ExtractUserStrings();
        Assert.Equal(0, extractor.SkippedUserStringCount);
        extractor.ExtractMetadataStrings();
        Assert.Equal(0, extractor.SkippedMetadataStringCount);
    }

    /// <summary>
    /// Reproduces #82: drilling into System.Runtime and switching to the Strings tab
    /// crashes with BadImageFormatException because GetNextHandle reads past the
    /// end of the #US heap.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RuntimeAssembly_ExtractUserStrings_DoesNotThrow()
    {
        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        var systemRuntime = Path.Combine(runtimeDir, "System.Runtime.dll");
        Assert.True(File.Exists(systemRuntime), $"System.Runtime.dll not found at {runtimeDir}");

        using var a = new AssemblyAnalyzer(systemRuntime);
        var extractor = new StringExtractor(a);

        var strings = extractor.ExtractUserStrings();

        // System.Runtime has no user string literals (its #US heap is zero bytes).
        // Must not crash, and must not report false skips.
        Assert.NotNull(strings);
        Assert.Empty(strings);
        Assert.Equal(0, extractor.SkippedUserStringCount);
    }

    /// <summary>
    /// Same as above but for the #Strings metadata heap, which has the same
    /// unguarded GetNextHandle pattern.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RuntimeAssembly_ExtractMetadataStrings_DoesNotThrow()
    {
        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        var systemRuntime = Path.Combine(runtimeDir, "System.Runtime.dll");
        Assert.True(File.Exists(systemRuntime), $"System.Runtime.dll not found at {runtimeDir}");

        using var a = new AssemblyAnalyzer(systemRuntime);
        var extractor = new StringExtractor(a);

        var strings = extractor.ExtractMetadataStrings();

        Assert.NotNull(strings);
    }
}
