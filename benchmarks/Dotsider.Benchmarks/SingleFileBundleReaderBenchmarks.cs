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

    /// <summary>Publishes a self-contained bundle, pre-reads the bytes, and captures the bundle header offset.</summary>
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

    /// <summary>Negative case over a file stream: scans CoreLib without finding a bundle signature.</summary>
    [Benchmark(Description = "IsBundle (CoreLib, negative)")]
    [BenchmarkCategory("IsBundle")]
    public bool IsBundle_NonBundle_CoreLib()
        => SingleFileBundleReader.IsBundle(_coreLibPath, out _);

    /// <summary>Positive case over a file stream: locates the bundle signature in a real bundle.</summary>
    [Benchmark(Description = "IsBundle (bundle, positive)")]
    [BenchmarkCategory("IsBundle")]
    public bool IsBundle_Bundle()
        => SingleFileBundleReader.IsBundle(_bundlePath, out _);

    /// <summary>Negative case over a byte span: avoids file I/O to isolate scan cost.</summary>
    [Benchmark(Description = "IsBundle span (CoreLib, negative)")]
    [BenchmarkCategory("IsBundle")]
    public bool IsBundle_Span_NonBundle()
        => SingleFileBundleReader.IsBundle(_coreLibBytes, out _);

    /// <summary>Positive case over a byte span: finds the bundle signature without touching disk.</summary>
    [Benchmark(Description = "IsBundle span (bundle, positive)")]
    [BenchmarkCategory("IsBundle")]
    public bool IsBundle_Span_Bundle()
        => SingleFileBundleReader.IsBundle(_bundleBytes, out _);

    /// <summary>Parses the bundle manifest to enumerate every embedded assembly entry.</summary>
    [Benchmark(Description = "ReadManifest")]
    [BenchmarkCategory("ReadManifest")]
    public BundleManifest ReadManifest()
        => SingleFileBundleReader.ReadManifest(_bundlePath, _headerOffset);

    /// <summary>End-to-end: locates the bundle, parses the manifest, and extracts the entry assembly bytes.</summary>
    [Benchmark(Description = "FindEntryAssembly")]
    [BenchmarkCategory("FindEntryAssembly")]
    public (byte[] Bytes, string Name)? FindEntryAssembly()
        => SingleFileBundleReader.FindEntryAssembly(_bundlePath);
}
