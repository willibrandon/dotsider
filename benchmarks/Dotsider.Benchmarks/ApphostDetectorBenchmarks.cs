using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Dotsider.Core.Analysis;

namespace Dotsider.Benchmarks;

/// <summary>
/// Benchmarks for <see cref="ApphostDetector"/> against real apphost executables,
/// single-file bundles, and synthetic non-apphost binaries.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class ApphostDetectorBenchmarks
{
    private string _helloWorldExe = null!;
    private string _helloWorldDll = null!;
    private string _dottedNameExe = null!;
    private string _selfContainedExe = null!;
    private string _fakeExePath = null!;
    private string _tempDir = null!;

    /// <summary>Builds real apphost samples, publishes a self-contained bundle, and fabricates a non-apphost exe fixture.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var ext = BenchmarkHelpers.ApphostExtension;

        // Build real apphost samples
        BenchmarkHelpers.BuildSample("samples/HelloWorld");
        _helloWorldExe = BenchmarkHelpers.GetBuildPath("samples/HelloWorld", $"HelloWorld{ext}");
        _helloWorldDll = BenchmarkHelpers.GetBuildPath("samples/HelloWorld", "HelloWorld.dll");

        BenchmarkHelpers.BuildSample("samples/Dotted.Name.App");
        _dottedNameExe = BenchmarkHelpers.GetBuildPath("samples/Dotted.Name.App", $"Dotted.Name.App{ext}");

        // Publish self-contained single-file bundle
        BenchmarkHelpers.PublishSelfContainedSample("samples/SelfContainedConsole");
        _selfContainedExe = BenchmarkHelpers.GetPublishPath("samples/SelfContainedConsole", "SelfContainedConsole");

        // Create fake non-apphost .exe (mirrors ApphostDetectorTests pattern):
        // embeds the DLL name but NOT "hostfxr", so full binary scan returns negative
        _tempDir = Path.Combine(Path.GetTempPath(), $"dotsider-bench-{Guid.NewGuid().ToString("N")[..8]}");
        Directory.CreateDirectory(_tempDir);

        var fakeDllName = "FakeLauncher.dll";
        _fakeExePath = Path.Combine(_tempDir, "FakeLauncher.exe");
        var fakeDllPath = Path.Combine(_tempDir, fakeDllName);

        var exeContent = new byte[512];
        Encoding.UTF8.GetBytes(fakeDllName).CopyTo(exeContent, 64);
        File.WriteAllBytes(_fakeExePath, exeContent);

        // Copy a real managed DLL as companion so the check would pass if hostfxr were present
        File.Copy(_helloWorldDll, fakeDllPath, overwrite: true);

        // Verify critical paths
        if (!File.Exists(_helloWorldExe))
            throw new FileNotFoundException($"HelloWorld apphost not found: {_helloWorldExe}");
        if (!File.Exists(_selfContainedExe))
            throw new FileNotFoundException($"SelfContainedConsole not found: {_selfContainedExe}");
    }

    /// <summary>Deletes the temporary directory holding the fake-apphost fixture.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort */ }
    }

    /// <summary>Measures companion DLL discovery on a real .NET apphost executable.</summary>
    [Benchmark(Description = "Real apphost (HelloWorld)")]
    [BenchmarkCategory("CompanionDll")]
    public string? FindCompanionDll_RealApphost()
        => ApphostDetector.FindCompanionDll(_helloWorldExe);

    /// <summary>Verifies the dotted-name parser correctly extracts the companion DLL from a multi-segment apphost name.</summary>
    [Benchmark(Description = "Dotted-name apphost (Dotted.Name.App)")]
    [BenchmarkCategory("CompanionDll")]
    public string? FindCompanionDll_DottedNameApphost()
        => ApphostDetector.FindCompanionDll(_dottedNameExe);

    /// <summary>Worst case: scans the entire binary because the hostfxr marker is absent, so no companion can be confirmed.</summary>
    [Benchmark(Description = "Fake exe full scan (no hostfxr)")]
    [BenchmarkCategory("CompanionDll")]
    public string? FindCompanionDll_FakeExeFullScan()
        => ApphostDetector.FindCompanionDll(_fakeExePath);

    /// <summary>Baseline: a .dll input takes the early-exit path without any scanning.</summary>
    [Benchmark(Description = ".dll early exit (baseline)")]
    [BenchmarkCategory("CompanionDll")]
    public string? FindCompanionDll_DllEarlyExit()
        => ApphostDetector.FindCompanionDll(_helloWorldDll);

    /// <summary>Positive case: locates the entry assembly inside a real self-contained single-file bundle.</summary>
    [Benchmark(Description = "Real single-file bundle (positive)")]
    [BenchmarkCategory("BundledEntry")]
    public object? FindBundledEntryAssembly_Bundle()
        => ApphostDetector.FindBundledEntryAssembly(_selfContainedExe);

    /// <summary>Negative case: scans a framework-dependent apphost that has no bundle manifest.</summary>
    [Benchmark(Description = "Apphost not a bundle (negative scan)")]
    [BenchmarkCategory("BundledEntry")]
    public object? FindBundledEntryAssembly_Apphost()
        => ApphostDetector.FindBundledEntryAssembly(_helloWorldExe);
}
