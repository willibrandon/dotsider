using BenchmarkDotNet.Attributes;
using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Benchmarks;

/// <summary>
/// Benchmarks for <see cref="MstatDiffer"/> and the <see cref="MstatSizeIndex"/> normalization
/// it builds on, against the real V1/V2 mstat pair the NativeAotConsole samples publish —
/// thousands of matched entries per side, the shape a CI size gate runs on every pull request.
/// </summary>
[MemoryDiagnoser]
public class MstatDifferBenchmarks
{
    private MstatData _left = null!;
    private MstatData _right = null!;

    /// <summary>
    /// Publishes both Native AOT samples (cached across benchmark classes) and decodes their
    /// mstat sidecars once; the benchmarks measure normalization and comparison, not I/O.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        BenchmarkHelpers.PublishNativeAotSample("samples/NativeAotConsole");
        BenchmarkHelpers.PublishNativeAotSample("samples/NativeAotConsoleV2");

        var v1Exe = BenchmarkHelpers.GetPublishPath("samples/NativeAotConsole", "NativeAotConsole");
        var v2Exe = BenchmarkHelpers.GetPublishPath("samples/NativeAotConsoleV2", "NativeAotConsole");
        _left = ReadMstat(Path.Combine(Path.GetDirectoryName(v1Exe)!, "NativeAotConsole.mstat"));
        _right = ReadMstat(Path.Combine(Path.GetDirectoryName(v2Exe)!, "NativeAotConsole.mstat"));
    }

    private static MstatData ReadMstat(string path) =>
        MstatReader.Read(path)
            ?? throw new InvalidOperationException($"mstat sidecar not readable at {path}");

    /// <summary>Builds the normalized size index for one report.</summary>
    /// <returns>The index, returned so the JIT cannot elide the work.</returns>
    [Benchmark(Description = "Index NativeAotConsole.mstat")]
    public MstatSizeIndex BuildIndex() => MstatSizeIndex.Create(_left);

    /// <summary>Compares the two builds end to end: indexing, matching, tree, aggregates.</summary>
    /// <returns>The diff, returned so the JIT cannot elide the work.</returns>
    [Benchmark(Description = "Diff V1 vs V2 mstat")]
    public MstatDiffResult Compare() => MstatDiffer.Compare(_left, _right);
}
