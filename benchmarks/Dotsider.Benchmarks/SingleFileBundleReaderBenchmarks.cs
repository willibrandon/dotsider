using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Benchmarks;

/// <summary>
/// Benchmarks for <see cref="SingleFileBundleReader"/> covering bundle detection
/// (file and span overloads), manifest parsing, and full entry-assembly extraction.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class SingleFileBundleReaderBenchmarks
{
    private string _bundlePath = null!;
    private string _coreLibPath = null!;
    private byte[] _bundleBytes = null!;
    private byte[] _coreLibBytes = null!;
    private long _headerOffset;

    [GlobalSetup]
    public void Setup()
    {
        BenchmarkHelpers.PublishSelfContainedSample("samples/SelfContainedConsole");
        _bundlePath = BenchmarkHelpers.GetPublishPath("samples/SelfContainedConsole", "SelfContainedConsole");

        if (!File.Exists(_bundlePath))
            throw new FileNotFoundException($"Published bundle not found: {_bundlePath}");

        _coreLibPath = Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "System.Private.CoreLib.dll");

        if (!File.Exists(_coreLibPath))
            throw new FileNotFoundException($"CoreLib not found: {_coreLibPath}");

        _bundleBytes = File.ReadAllBytes(_bundlePath);
        _coreLibBytes = File.ReadAllBytes(_coreLibPath);

        if (!SingleFileBundleReader.IsBundle(_bundlePath, out _headerOffset))
            throw new InvalidOperationException($"Published file is not a bundle: {_bundlePath}");
    }

    [Benchmark(Description = "IsBundle (CoreLib, negative)")]
    [BenchmarkCategory("IsBundle")]
    public bool IsBundle_NonBundle_CoreLib()
        => SingleFileBundleReader.IsBundle(_coreLibPath, out _);

    [Benchmark(Description = "IsBundle (bundle, positive)")]
    [BenchmarkCategory("IsBundle")]
    public bool IsBundle_Bundle()
        => SingleFileBundleReader.IsBundle(_bundlePath, out _);

    [Benchmark(Description = "IsBundle span (CoreLib, negative)")]
    [BenchmarkCategory("IsBundle")]
    public bool IsBundle_Span_NonBundle()
        => SingleFileBundleReader.IsBundle(_coreLibBytes, out _);

    [Benchmark(Description = "IsBundle span (bundle, positive)")]
    [BenchmarkCategory("IsBundle")]
    public bool IsBundle_Span_Bundle()
        => SingleFileBundleReader.IsBundle(_bundleBytes, out _);

    [Benchmark(Description = "ReadManifest")]
    [BenchmarkCategory("ReadManifest")]
    public BundleManifest ReadManifest()
        => SingleFileBundleReader.ReadManifest(_bundlePath, _headerOffset);

    [Benchmark(Description = "FindEntryAssembly")]
    [BenchmarkCategory("FindEntryAssembly")]
    public (byte[] Bytes, string Name)? FindEntryAssembly()
        => SingleFileBundleReader.FindEntryAssembly(_bundlePath);
}
