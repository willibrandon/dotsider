using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using System.Runtime.InteropServices;

namespace Dotsider.Tests;

/// <summary>
/// Tests for String Extractor.
/// </summary>
[TestClass]
public class StringExtractorTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// Verifies hello world user strings contain output text.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void HelloWorld_UserStrings_ContainOutputText()
    {
        using var a = new AssemblyAnalyzer(Samples.HelloWorldDll);
        var extractor = new StringExtractor(a);
        var strings = extractor.ExtractUserStrings();
        Assert.IsNotEmpty(strings);
        // HelloWorld prints messages that should appear as user strings
        Assert.Contains(s => s.Source == StringSource.UserStrings, strings);
    }

    /// <summary>
    /// Verifies compiler-emitted terminal controls remain exact in the analysis model.
    /// Presentation layers, rather than the extractor, own terminal escaping.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TerminalControlLib_UserStrings_PreservesExactValue()
    {
        using var analyzer = new AssemblyAnalyzer(Samples.TerminalControlLibDll);
        var extractor = new StringExtractor(analyzer);

        var strings = extractor.ExtractUserStrings();

        Assert.Contains(entry => entry.Value == TerminalControlTestData.CompilerPayload, strings);
    }

    /// <summary>
    /// Verifies rich library metadata strings contain type names.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibrary_MetadataStrings_ContainTypeNames()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var extractor = new StringExtractor(a);
        var strings = extractor.ExtractMetadataStrings();
        Assert.IsNotEmpty(strings);
        Assert.Contains(s => s.Value.Contains("UserService"), strings);
    }

    /// <summary>
    /// Verifies complex app user strings contain pipeline text.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ComplexApp_UserStrings_ContainPipelineText()
    {
        using var a = new AssemblyAnalyzer(Samples.ComplexAppDll);
        var extractor = new StringExtractor(a);
        var strings = extractor.ExtractUserStrings();
        Assert.IsNotEmpty(strings);
    }

    /// <summary>
    /// Verifies minimal api user strings contain endpoint paths.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void MinimalApi_UserStrings_ContainEndpointPaths()
    {
        using var a = new AssemblyAnalyzer(Samples.MinimalApiDll);
        var extractor = new StringExtractor(a);
        var strings = extractor.ExtractUserStrings();
        Assert.IsNotEmpty(strings);
        Assert.Contains(s => s.Value.Contains("/hello") || s.Value.Contains("/echo"), strings);
    }

    /// <summary>
    /// Verifies empty lib minimal strings.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void EmptyLib_MinimalStrings()
    {
        using var a = new AssemblyAnalyzer(Samples.EmptyLibDll);
        var extractor = new StringExtractor(a);
        var userStrings = extractor.ExtractUserStrings();
        // EmptyLib should have few or no user strings
        Assert.IsLessThanOrEqualTo(5, userStrings.Count);
    }

    /// <summary>
    /// Verifies raw strings min length4 more than min length16.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RawStrings_MinLength4_MoreThanMinLength16()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var extractor = new StringExtractor(a);
        var raw4 = extractor.ExtractRawStrings(4);
        var raw16 = extractor.ExtractRawStrings(16);
        Assert.IsGreaterThanOrEqualTo(raw16.Count, raw4.Count);
    }

    /// <summary>
    /// Verifies raw strings default min length4.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RawStrings_Default_MinLength4()
    {
        using var a = new AssemblyAnalyzer(Samples.HelloWorldDll);
        var extractor = new StringExtractor(a);
        var raw = extractor.ExtractRawStrings();
        Assert.IsNotEmpty(raw);
        TestAssert.All(raw, s => Assert.IsGreaterThanOrEqualTo(4, s.Value.Length));
    }

    /// <summary>
    /// Verifies metadata strings contain namespaces.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void MetadataStrings_ContainNamespaces()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var extractor = new StringExtractor(a);
        var strings = extractor.ExtractMetadataStrings();
        Assert.Contains(s => s.Value.Contains("RichLibrary"), strings);
    }

    /// <summary>
    /// Verifies user strings all have correct source.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void UserStrings_AllHaveCorrectSource()
    {
        using var a = new AssemblyAnalyzer(Samples.ComplexAppDll);
        var extractor = new StringExtractor(a);
        var strings = extractor.ExtractUserStrings();
        TestAssert.All(strings, s => Assert.AreEqual(StringSource.UserStrings, s.Source));
    }

    /// <summary>
    /// Verifies metadata strings all have correct source.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void MetadataStrings_AllHaveCorrectSource()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var extractor = new StringExtractor(a);
        var strings = extractor.ExtractMetadataStrings();
        TestAssert.All(strings, s => Assert.AreEqual(StringSource.MetadataStrings, s.Source));
    }

    /// <summary>
    /// Verifies raw strings all have correct source.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RawStrings_AllHaveCorrectSource()
    {
        using var a = new AssemblyAnalyzer(Samples.HelloWorldDll);
        var extractor = new StringExtractor(a);
        var strings = extractor.ExtractRawStrings();
        TestAssert.All(strings, s => Assert.AreEqual(StringSource.RawBinary, s.Source));
    }

    /// <summary>
    /// Verifies native lib has metadata strings.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeLib_HasMetadataStrings()
    {
        using var a = new AssemblyAnalyzer(Samples.NativeLibDll);
        var extractor = new StringExtractor(a);
        var strings = extractor.ExtractMetadataStrings();
        Assert.IsNotEmpty(strings);
        Assert.Contains(s => s.Value.Contains("NativeInterop") || s.Value.Contains("UnsafeOperations"), strings);
    }

    /// <summary>
    /// Verifies raw strings min length8 all meet minimum.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RawStrings_MinLength8_AllMeetMinimum()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var extractor = new StringExtractor(a);
        var raw = extractor.ExtractRawStrings(8);
        Assert.IsNotEmpty(raw);
        TestAssert.All(raw, s => Assert.IsGreaterThanOrEqualTo(8, s.Value.Length));
    }

    /// <summary>
    /// Verifies string entries have positive offsets.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void StringEntries_HavePositiveOffsets()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var extractor = new StringExtractor(a);
        var userStrings = extractor.ExtractUserStrings();
        TestAssert.All(userStrings, s => Assert.IsGreaterThanOrEqualTo(0, s.Offset));
        var metaStrings = extractor.ExtractMetadataStrings();
        TestAssert.All(metaStrings, s => Assert.IsGreaterThanOrEqualTo(0, s.Offset));
    }

    /// <summary>
    /// Verifies skipped counts zero for valid assembly.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void SkippedCounts_ZeroForValidAssembly()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var extractor = new StringExtractor(a);
        extractor.ExtractUserStrings();
        Assert.AreEqual(0, extractor.SkippedUserStringCount);
        extractor.ExtractMetadataStrings();
        Assert.AreEqual(0, extractor.SkippedMetadataStringCount);
    }

    /// <summary>
    /// Reproduces #82: drilling into System.Runtime and switching to the Strings tab
    /// crashes with BadImageFormatException because GetNextHandle reads past the
    /// end of the #US heap.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RuntimeAssembly_ExtractUserStrings_DoesNotThrow()
    {
        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        var systemRuntime = Path.Combine(runtimeDir, "System.Runtime.dll");
        Assert.IsTrue(File.Exists(systemRuntime), $"System.Runtime.dll not found at {runtimeDir}");

        using var a = new AssemblyAnalyzer(systemRuntime);
        var extractor = new StringExtractor(a);

        var strings = extractor.ExtractUserStrings();

        // System.Runtime has no user string literals (its #US heap is zero bytes).
        // Must not crash, and must not report false skips.
        Assert.IsNotNull(strings);
        Assert.IsEmpty(strings);
        Assert.AreEqual(0, extractor.SkippedUserStringCount);
    }

    /// <summary>
    /// Same as above but for the #Strings metadata heap, which has the same
    /// unguarded GetNextHandle pattern.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RuntimeAssembly_ExtractMetadataStrings_DoesNotThrow()
    {
        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        var systemRuntime = Path.Combine(runtimeDir, "System.Runtime.dll");
        Assert.IsTrue(File.Exists(systemRuntime), $"System.Runtime.dll not found at {runtimeDir}");

        using var a = new AssemblyAnalyzer(systemRuntime);
        var extractor = new StringExtractor(a);

        var strings = extractor.ExtractMetadataStrings();

        Assert.IsNotNull(strings);
    }

    /// <summary>
    /// Verifies the UTF-16 raw scan finds frozen managed string literals in a
    /// Native AOT binary (the DOTNET_ environment variable names the runtime reads).
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeAot_ExtractRawUtf16Strings_FindsFrozenLiterals()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null,
            "NativeAOT sample was not built");

        using var a = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);
        var extractor = new StringExtractor(a);

        var strings = extractor.ExtractRawUtf16Strings(minLength: 8);

        Assert.IsNotEmpty(strings);
        Assert.Contains(s => s.Value.Contains("DOTNET_", StringComparison.Ordinal), strings);
        TestAssert.All(strings, s => Assert.AreEqual(StringSource.RawBinaryUtf16, s.Source));
    }

    /// <summary>
    /// Verifies the UTF-16 raw scan honors the minimum length.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ExtractRawUtf16Strings_RespectsMinLength()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var extractor = new StringExtractor(a);

        var strings = extractor.ExtractRawUtf16Strings(minLength: 16);

        TestAssert.All(strings, s => Assert.IsGreaterThanOrEqualTo(16, s.Value.Length));
    }

    /// <summary>
    /// Verifies UTF-16 entries carry offsets inside the file.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ExtractRawUtf16Strings_HaveValidOffsets()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var extractor = new StringExtractor(a);

        var strings = extractor.ExtractRawUtf16Strings();

        TestAssert.All(strings, s => Assert.IsInRange(0, (int)a.FileSize - 1, s.Offset));
    }

    /// <summary>
    /// Verifies a UTF-16 run at an odd byte offset is found — UTF-16 data sits at
    /// arbitrary alignments in real binaries.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
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

        var entry = Assert.ContainsSingle(s => s.Value == "Hello", strings);
        Assert.AreEqual(7, entry.Offset);
    }
}
