using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Benchmarks;

/// <summary>
/// Benchmarks for <see cref="StringExtractor"/> extraction methods against BCL assemblies.
/// </summary>
[MemoryDiagnoser]
public class StringExtractorBenchmarks
{
    private AssemblyAnalyzer _coreLibAnalyzer = null!;
    private AssemblyAnalyzer _xmlAnalyzer = null!;

    /// <summary>
    /// Opens CoreLib and Xml analyzers for reuse across all extraction benchmarks.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        _coreLibAnalyzer = new AssemblyAnalyzer(Path.Combine(runtimeDir, "System.Private.CoreLib.dll"));
        _xmlAnalyzer = new AssemblyAnalyzer(Path.Combine(runtimeDir, "System.Private.Xml.dll"));
    }

    /// <summary>
    /// Disposes the shared analyzers.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _coreLibAnalyzer.Dispose();
        _xmlAnalyzer.Dispose();
    }

    /// <summary>
    /// Walks the CoreLib UserString heap — every ldstr-referenced literal.
    /// </summary>
    [Benchmark(Description = "CoreLib UserStrings")]
    public IReadOnlyList<StringEntry> CoreLib_UserStrings()
        => new StringExtractor(_coreLibAnalyzer).ExtractUserStrings();

    /// <summary>
    /// Walks the CoreLib #Strings metadata heap — type/member names.
    /// </summary>
    [Benchmark(Description = "CoreLib MetadataStrings")]
    public IReadOnlyList<StringEntry> CoreLib_MetadataStrings()
        => new StringExtractor(_coreLibAnalyzer).ExtractMetadataStrings();

    /// <summary>
    /// Scans the CoreLib PE for raw printable strings outside the metadata heaps.
    /// </summary>
    [Benchmark(Description = "CoreLib RawStrings")]
    public IReadOnlyList<StringEntry> CoreLib_RawStrings()
        => new StringExtractor(_coreLibAnalyzer).ExtractRawStrings();

    /// <summary>
    /// Walks the Xml UserString heap.
    /// </summary>
    [Benchmark(Description = "Xml UserStrings")]
    public IReadOnlyList<StringEntry> Xml_UserStrings()
        => new StringExtractor(_xmlAnalyzer).ExtractUserStrings();

    /// <summary>
    /// Walks the Xml #Strings metadata heap.
    /// </summary>
    [Benchmark(Description = "Xml MetadataStrings")]
    public IReadOnlyList<StringEntry> Xml_MetadataStrings()
        => new StringExtractor(_xmlAnalyzer).ExtractMetadataStrings();

    /// <summary>
    /// Scans the Xml PE for raw printable strings.
    /// </summary>
    [Benchmark(Description = "Xml RawStrings")]
    public IReadOnlyList<StringEntry> Xml_RawStrings()
        => new StringExtractor(_xmlAnalyzer).ExtractRawStrings();

    /// <summary>
    /// Scans the CoreLib PE for raw UTF-16 strings — the parity-restarting pass
    /// that surfaces frozen literals in Native AOT images.
    /// </summary>
    [Benchmark(Description = "CoreLib RawUtf16Strings")]
    public IReadOnlyList<StringEntry> CoreLib_RawUtf16Strings()
        => new StringExtractor(_coreLibAnalyzer).ExtractRawUtf16Strings();

    /// <summary>
    /// Scans the Xml PE for raw UTF-16 strings.
    /// </summary>
    [Benchmark(Description = "Xml RawUtf16Strings")]
    public IReadOnlyList<StringEntry> Xml_RawUtf16Strings()
        => new StringExtractor(_xmlAnalyzer).ExtractRawUtf16Strings();
}
