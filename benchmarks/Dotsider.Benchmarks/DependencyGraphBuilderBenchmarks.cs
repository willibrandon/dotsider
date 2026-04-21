using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

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

    /// <summary>
    /// Opens CoreLib and Xml analyzers to supply assembly and type-ref data to the graph builder.
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
    /// Builds the transitive dependency graph rooted at CoreLib — the widest typical shape.
    /// </summary>
    [Benchmark(Description = "CoreLib graph")]
    public DependencyGraphResult Build_CoreLib()
        => DependencyGraphBuilder.Build(_coreLibAnalyzer);

    /// <summary>
    /// Builds the transitive dependency graph rooted at Xml.
    /// </summary>
    [Benchmark(Description = "Xml graph")]
    public DependencyGraphResult Build_Xml()
        => DependencyGraphBuilder.Build(_xmlAnalyzer);
}
