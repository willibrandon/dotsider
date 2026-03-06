using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using Dotsider.Views;

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

    [Benchmark(Description = "CoreLib short pattern (2B)")]
    public List<long> CoreLib_ShortPattern()
        => HexDumpView.FindBytePattern(_coreLibBytes, _shortPattern);

    [Benchmark(Description = "CoreLib long pattern (14B)")]
    public List<long> CoreLib_LongPattern()
        => HexDumpView.FindBytePattern(_coreLibBytes, _longPattern);

    [Benchmark(Description = "CoreLib no-match (full scan)")]
    public List<long> CoreLib_NoMatch()
        => HexDumpView.FindBytePattern(_coreLibBytes, _noMatchPattern);

    [Benchmark(Description = "Xml short pattern (2B)")]
    public List<long> Xml_ShortPattern()
        => HexDumpView.FindBytePattern(_xmlBytes, _shortPattern);

    [Benchmark(Description = "Xml long pattern (14B)")]
    public List<long> Xml_LongPattern()
        => HexDumpView.FindBytePattern(_xmlBytes, _longPattern);

    [Benchmark(Description = "Xml no-match (full scan)")]
    public List<long> Xml_NoMatch()
        => HexDumpView.FindBytePattern(_xmlBytes, _noMatchPattern);
}
