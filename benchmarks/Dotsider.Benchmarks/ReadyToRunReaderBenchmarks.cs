using BenchmarkDotNet.Attributes;
using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Benchmarks;

/// <summary>
/// Benchmarks for the Native AOT readers, measured through the <see cref="AssemblyAnalyzer"/>
/// properties so each platform exercises its own image format. The frozen object walk and the
/// NativeFormat metadata reader do the bulk of the work; the section walk is a fixed-size table.
/// </summary>
[MemoryDiagnoser]
public class ReadyToRunReaderBenchmarks
{
    private string _nativeAotPath = null!;

    /// <summary>Publishes the Native AOT sample and resolves its path.</summary>
    [GlobalSetup]
    public void Setup()
    {
        BenchmarkHelpers.PublishNativeAotSample("samples/NativeAotConsole");
        _nativeAotPath = BenchmarkHelpers.GetPublishPath("samples/NativeAotConsole", "NativeAotConsole");
    }

    /// <summary>Walks the ReadyToRun section table.</summary>
    [Benchmark(Description = "ReadyToRun sections")]
    public IReadOnlyList<RtrSection> Sections()
    {
        using var analyzer = new AssemblyAnalyzer(_nativeAotPath);
        return analyzer.ReadyToRunSections;
    }

    /// <summary>Recovers frozen string literals from the frozen object region.</summary>
    [Benchmark(Description = "Frozen strings")]
    public IReadOnlyList<StringEntry> FrozenStrings()
    {
        using var analyzer = new AssemblyAnalyzer(_nativeAotPath);
        return analyzer.FrozenStrings;
    }

    /// <summary>Recovers type and method names from the embedded NativeFormat metadata.</summary>
    [Benchmark(Description = "Recovered types")]
    public IReadOnlyList<RecoveredType> RecoveredTypes()
    {
        using var analyzer = new AssemblyAnalyzer(_nativeAotPath);
        return analyzer.RecoveredTypes;
    }
}
