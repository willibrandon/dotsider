using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using Dotsider.Analysis;
using Dotsider.Analysis.Models;

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
    private IlDisassembler _coreLibDisasm = null!;
    private IlDisassembler _xmlDisasm = null!;

    [GlobalSetup]
    public void Setup()
    {
        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        _coreLibAnalyzer = new AssemblyAnalyzer(Path.Combine(runtimeDir, "System.Private.CoreLib.dll"));
        _xmlAnalyzer = new AssemblyAnalyzer(Path.Combine(runtimeDir, "System.Private.Xml.dll"));
        _coreLibDisasm = new IlDisassembler(_coreLibAnalyzer);
        _xmlDisasm = new IlDisassembler(_xmlAnalyzer);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _coreLibAnalyzer.Dispose();
        _xmlAnalyzer.Dispose();
    }

    [Benchmark(Description = "CoreLib BuildSizeTree")]
    public SizeNode CoreLib_BuildSizeTree()
        => SizeAnalyzer.BuildSizeTree(_coreLibAnalyzer, _coreLibDisasm);

    [Benchmark(Description = "Xml BuildSizeTree")]
    public SizeNode Xml_BuildSizeTree()
        => SizeAnalyzer.BuildSizeTree(_xmlAnalyzer, _xmlDisasm);
}
