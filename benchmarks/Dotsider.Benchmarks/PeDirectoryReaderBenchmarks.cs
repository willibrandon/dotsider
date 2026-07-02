using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Benchmarks;

/// <summary>
/// Benchmarks for <see cref="PeDirectoryReader"/> import, export, and load-config
/// parsing against a real Native AOT executable (11 import modules) and CoreLib
/// (a managed PE with a near-empty import surface).
/// </summary>
[MemoryDiagnoser]
public class PeDirectoryReaderBenchmarks
{
    private FileStream _nativeAotStream = null!;
    private FileStream _coreLibStream = null!;
    private PEReader _nativeAotPe = null!;
    private PEReader _coreLibPe = null!;

    /// <summary>
    /// Publishes the Native AOT sample and opens PE readers for reuse.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        BenchmarkHelpers.PublishNativeAotSample("samples/NativeAotConsole");

        _nativeAotStream = File.OpenRead(
            BenchmarkHelpers.GetPublishPath("samples/NativeAotConsole", "NativeAotConsole"));
        _nativeAotPe = new PEReader(_nativeAotStream);

        _coreLibStream = File.OpenRead(Path.Combine(
            RuntimeEnvironment.GetRuntimeDirectory(), "System.Private.CoreLib.dll"));
        _coreLibPe = new PEReader(_coreLibStream);
    }

    /// <summary>
    /// Disposes the shared PE readers and streams.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _nativeAotPe.Dispose();
        _coreLibPe.Dispose();
        _nativeAotStream.Dispose();
        _coreLibStream.Dispose();
    }

    /// <summary>
    /// Walks the Native AOT import descriptors, thunk tables, and hint/name entries.
    /// </summary>
    [Benchmark(Description = "NativeAOT ReadImports")]
    public IReadOnlyList<ImportedModuleInfo> NativeAot_ReadImports()
        => PeDirectoryReader.ReadImports(_nativeAotPe);

    /// <summary>
    /// Reads the Native AOT export directory (typically empty for an executable).
    /// </summary>
    [Benchmark(Description = "NativeAOT ReadExports")]
    public IReadOnlyList<ExportedFunctionInfo> NativeAot_ReadExports()
        => PeDirectoryReader.ReadExports(_nativeAotPe);

    /// <summary>
    /// Parses the Native AOT load configuration directory including guard-flag decoding.
    /// </summary>
    [Benchmark(Description = "NativeAOT ReadLoadConfig")]
    public LoadConfigInfo? NativeAot_ReadLoadConfig()
        => PeDirectoryReader.ReadLoadConfig(_nativeAotPe);

    /// <summary>
    /// Walks CoreLib's import table — the managed-PE fast path.
    /// </summary>
    [Benchmark(Description = "CoreLib ReadImports")]
    public IReadOnlyList<ImportedModuleInfo> CoreLib_ReadImports()
        => PeDirectoryReader.ReadImports(_coreLibPe);
}
