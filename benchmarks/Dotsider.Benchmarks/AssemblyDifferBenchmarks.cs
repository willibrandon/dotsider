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

    [Benchmark(Description = "CoreLib vs Xml (max diff)")]
    public AssemblyDiffResult Compare_CrossAssembly()
        => AssemblyDiffer.Compare(_coreLibAnalyzer, _xmlAnalyzer);

    [Benchmark(Description = "CoreLib vs CoreLib (identity)")]
    public AssemblyDiffResult Compare_Identity()
        => AssemblyDiffer.Compare(_coreLibAnalyzer, _coreLibAnalyzer);
}
