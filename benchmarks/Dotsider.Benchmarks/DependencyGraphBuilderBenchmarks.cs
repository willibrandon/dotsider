using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using Dotsider.Analysis;
using Dotsider.Analysis.Models;

namespace Dotsider.Benchmarks;

/// <summary>
/// Benchmarks for <see cref="DependencyGraphBuilder.Build"/> which builds a positioned
/// dependency graph from assembly references and type ref counts.
/// </summary>
[MemoryDiagnoser]
public class DependencyGraphBuilderBenchmarks
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

    [Benchmark(Description = "CoreLib graph")]
    public (IReadOnlyList<GraphNode> Nodes, IReadOnlyList<GraphEdge> Edges) Build_CoreLib()
        => DependencyGraphBuilder.Build(_coreLibAnalyzer);

    [Benchmark(Description = "Xml graph")]
    public (IReadOnlyList<GraphNode> Nodes, IReadOnlyList<GraphEdge> Edges) Build_Xml()
        => DependencyGraphBuilder.Build(_xmlAnalyzer);
}
