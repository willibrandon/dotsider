using BenchmarkDotNet.Attributes;
using Dotsider.Views;

namespace Dotsider.Benchmarks;

/// <summary>
/// Parameterized benchmark that tests <see cref="HexDumpView.FindBytePattern"/>
/// against synthetic byte arrays of varying sizes to pinpoint where the 8ms
/// adaptive search threshold (HexDumpView line 44) is crossed. The no-match
/// pattern forces a complete scan — the worst-case path that determines whether
/// live search degrades.
/// </summary>
[MemoryDiagnoser]
public class HexSearchThresholdBenchmarks
{
    private byte[] _data = null!;
    private byte[] _pattern = null!;

    /// <summary>Payload size, in megabytes, parameterized to bracket the 8 ms adaptive search threshold.</summary>
    // Cluster around the expected 8ms crossover, with low/high anchors for slope.
    [Params(4, 8, 9, 10, 11, 12, 16)]
    public int SizeMB { get; set; }

    /// <summary>Allocates a random payload of SizeMB and prepares the no-match pattern for a worst-case scan.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _data = new byte[SizeMB * 1024 * 1024];
        Random.Shared.NextBytes(_data);

        // No-match pattern: forces full scan (worst case for threshold)
        _pattern = [0xDE, 0xAD, 0xBE, 0xEF, 0xCA, 0xFE, 0xBA, 0xBE];
    }

    /// <summary>Measures the full-scan worst case that determines whether live-as-you-type search degrades at this size.</summary>
    [Benchmark(Description = "FindBytePattern (no match, full scan)")]
    public List<long> FullScan()
        => HexDumpView.FindBytePattern(_data, _pattern);
}
