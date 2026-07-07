using BenchmarkDotNet.Attributes;
using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using System.Runtime.InteropServices;

namespace Dotsider.Benchmarks;

/// <summary>
/// Benchmarks for the native import/export/load-config readers, measured through the
/// format-aware <see cref="AssemblyAnalyzer"/> properties so each platform exercises
/// its own parser: PE directories on Windows, ELF dynamic tables on Linux, and Mach-O
/// load commands on macOS. CoreLib gives a managed-PE reference point on every OS.
/// </summary>
[MemoryDiagnoser]
public class PeDirectoryReaderBenchmarks
{
    private string _nativeAotPath = null!;
    private string _coreLibPath = null!;

    /// <summary>
    /// Publishes the Native AOT sample and resolves the binary paths.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        BenchmarkHelpers.PublishNativeAotSample("samples/NativeAotConsole");
        _nativeAotPath = BenchmarkHelpers.GetPublishPath("samples/NativeAotConsole", "NativeAotConsole");
        _coreLibPath = Path.Combine(
            RuntimeEnvironment.GetRuntimeDirectory(), "System.Private.CoreLib.dll");
    }

    /// <summary>
    /// Reads the Native AOT import table in the platform's native format.
    /// </summary>
    [Benchmark(Description = "NativeAOT Imports")]
    public IReadOnlyList<ImportedModuleInfo> NativeAot_Imports()
    {
        using var analyzer = new AssemblyAnalyzer(_nativeAotPath);
        return analyzer.Imports;
    }

    /// <summary>
    /// Reads the Native AOT export table in the platform's native format.
    /// </summary>
    [Benchmark(Description = "NativeAOT Exports")]
    public IReadOnlyList<ExportedFunctionInfo> NativeAot_Exports()
    {
        using var analyzer = new AssemblyAnalyzer(_nativeAotPath);
        return analyzer.Exports;
    }

    /// <summary>
    /// Reads the Native AOT load configuration (PE-only; empty on ELF and Mach-O).
    /// </summary>
    [Benchmark(Description = "NativeAOT LoadConfig")]
    public LoadConfigInfo? NativeAot_LoadConfig()
    {
        using var analyzer = new AssemblyAnalyzer(_nativeAotPath);
        return analyzer.LoadConfig;
    }

    /// <summary>
    /// Reads CoreLib's import table — the managed-PE reference point.
    /// </summary>
    [Benchmark(Description = "CoreLib Imports")]
    public IReadOnlyList<ImportedModuleInfo> CoreLib_Imports()
    {
        using var analyzer = new AssemblyAnalyzer(_coreLibPath);
        return analyzer.Imports;
    }
}
