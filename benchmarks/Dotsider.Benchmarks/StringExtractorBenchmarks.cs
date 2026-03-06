using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using Dotsider.Analysis;
using Dotsider.Analysis.Models;

namespace Dotsider.Benchmarks;

/// <summary>
/// Benchmarks for <see cref="StringExtractor"/> extraction methods against BCL assemblies.
/// </summary>
[MemoryDiagnoser]
public class StringExtractorBenchmarks
{
    private AssemblyAnalyzer _coreLibAnalyzer = null!;
    private AssemblyAnalyzer _xmlAnalyzer = null!;

    [GlobalSetup]
    public void Setup()
    {
        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        _coreLibAnalyzer = new AssemblyAnalyzer(Path.Combine(runtimeDir, "System.Private.CoreLib.dll"));
        _xmlAnalyzer = new AssemblyAnalyzer(Path.Combine(runtimeDir, "System.Private.Xml.dll"));
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _coreLibAnalyzer.Dispose();
        _xmlAnalyzer.Dispose();
    }

    [Benchmark(Description = "CoreLib UserStrings")]
    public IReadOnlyList<StringEntry> CoreLib_UserStrings()
        => new StringExtractor(_coreLibAnalyzer).ExtractUserStrings();

    [Benchmark(Description = "CoreLib MetadataStrings")]
    public IReadOnlyList<StringEntry> CoreLib_MetadataStrings()
        => new StringExtractor(_coreLibAnalyzer).ExtractMetadataStrings();

    [Benchmark(Description = "CoreLib RawStrings")]
    public IReadOnlyList<StringEntry> CoreLib_RawStrings()
        => new StringExtractor(_coreLibAnalyzer).ExtractRawStrings();

    [Benchmark(Description = "Xml UserStrings")]
    public IReadOnlyList<StringEntry> Xml_UserStrings()
        => new StringExtractor(_xmlAnalyzer).ExtractUserStrings();

    [Benchmark(Description = "Xml MetadataStrings")]
    public IReadOnlyList<StringEntry> Xml_MetadataStrings()
        => new StringExtractor(_xmlAnalyzer).ExtractMetadataStrings();

    [Benchmark(Description = "Xml RawStrings")]
    public IReadOnlyList<StringEntry> Xml_RawStrings()
        => new StringExtractor(_xmlAnalyzer).ExtractRawStrings();
}
