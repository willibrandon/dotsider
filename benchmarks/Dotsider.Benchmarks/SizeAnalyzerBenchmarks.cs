using BenchmarkDotNet.Attributes;
using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using System.Runtime.InteropServices;

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

    /// <summary>
    /// Opens CoreLib and Xml analyzers so the size tree can be built without construction cost in scope.
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
    /// Walks every CoreLib type and method to build the treemap hierarchy.
    /// </summary>
    [Benchmark(Description = "CoreLib BuildSizeTree")]
    public SizeNode CoreLib_BuildSizeTree()
        => SizeAnalyzer.BuildSizeTree(_coreLibAnalyzer);

    /// <summary>
    /// Walks every Xml type and method to build the treemap hierarchy.
    /// </summary>
    [Benchmark(Description = "Xml BuildSizeTree")]
    public SizeNode Xml_BuildSizeTree()
        => SizeAnalyzer.BuildSizeTree(_xmlAnalyzer);
}
