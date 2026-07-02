using BenchmarkDotNet.Attributes;
using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Benchmarks;

/// <summary>
/// Benchmarks for <see cref="MstatReader"/> against the real size report the NativeAotConsole
/// sample publishes. The read decodes every IL stream, resolves every token, and reads every
/// node name from the <c>.names</c> section.
/// </summary>
[MemoryDiagnoser]
public class MstatReaderBenchmarks
{
    private string _mstatPath = null!;

    /// <summary>
    /// Publishes the Native AOT sample (cached across benchmark classes) and locates the
    /// mstat sidecar next to the executable.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        BenchmarkHelpers.PublishNativeAotSample("samples/NativeAotConsole");
        var exe = BenchmarkHelpers.GetPublishPath("samples/NativeAotConsole", "NativeAotConsole");
        _mstatPath = Path.Combine(Path.GetDirectoryName(exe)!, "NativeAotConsole.mstat");
        if (!File.Exists(_mstatPath))
            throw new InvalidOperationException($"mstat sidecar not found at {_mstatPath}");
    }

    /// <summary>Reads and fully decodes the sample's mstat report.</summary>
    /// <returns>The decoded report, returned so the JIT cannot elide the work.</returns>
    [Benchmark(Description = "Read NativeAotConsole.mstat")]
    public MstatData? ReadMstat() => MstatReader.Read(_mstatPath);
}
