using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Dotsider.Core.Analysis;
using Dotsider.Core.Protocol;

namespace Dotsider.Benchmarks;

/// <summary>
/// Cold-path benchmarks for <see cref="DotNetRuntimeLocator"/> that clear
/// both the assembly cache and base-path cache before each iteration,
/// measuring the real cost of runtime discovery from scratch.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class DotNetRuntimeLocatorColdBenchmarks
{
    private string _targetFramework = null!;
    private FrameworkAssemblyInfo? _lastAssemblyResult;
    private string? _lastBasePath;

    /// <summary>
    /// Warms JIT with a throwaway call and records the target framework used by the cold benchmarks.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        // Warm up once so any first-call JIT cost is excluded from setup time.
        _ = DotNetRuntimeLocator.FindAssemblyInSharedFramework("System.Runtime", null);

        _targetFramework = ".NETCoreApp,Version=v10.0";
    }

    /// <summary>
    /// Clears caches before each iteration so first-call probing cost is measured.
    /// </summary>
    [IterationSetup]
    public void ClearBeforeIteration()
    {
        _lastAssemblyResult = null;
        _lastBasePath = null;
        DotNetRuntimeLocator.ClearCache();
    }

    /// <summary>
    /// Cold path: resolves System.Runtime against the shared framework with no cached base path.
    /// </summary>
    [Benchmark(Description = "FindAssembly (cold)")]
    [BenchmarkCategory("ColdPath")]
    public FrameworkAssemblyInfo? FindAssembly_ColdPath()
        => _lastAssemblyResult = DotNetRuntimeLocator.FindAssemblyInSharedFramework(
            "System.Runtime", _targetFramework);

    /// <summary>
    /// Cold path: discovers the .NET install base path from scratch.
    /// </summary>
    [Benchmark(Description = "FindBasePath (cold)")]
    [BenchmarkCategory("ColdPath")]
    public string? FindBasePath_Cold()
        => _lastBasePath = DotNetRuntimeLocator.FindDotNetBasePath();
}

/// <summary>
/// Warm-cache benchmarks for <see cref="DotNetRuntimeLocator"/> that measure
/// the cost of a <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}"/>
/// cache hit, with no per-iteration cache clearing.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class DotNetRuntimeLocatorWarmBenchmarks
{
    private string _targetFramework = null!;

    /// <summary>
    /// Populates the resolver cache so every benchmark invocation is a hit.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        _targetFramework = ".NETCoreApp,Version=v10.0";

        // Populate the cache so every benchmark invocation is a cache hit.
        _ = DotNetRuntimeLocator.FindAssemblyInSharedFramework("System.Runtime", _targetFramework);
    }

    /// <summary>
    /// Warm path: characterizes the concurrent-dictionary cache hit cost.
    /// </summary>
    [Benchmark(Description = "FindAssembly (warm)")]
    [BenchmarkCategory("WarmCache")]
    public FrameworkAssemblyInfo? FindAssembly_WarmCache()
        => DotNetRuntimeLocator.FindAssemblyInSharedFramework("System.Runtime", _targetFramework);
}
