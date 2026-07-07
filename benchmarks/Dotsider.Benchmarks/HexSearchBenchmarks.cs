using BenchmarkDotNet.Attributes;
using Dotsider.Views;
using System.Runtime.InteropServices;

namespace Dotsider.Benchmarks;

/// <summary>
/// Benchmarks for <see cref="HexDumpView.FindBytePattern"/> against BCL assemblies.
/// The 8ms adaptive threshold (HexDumpView line 44) governs whether hex search runs
/// live-as-you-type or waits for confirmation. These benchmarks establish baselines
/// against real assemblies to validate that threshold.
/// </summary>
[MemoryDiagnoser]
public class HexSearchBenchmarks
{
    private byte[] _coreLibBytes = null!;
    private byte[] _xmlBytes = null!;
    private byte[] _shortPattern = null!;
    private byte[] _longPattern = null!;
    private byte[] _noMatchPattern = null!;

    /// <summary>
    /// Loads BCL assembly bytes and prepares short, long, and no-match search patterns.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        _coreLibBytes = File.ReadAllBytes(Path.Combine(runtimeDir, "System.Private.CoreLib.dll"));
        _xmlBytes = File.ReadAllBytes(Path.Combine(runtimeDir, "System.Private.Xml.dll"));

        // Short pattern: "MZ" header — guaranteed to exist at offset 0
        _shortPattern = "MZ"u8.ToArray();

        // Longer pattern: search for "System.Runtime" — common metadata string
        _longPattern = System.Text.Encoding.ASCII.GetBytes("System.Runtime");

        // No-match pattern: unlikely byte sequence that forces a full scan
        _noMatchPattern = [0xDE, 0xAD, 0xBE, 0xEF, 0xCA, 0xFE, 0xBA, 0xBE];
    }

    /// <summary>
    /// Search cost with a 2-byte pattern that matches at the PE header — dominated by per-match overhead.
    /// </summary>
    [Benchmark(Description = "CoreLib short pattern (2B)")]
    public List<long> CoreLib_ShortPattern()
        => HexDumpView.FindBytePattern(_coreLibBytes, _shortPattern);

    /// <summary>
    /// Search cost with a 14-byte ASCII pattern common in metadata strings.
    /// </summary>
    [Benchmark(Description = "CoreLib long pattern (14B)")]
    public List<long> CoreLib_LongPattern()
        => HexDumpView.FindBytePattern(_coreLibBytes, _longPattern);

    /// <summary>
    /// Worst case for CoreLib: a pattern guaranteed to miss forces a complete scan.
    /// </summary>
    [Benchmark(Description = "CoreLib no-match (full scan)")]
    public List<long> CoreLib_NoMatch()
        => HexDumpView.FindBytePattern(_coreLibBytes, _noMatchPattern);

    /// <summary>
    /// Search cost against the smaller Xml assembly with a 2-byte header-matching pattern.
    /// </summary>
    [Benchmark(Description = "Xml short pattern (2B)")]
    public List<long> Xml_ShortPattern()
        => HexDumpView.FindBytePattern(_xmlBytes, _shortPattern);

    /// <summary>
    /// Search cost against Xml with a 14-byte metadata-string pattern.
    /// </summary>
    [Benchmark(Description = "Xml long pattern (14B)")]
    public List<long> Xml_LongPattern()
        => HexDumpView.FindBytePattern(_xmlBytes, _longPattern);

    /// <summary>
    /// Worst case for Xml: no-match pattern forces a complete scan.
    /// </summary>
    [Benchmark(Description = "Xml no-match (full scan)")]
    public List<long> Xml_NoMatch()
        => HexDumpView.FindBytePattern(_xmlBytes, _noMatchPattern);
}
