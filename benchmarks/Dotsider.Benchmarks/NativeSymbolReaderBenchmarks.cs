using System.Buffers.Binary;
using BenchmarkDotNet.Attributes;
using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Core.Analysis.NativePdb;

namespace Dotsider.Benchmarks;

/// <summary>
/// Benchmarks for <see cref="NativeSymbolReader"/> against the real NativeAotConsole publish:
/// the full symbol read beside the platform's artifact (native PDB on Windows, <c>.dbg</c> on
/// Linux, dSYM on macOS), the unwind-data boundary fallback with every sidecar out of reach,
/// and the cheap PDB identity probe the analyzer constructor runs. The probe is plain file
/// reading, so on legs whose publish makes no PDB it targets a staged minimal MSF — the same
/// superblock, block-map, directory, and stream-1 reads, just a smaller directory.
/// </summary>
[MemoryDiagnoser]
public class NativeSymbolReaderBenchmarks
{
    private string _exePath = null!;
    private byte[] _exeBytes = null!;
    private IReadOnlyList<RecoveredType> _recoveredTypes = null!;
    private string _bareExePath = null!;
    private byte[] _bareExeBytes = null!;
    private string _pdbPath = null!;
    private string _tempDir = null!;

    /// <summary>
    /// Publishes the Native AOT sample (cached across benchmark classes), captures its bytes
    /// and recovered metadata, and stages a sidecar-free copy for the fallback benchmark plus
    /// a probe target on legs whose publish produces no PDB.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        BenchmarkHelpers.PublishNativeAotSample("samples/NativeAotConsole");
        _exePath = BenchmarkHelpers.GetPublishPath("samples/NativeAotConsole", "NativeAotConsole");
        _exeBytes = File.ReadAllBytes(_exePath);
        using var analyzer = new AssemblyAnalyzer(_exePath);
        _recoveredTypes = analyzer.RecoveredTypes;

        // A copy with no sidecars in reach forces the unwind-data fallback.
        _tempDir = Directory.CreateTempSubdirectory("dotsider-bench-symbols-").FullName;
        _bareExePath = Path.Combine(_tempDir, Path.GetFileName(_exePath));
        File.Copy(_exePath, _bareExePath);
        _bareExeBytes = File.ReadAllBytes(_bareExePath);

        var pdb = Path.Combine(Path.GetDirectoryName(_exePath)!, "NativeAotConsole.pdb");
        _pdbPath = File.Exists(pdb) ? pdb : StageMinimalPdb();
        if (!NativePdbReader.TryReadPdbId(_pdbPath, out _, out _))
            throw new InvalidOperationException($"probe target is not a readable PDB: {_pdbPath}");
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
    /// Runs the constructor's cheap PDB identity probe: the real publish PDB on Windows, the
    /// staged minimal MSF elsewhere.
    /// </summary>
    /// <returns>Whether the probe read an identity.</returns>
    [Benchmark(Description = "TryReadPdbId probe")]
    public bool ProbePdbId() => NativePdbReader.TryReadPdbId(_pdbPath, out _, out _);

    /// <summary>
    /// Writes a minimal MSF 7.0 container — superblock, free-block map, a stream-1 block with
    /// version/signature/age/GUID, the directory, and the block map — so the probe's targeted
    /// reads run on legs whose publish never produces a PDB.
    /// </summary>
    private string StageMinimalPdb()
    {
        const int blockSize = 4096;
        byte[] magic = [.. "Microsoft C/C++ MSF 7.00\r\n"u8, 0x1A, .. "DS"u8, 0, 0, 0];

        // Blocks: 0 superblock, 1 free-block map, 2 stream-1 data, 3 directory, 4 block map.
        var image = new byte[5 * blockSize];
        magic.CopyTo(image, 0);
        BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(32), blockSize);
        BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(36), 1); // free block map
        BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(40), 5); // block count
        BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(44), 16); // directory bytes
        BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(52), 4); // block map address

        // Stream 1 (PDB info): version, signature, age, GUID.
        var info = image.AsSpan(2 * blockSize);
        BinaryPrimitives.WriteInt32LittleEndian(info, 20000404);
        BinaryPrimitives.WriteInt32LittleEndian(info[4..], 1);
        BinaryPrimitives.WriteInt32LittleEndian(info[8..], 1);
        Guid.NewGuid().TryWriteBytes(info.Slice(12, 16));

        // Directory: numStreams = 2, sizes [0, 28], stream 1's block list = [2].
        var directory = image.AsSpan(3 * blockSize);
        BinaryPrimitives.WriteInt32LittleEndian(directory, 2);
        BinaryPrimitives.WriteInt32LittleEndian(directory[8..], 28);
        BinaryPrimitives.WriteInt32LittleEndian(directory[12..], 2);

        // Block map: the single directory block.
        BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(4 * blockSize), 3);

        var path = Path.Combine(_tempDir, "probe-target.pdb");
        File.WriteAllBytes(path, image);
        return path;
    }
}
