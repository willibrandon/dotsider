using BenchmarkDotNet.Attributes;
using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Core.Analysis.NativePdb;

namespace Dotsider.Benchmarks;

/// <summary>
/// Benchmarks for <see cref="NativeSymbolReader"/> against the real NativeAotConsole publish:
/// the full symbol read beside the platform's artifact (native PDB on Windows, <c>.dbg</c> on
/// Linux, dSYM on macOS), the unwind-data boundary fallback with every sidecar out of reach,
/// and the cheap PDB identity probe the analyzer constructor runs (Windows artifact only).
/// </summary>
[MemoryDiagnoser]
public class NativeSymbolReaderBenchmarks
{
    private string _exePath = null!;
    private byte[] _exeBytes = null!;
    private IReadOnlyList<RecoveredType> _recoveredTypes = null!;
    private string _bareExePath = null!;
    private byte[] _bareExeBytes = null!;
    private string? _pdbPath;
    private string _tempDir = null!;

    /// <summary>
    /// Publishes the Native AOT sample (cached across benchmark classes), captures its bytes
    /// and recovered metadata, and stages a sidecar-free copy for the fallback benchmark.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        BenchmarkHelpers.PublishNativeAotSample("samples/NativeAotConsole");
        _exePath = BenchmarkHelpers.GetPublishPath("samples/NativeAotConsole", "NativeAotConsole");
        _exeBytes = File.ReadAllBytes(_exePath);
        using var analyzer = new AssemblyAnalyzer(_exePath);
        _recoveredTypes = analyzer.RecoveredTypes;

        var pdb = Path.Combine(Path.GetDirectoryName(_exePath)!, "NativeAotConsole.pdb");
        _pdbPath = File.Exists(pdb) ? pdb : null;

        // A copy with no sidecars in reach forces the unwind-data fallback.
        _tempDir = Directory.CreateTempSubdirectory("dotsider-bench-symbols-").FullName;
        _bareExePath = Path.Combine(_tempDir, Path.GetFileName(_exePath));
        File.Copy(_exePath, _bareExePath);
        _bareExeBytes = File.ReadAllBytes(_bareExePath);
    }

    /// <summary>Removes the staged sidecar-free copy.</summary>
    [GlobalCleanup]
    public void Cleanup() => Directory.Delete(_tempDir, recursive: true);

    /// <summary>Reads the full symbol set beside the platform's symbol artifact.</summary>
    /// <returns>The symbol info, returned so the JIT cannot elide the work.</returns>
    [Benchmark(Description = "Read symbols (platform artifact)")]
    public NativeSymbolInfo ReadSymbols() =>
        NativeSymbolReader.Read(_exePath, _exeBytes, _recoveredTypes);

    /// <summary>Recovers function boundaries with every sidecar out of reach.</summary>
    /// <returns>The boundary info, returned so the JIT cannot elide the work.</returns>
    [Benchmark(Description = "Boundary fallback (no sidecars)")]
    public NativeSymbolInfo ReadBoundaries() =>
        NativeSymbolReader.Read(_bareExePath, _bareExeBytes, _recoveredTypes);

    /// <summary>
    /// Runs the constructor's cheap PDB identity probe; a fast false on the legs whose
    /// artifact is not a PDB.
    /// </summary>
    /// <returns>Whether the probe read an identity.</returns>
    [Benchmark(Description = "TryReadPdbId probe")]
    public bool ProbePdbId() =>
        _pdbPath is not null && NativePdbReader.TryReadPdbId(_pdbPath, out _, out _);
}
