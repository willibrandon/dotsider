using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Dotsider.Core.Analysis;
using System.Runtime.InteropServices;

namespace Dotsider.Benchmarks;

/// <summary>
/// Cold-path benchmarks for <see cref="ImplementationAssemblyResolver"/> that clear
/// the cache before each iteration to measure first-touch resolution cost.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class ImplementationAssemblyResolverColdBenchmarks
{
    private string _coreLibPath = null!;
    private object? _lastResult;

    /// <summary>
    /// Records the CoreLib path used as the referring assembly for each resolution.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        _coreLibPath = Path.Combine(runtimeDir, "System.Private.CoreLib.dll");
    }

    /// <summary>
    /// Clears both resolver caches before each iteration so first-touch cost is measured.
    /// </summary>
    [IterationSetup]
    public void ClearCaches()
    {
        _lastResult = null;
        ImplementationAssemblyResolver.ClearCache();
        DotNetRuntimeLocator.ClearCache();
    }

    /// <summary>
    /// Cold path: redirects System.Runtime to its CoreLib implementation via the known-mappings table.
    /// </summary>
    [Benchmark(Description = "Known mapping cold (System.Runtime → CoreLib)")]
    [BenchmarkCategory("KnownMapping")]
    public object? Resolve_KnownMapping_ColdPath()
        => _lastResult = ImplementationAssemblyResolver.Resolve(
            _coreLibPath, "System.Runtime",
            targetFramework: ".NETCoreApp,Version=v10.0");

    /// <summary>
    /// Cold path: follows an ExportedType forwarder from mscorlib to the real System.Object host.
    /// </summary>
    [Benchmark(Description = "Type forwarder (mscorlib → System.Object)")]
    [BenchmarkCategory("TypeForwarder")]
    public object? Resolve_TypeForwarder()
        => _lastResult = ImplementationAssemblyResolver.Resolve(
            _coreLibPath, "mscorlib",
            declaringType: "System.Object",
            targetFramework: ".NETCoreApp,Version=v10.0");

    /// <summary>
    /// Cold path: the requested assembly is already usable, so resolution short-circuits.
    /// </summary>
    [Benchmark(Description = "Direct usable (System.Private.Xml)")]
    [BenchmarkCategory("Direct")]
    public object? Resolve_DirectUsable()
        => _lastResult = ImplementationAssemblyResolver.Resolve(
            _coreLibPath, "System.Private.Xml",
            targetFramework: ".NETCoreApp,Version=v10.0");
}

/// <summary>
/// Warm-cache benchmarks for <see cref="ImplementationAssemblyResolver"/> that measure
/// <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}.TryGetValue"/>
/// hit cost.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class ImplementationAssemblyResolverWarmBenchmarks
{
    private string _coreLibPath = null!;

    /// <summary>
    /// Warms the resolver cache with a known-mapping lookup so every benchmark invocation is a hit.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        _coreLibPath = Path.Combine(runtimeDir, "System.Private.CoreLib.dll");

        // Warm the cache
        ImplementationAssemblyResolver.Resolve(
            _coreLibPath, "System.Runtime",
            targetFramework: ".NETCoreApp,Version=v10.0");
    }

    /// <summary>
    /// Warm path: characterizes the cache-hit cost for a known-mapping resolution.
    /// </summary>
    [Benchmark(Description = "Known mapping warm cache hit")]
    [BenchmarkCategory("CacheHit")]
    public object? Resolve_KnownMapping_WarmCache()
        => ImplementationAssemblyResolver.Resolve(
            _coreLibPath, "System.Runtime",
            targetFramework: ".NETCoreApp,Version=v10.0");
}
