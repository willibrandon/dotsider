using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Benchmarks;

/// <summary>
/// Benchmarks for <see cref="NativeAotDetector"/>. The detector scans the whole file
/// for the RTR signature and validates each candidate, so the cases cover a real
/// Native AOT binary (hits a false-positive code immediate before the genuine header),
/// a large ReadyToRun-compiled managed assembly (many rejected candidates, no match),
/// and a native apphost (no candidates at all).
/// </summary>
[MemoryDiagnoser]
public class NativeAotDetectorBenchmarks
{
    private byte[] _nativeAotBytes = null!;
    private byte[] _coreLibBytes = null!;
    private byte[] _apphostBytes = null!;

    /// <summary>
    /// Publishes the Native AOT sample, builds the apphost sample, and loads all
    /// binaries into memory.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        BenchmarkHelpers.PublishNativeAotSample("samples/NativeAotConsole");
        BenchmarkHelpers.BuildSample("samples/HelloWorld");

        _nativeAotBytes = File.ReadAllBytes(
            BenchmarkHelpers.GetPublishPath("samples/NativeAotConsole", "NativeAotConsole"));
        _coreLibBytes = File.ReadAllBytes(Path.Combine(
            RuntimeEnvironment.GetRuntimeDirectory(), "System.Private.CoreLib.dll"));
        _apphostBytes = File.ReadAllBytes(BenchmarkHelpers.GetBuildPath(
            "samples/HelloWorld", "HelloWorld" + BenchmarkHelpers.ApphostExtension));
    }

    /// <summary>
    /// Full detection on a real Native AOT executable: signature scan, false-positive
    /// rejection, header validation, and the runtime-version heuristic.
    /// </summary>
    [Benchmark(Description = "Detect NativeAOT exe (positive)")]
    public NativeAotInfo? Detect_NativeAotExe() => NativeAotDetector.Detect(_nativeAotBytes);

    /// <summary>
    /// Worst-case negative: CoreLib is a large ReadyToRun image whose R2R headers and
    /// code immediates all fail validation, so the scan walks the entire file.
    /// </summary>
    [Benchmark(Description = "Detect CoreLib (R2R negative)")]
    public NativeAotInfo? Detect_CoreLib() => NativeAotDetector.Detect(_coreLibBytes);

    /// <summary>
    /// Native apphost negative: no RTR candidates, pure signature-scan throughput.
    /// </summary>
    [Benchmark(Description = "Detect apphost (negative)")]
    public NativeAotInfo? Detect_Apphost() => NativeAotDetector.Detect(_apphostBytes);
}
