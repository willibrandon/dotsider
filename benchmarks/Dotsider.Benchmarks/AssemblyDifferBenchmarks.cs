using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Benchmarks;

/// <summary>
/// Benchmarks for <see cref="AssemblyDiffer.Compare"/> which uses dictionary-based
/// O(n) matching to diff two assemblies by type, method, and reference.
/// </summary>
[MemoryDiagnoser]
public class AssemblyDifferBenchmarks
{
    private AssemblyAnalyzer _coreLibAnalyzer = null!;
    private AssemblyAnalyzer _xmlAnalyzer = null!;

    /// <summary>Opens CoreLib and Xml analyzers so comparisons have two fully-initialized assemblies on hand.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        _coreLibAnalyzer = new AssemblyAnalyzer(Path.Combine(runtimeDir, "System.Private.CoreLib.dll"));
        _xmlAnalyzer = new AssemblyAnalyzer(Path.Combine(runtimeDir, "System.Private.Xml.dll"));
    }

    /// <summary>Disposes the shared analyzers.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _coreLibAnalyzer.Dispose();
        _xmlAnalyzer.Dispose();
    }

    /// <summary>Worst case: diffing two unrelated assemblies populates every add/remove bucket in the result.</summary>
    [Benchmark(Description = "CoreLib vs Xml (max diff)")]
    public AssemblyDiffResult Compare_CrossAssembly()
        => AssemblyDiffer.Compare(_coreLibAnalyzer, _xmlAnalyzer);

    /// <summary>Compares an assembly against itself to characterize the identity-diff fast path.</summary>
    [Benchmark(Description = "CoreLib vs CoreLib (identity)")]
    public AssemblyDiffResult Compare_Identity()
        => AssemblyDiffer.Compare(_coreLibAnalyzer, _coreLibAnalyzer);
}
