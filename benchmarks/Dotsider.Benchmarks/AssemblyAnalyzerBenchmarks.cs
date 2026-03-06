using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Dotsider.Analysis;

namespace Dotsider.Benchmarks;

/// <summary>
/// Benchmarks for <see cref="AssemblyAnalyzer"/> construction against BCL assemblies
/// of varying sizes.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class AssemblyAnalyzerBenchmarks
{
    private string _coreLibPath = null!;
    private string _xmlPath = null!;
    private AssemblyAnalyzer? _lastAnalyzer;

    [GlobalSetup]
    public void Setup()
    {
        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        _coreLibPath = Path.Combine(runtimeDir, "System.Private.CoreLib.dll");
        _xmlPath = Path.Combine(runtimeDir, "System.Private.Xml.dll");

        if (!File.Exists(_coreLibPath))
            throw new FileNotFoundException($"BCL assembly not found: {_coreLibPath}");
        if (!File.Exists(_xmlPath))
            throw new FileNotFoundException($"BCL assembly not found: {_xmlPath}");
    }

    // IterationCleanup with no explicit InvocationCount/UnrollFactor
    // gives 1 invocation per iteration. Do not add those attributes to
    // Construction benchmarks without switching to a list-based cleanup.
    [IterationCleanup]
    public void CleanupConstruction()
    {
        _lastAnalyzer?.Dispose();
        _lastAnalyzer = null;
    }

    [Benchmark(Description = "CoreLib (~16MB)")]
    [BenchmarkCategory("Construction")]
    public AssemblyAnalyzer ConstructCoreLib()
        => _lastAnalyzer = new AssemblyAnalyzer(_coreLibPath);

    [Benchmark(Description = "Xml (~8MB)")]
    [BenchmarkCategory("Construction")]
    public AssemblyAnalyzer ConstructXml()
        => _lastAnalyzer = new AssemblyAnalyzer(_xmlPath);

    [Benchmark(Description = "CoreLib TypeDefs")]
    [BenchmarkCategory("TypeDefs")]
    public int EnumerateTypeDefsCoreLib()
    {
        using var analyzer = new AssemblyAnalyzer(_coreLibPath);
        return analyzer.TypeDefs.Count;
    }

    [Benchmark(Description = "Xml TypeDefs")]
    [BenchmarkCategory("TypeDefs")]
    public int EnumerateTypeDefsXml()
    {
        using var analyzer = new AssemblyAnalyzer(_xmlPath);
        return analyzer.TypeDefs.Count;
    }

    [Benchmark(Description = "CoreLib MethodDefs")]
    [BenchmarkCategory("MethodDefs")]
    public int EnumerateMethodDefsCoreLib()
    {
        using var analyzer = new AssemblyAnalyzer(_coreLibPath);
        return analyzer.MethodDefs.Count;
    }

    [Benchmark(Description = "Xml MethodDefs")]
    [BenchmarkCategory("MethodDefs")]
    public int EnumerateMethodDefsXml()
    {
        using var analyzer = new AssemblyAnalyzer(_xmlPath);
        return analyzer.MethodDefs.Count;
    }
}
