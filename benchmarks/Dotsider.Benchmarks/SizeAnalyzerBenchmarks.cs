using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Benchmarks;

/// <summary>
/// Benchmarks for <see cref="SizeAnalyzer.BuildSizeTree"/> which traverses
/// every type and method to build the treemap hierarchy.
/// </summary>
[MemoryDiagnoser]
public class SizeAnalyzerBenchmarks
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

    [Benchmark(Description = "CoreLib BuildSizeTree")]
    public SizeNode CoreLib_BuildSizeTree()
        => SizeAnalyzer.BuildSizeTree(_coreLibAnalyzer);

    [Benchmark(Description = "Xml BuildSizeTree")]
    public SizeNode Xml_BuildSizeTree()
        => SizeAnalyzer.BuildSizeTree(_xmlAnalyzer);
}
