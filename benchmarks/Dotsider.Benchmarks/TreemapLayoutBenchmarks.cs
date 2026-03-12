using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Views;

namespace Dotsider.Benchmarks;

/// <summary>
/// Benchmarks for <see cref="TreemapLayout.Layout"/> which computes squarified treemap
/// rectangles for assembly size trees.
/// </summary>
[MemoryDiagnoser]
public class TreemapLayoutBenchmarks
{
    private AssemblyAnalyzer _coreLibAnalyzer = null!;
    private AssemblyAnalyzer _xmlAnalyzer = null!;
    private IReadOnlyList<SizeNode> _coreLibChildren = null!;
    private IReadOnlyList<SizeNode> _xmlChildren = null!;

    [GlobalSetup]
    public void Setup()
    {
        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        _coreLibAnalyzer = new AssemblyAnalyzer(Path.Combine(runtimeDir, "System.Private.CoreLib.dll"));
        _xmlAnalyzer = new AssemblyAnalyzer(Path.Combine(runtimeDir, "System.Private.Xml.dll"));

        _coreLibChildren = SizeAnalyzer.BuildSizeTree(_coreLibAnalyzer).Children;
        _xmlChildren = SizeAnalyzer.BuildSizeTree(_xmlAnalyzer).Children;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _coreLibAnalyzer.Dispose();
        _xmlAnalyzer.Dispose();
    }

    [Benchmark(Description = "CoreLib layout (120x30)")]
    public IReadOnlyList<TreemapRect> Layout_CoreLib()
        => TreemapLayout.Layout(_coreLibChildren, 0, 0, 120, 30);

    [Benchmark(Description = "Xml layout (120x30)")]
    public IReadOnlyList<TreemapRect> Layout_Xml()
        => TreemapLayout.Layout(_xmlChildren, 0, 0, 120, 30);

    [Benchmark(Description = "CoreLib layout (240x60)")]
    public IReadOnlyList<TreemapRect> Layout_CoreLib_Large()
        => TreemapLayout.Layout(_coreLibChildren, 0, 0, 240, 60);
}
