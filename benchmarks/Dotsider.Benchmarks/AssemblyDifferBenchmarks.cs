using BenchmarkDotNet.Attributes;
using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using System.Runtime.InteropServices;

namespace Dotsider.Benchmarks;

/// <summary>
/// Benchmarks for <see cref="AssemblyDiffer.Compare"/> which uses dictionary-based
/// O(n) matching to diff two assemblies by type, method, and reference.
/// </summary>
[MemoryDiagnoser]
public class AssemblyDifferBenchmarks
{
    private AssemblyAnalyzer _coreLibAnalyzer = null!;
    private AssemblyAnalyzer _coreLibAnalyzer2 = null!;
    private AssemblyAnalyzer _xmlAnalyzer = null!;
    private AssemblyAnalyzer _richV1Analyzer = null!;
    private AssemblyAnalyzer _richV2Analyzer = null!;

    /// <summary>
    /// Opens all analyzers for benchmarking. Creates two distinct CoreLib instances
    /// to measure real body comparison cost with separate PEReader instances.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        var coreLibPath = Path.Combine(runtimeDir, "System.Private.CoreLib.dll");
        _coreLibAnalyzer = new AssemblyAnalyzer(coreLibPath);
        _coreLibAnalyzer2 = new AssemblyAnalyzer(coreLibPath);
        _xmlAnalyzer = new AssemblyAnalyzer(Path.Combine(runtimeDir, "System.Private.Xml.dll"));

        BenchmarkHelpers.BuildSample("samples/RichLibrary");
        BenchmarkHelpers.BuildSample("samples/RichLibraryV2");
        _richV1Analyzer = new AssemblyAnalyzer(
            BenchmarkHelpers.GetBuildPath("samples/RichLibrary", "RichLibrary.dll"));
        _richV2Analyzer = new AssemblyAnalyzer(
            BenchmarkHelpers.GetBuildPath("samples/RichLibraryV2", "RichLibrary.dll"));
    }

    /// <summary>
    /// Disposes all shared analyzers.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _coreLibAnalyzer.Dispose();
        _coreLibAnalyzer2.Dispose();
        _xmlAnalyzer.Dispose();
        _richV1Analyzer.Dispose();
        _richV2Analyzer.Dispose();
    }

    /// <summary>
    /// Worst case: diffing two unrelated assemblies populates every add/remove bucket in the result.
    /// </summary>
    [Benchmark(Description = "CoreLib vs Xml (max diff)")]
    public AssemblyDiffResult Compare_CrossAssembly()
        => AssemblyDiffer.Compare(_coreLibAnalyzer, _xmlAnalyzer);

    /// <summary>
    /// Compares an assembly against itself to characterize the identity-diff fast path
    /// where both sides share the same analyzer instance.
    /// </summary>
    [Benchmark(Description = "CoreLib vs CoreLib (identity)")]
    public AssemblyDiffResult Compare_Identity()
        => AssemblyDiffer.Compare(_coreLibAnalyzer, _coreLibAnalyzer);

    /// <summary>
    /// Compares two distinct analyzer instances for the same CoreLib file.
    /// Measures the real cost of body comparison with separate PEReader instances
    /// and normalized token resolution.
    /// </summary>
    [Benchmark(Description = "CoreLib vs CoreLib (distinct)")]
    public AssemblyDiffResult Compare_DistinctAnalyzers()
        => AssemblyDiffer.Compare(_coreLibAnalyzer, _coreLibAnalyzer2);

    /// <summary>
    /// Compares RichLibrary v1 vs v2 — a realistic diff with body changes,
    /// token churn, exception region differences, and local signature changes.
    /// </summary>
    [Benchmark(Description = "RichLibrary v1 vs v2 (body diff)")]
    public AssemblyDiffResult Compare_RichLibraryVersions()
        => AssemblyDiffer.Compare(_richV1Analyzer, _richV2Analyzer);
}
