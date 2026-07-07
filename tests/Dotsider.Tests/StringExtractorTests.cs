using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using System.Runtime.InteropServices;

namespace Dotsider.Tests;

/// <summary>
/// Tests for String Extractor.
/// </summary>
[Collection("SampleAssemblies")]
public class StringExtractorTests(SampleAssemblyFixture samples)
{
    /// <summary>
    /// Verifies hello world user strings contain output text.
    /// </summary>
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

    /// <summary>
    /// Verifies rich library metadata strings contain type names.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibrary_MetadataStrings_ContainTypeNames()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var extractor = new StringExtractor(a);
        var strings = extractor.ExtractMetadataStrings();
        Assert.NotEmpty(strings);
        Assert.Contains(strings, s => s.Value.Contains("UserService"));
    }

    /// <summary>
    /// Verifies complex app user strings contain pipeline text.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ComplexApp_UserStrings_ContainPipelineText()
    {
        using var a = new AssemblyAnalyzer(samples.ComplexAppDll);
        var extractor = new StringExtractor(a);
        var strings = extractor.ExtractUserStrings();
        Assert.NotEmpty(strings);
    }

    /// <summary>
    /// Verifies minimal api user strings contain endpoint paths.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void MinimalApi_UserStrings_ContainEndpointPaths()
    {
        using var a = new AssemblyAnalyzer(samples.MinimalApiDll);
        var extractor = new StringExtractor(a);
        var strings = extractor.ExtractUserStrings();
        Assert.NotEmpty(strings);
        Assert.Contains(strings, s => s.Value.Contains("/hello") || s.Value.Contains("/echo"));
    }

    /// <summary>
    /// Verifies empty lib minimal strings.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void EmptyLib_MinimalStrings()
    {
        using var a = new AssemblyAnalyzer(samples.EmptyLibDll);
        var extractor = new StringExtractor(a);
        var userStrings = extractor.ExtractUserStrings();
        // EmptyLib should have few or no user strings
        Assert.True(userStrings.Count <= 5);
    }

    /// <summary>
    /// Verifies raw strings min length4 more than min length16.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RawStrings_MinLength4_MoreThanMinLength16()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var extractor = new StringExtractor(a);
        var raw4 = extractor.ExtractRawStrings(4);
        var raw16 = extractor.ExtractRawStrings(16);
        Assert.True(raw4.Count >= raw16.Count);
    }

    /// <summary>
    /// Verifies raw strings default min length4.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RawStrings_Default_MinLength4()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        var extractor = new StringExtractor(a);
        var raw = extractor.ExtractRawStrings();
        Assert.NotEmpty(raw);
        Assert.All(raw, s => Assert.True(s.Value.Length >= 4));
    }

    /// <summary>
    /// Verifies metadata strings contain namespaces.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void MetadataStrings_ContainNamespaces()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var extractor = new StringExtractor(a);
        var strings = extractor.ExtractMetadataStrings();
        Assert.Contains(strings, s => s.Value.Contains("RichLibrary"));
    }

    /// <summary>
    /// Verifies user strings all have correct source.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void UserStrings_AllHaveCorrectSource()
    {
        using var a = new AssemblyAnalyzer(samples.ComplexAppDll);
        var extractor = new StringExtractor(a);
        var strings = extractor.ExtractUserStrings();
        Assert.All(strings, s => Assert.Equal(StringSource.UserStrings, s.Source));
    }

    /// <summary>
    /// Verifies metadata strings all have correct source.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void MetadataStrings_AllHaveCorrectSource()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var extractor = new StringExtractor(a);
        var strings = extractor.ExtractMetadataStrings();
        Assert.All(strings, s => Assert.Equal(StringSource.MetadataStrings, s.Source));
    }

    /// <summary>
    /// Verifies raw strings all have correct source.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RawStrings_AllHaveCorrectSource()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        var extractor = new StringExtractor(a);
        var strings = extractor.ExtractRawStrings();
        Assert.All(strings, s => Assert.Equal(StringSource.RawBinary, s.Source));
    }

    /// <summary>
    /// Verifies native lib has metadata strings.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NativeLib_HasMetadataStrings()
    {
        using var a = new AssemblyAnalyzer(samples.NativeLibDll);
        var extractor = new StringExtractor(a);
        var strings = extractor.ExtractMetadataStrings();
        Assert.NotEmpty(strings);
        Assert.Contains(strings, s => s.Value.Contains("NativeInterop") || s.Value.Contains("UnsafeOperations"));
    }

    /// <summary>
    /// Verifies raw strings min length8 all meet minimum.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RawStrings_MinLength8_AllMeetMinimum()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var extractor = new StringExtractor(a);
        var raw = extractor.ExtractRawStrings(8);
        Assert.NotEmpty(raw);
        Assert.All(raw, s => Assert.True(s.Value.Length >= 8));
    }

    /// <summary>
    /// Verifies string entries have positive offsets.
    /// </summary>
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

    /// <summary>
    /// Verifies skipped counts zero for valid assembly.
    /// </summary>
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

    /// <summary>
    /// Verifies the UTF-16 raw scan finds frozen managed string literals in a
    /// Native AOT binary (the DOTNET_ environment variable names the runtime reads).
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NativeAot_ExtractRawUtf16Strings_FindsFrozenLiterals()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null,
            "NativeAOT sample was not built");

        using var a = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);
        var extractor = new StringExtractor(a);

        var strings = extractor.ExtractRawUtf16Strings(minLength: 8);

        Assert.NotEmpty(strings);
        Assert.Contains(strings, s => s.Value.Contains("DOTNET_", StringComparison.Ordinal));
        Assert.All(strings, s => Assert.Equal(StringSource.RawBinaryUtf16, s.Source));
    }

    /// <summary>
    /// Verifies the UTF-16 raw scan honors the minimum length.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ExtractRawUtf16Strings_RespectsMinLength()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var extractor = new StringExtractor(a);

        var strings = extractor.ExtractRawUtf16Strings(minLength: 16);

        Assert.All(strings, s => Assert.True(s.Value.Length >= 16));
    }

    /// <summary>
    /// Verifies UTF-16 entries carry offsets inside the file.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ExtractRawUtf16Strings_HaveValidOffsets()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var extractor = new StringExtractor(a);

        var strings = extractor.ExtractRawUtf16Strings();

        Assert.All(strings, s => Assert.InRange(s.Offset, 0, (int)a.FileSize - 1));
    }

    /// <summary>
    /// Verifies a UTF-16 run at an odd byte offset is found — UTF-16 data sits at
    /// arbitrary alignments in real binaries.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ExtractRawUtf16Strings_OddOffsetRun_Found()
    {
        var bytes = new byte[64];
        bytes[0] = 0x7F;
        bytes[1] = (byte)'E';
        bytes[2] = (byte)'L';
        bytes[3] = (byte)'F';

        // "Hello" as UTF-16LE starting at odd offset 7
        var text = "Hello"u8;
        for (var i = 0; i < text.Length; i++)
            bytes[7 + i * 2] = text[i];

        using var a = new AssemblyAnalyzer(bytes, "fake.so");
        var extractor = new StringExtractor(a);

        var strings = extractor.ExtractRawUtf16Strings(minLength: 5);

        var entry = Assert.Single(strings, s => s.Value == "Hello");
        Assert.Equal(7, entry.Offset);
    }
}
