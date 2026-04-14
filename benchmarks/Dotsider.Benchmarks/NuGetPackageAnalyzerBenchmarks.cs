using System.IO.Compression;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Dotsider.Core.Analysis;

namespace Dotsider.Benchmarks;

/// <summary>
/// Benchmarks for <see cref="NuGetPackageAnalyzer"/> construction and DLL extraction.
/// Creates synthetic .nupkg packages from BCL assemblies in GlobalSetup — a standard
/// package (2 DLLs) and a large package (120+ entries) to test enumeration at scale.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class NuGetPackageAnalyzerBenchmarks
{
    private string _nupkgPath = null!;
    private string _largeNupkgPath = null!;
    private string _tempDir = null!;
    private NuGetPackageAnalyzer? _lastAnalyzer;

    /// <summary>
    /// Synthesizes a standard .nupkg and a 120+ entry large .nupkg so both shapes can be measured.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        var coreLibPath = Path.Combine(runtimeDir, "System.Private.CoreLib.dll");
        var xmlPath = Path.Combine(runtimeDir, "System.Private.Xml.dll");

        _tempDir = Path.Combine(Path.GetTempPath(), "dotsider-bench-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);

        // --- Standard package (2 DLLs, ~24MB) ---
        _nupkgPath = Path.Combine(_tempDir, "BenchPackage.1.0.0.nupkg");

        using (var zip = ZipFile.Open(_nupkgPath, ZipArchiveMode.Create))
        {
            var nuspecEntry = zip.CreateEntry("BenchPackage.nuspec");
            using (var writer = new StreamWriter(nuspecEntry.Open()))
            {
                writer.Write("""
                    <?xml version="1.0" encoding="utf-8"?>
                    <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
                      <metadata>
                        <id>BenchPackage</id>
                        <version>1.0.0</version>
                        <authors>bench</authors>
                        <description>Benchmark synthetic package</description>
                      </metadata>
                    </package>
                    """);
            }

            zip.CreateEntryFromFile(coreLibPath, "lib/net10.0/System.Private.CoreLib.dll");
            zip.CreateEntryFromFile(xmlPath, "lib/net10.0/System.Private.Xml.dll");
        }

        // --- Large package (120+ entries: 2 real DLLs + 100+ filler files) ---
        _largeNupkgPath = Path.Combine(_tempDir, "LargePackage.1.0.0.nupkg");

        using (var zip = ZipFile.Open(_largeNupkgPath, ZipArchiveMode.Create))
        {
            var nuspecEntry = zip.CreateEntry("LargePackage.nuspec");
            using (var writer = new StreamWriter(nuspecEntry.Open()))
            {
                writer.Write("""
                    <?xml version="1.0" encoding="utf-8"?>
                    <package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd">
                      <metadata>
                        <id>LargePackage</id>
                        <version>1.0.0</version>
                        <authors>bench</authors>
                        <description>Large benchmark package with 120+ entries</description>
                      </metadata>
                    </package>
                    """);
            }

            // 2 real managed DLLs
            zip.CreateEntryFromFile(coreLibPath, "lib/net10.0/System.Private.CoreLib.dll");
            zip.CreateEntryFromFile(xmlPath, "lib/net10.0/System.Private.Xml.dll");

            // 100+ filler files across typical NuGet package directories
            string[] dirs = ["ref/net10.0", "build", "buildTransitive", "tools", "content", "analyzers/dotnet/cs"];
            string[] extensions = [".xml", ".json", ".txt", ".pdb", ".props", ".targets"];
            var fillerContent = new byte[256];

            for (var i = 0; i < 100; i++)
            {
                var dir = dirs[i % dirs.Length];
                var ext = extensions[i % extensions.Length];
                var entry = zip.CreateEntry($"{dir}/File{i:D3}{ext}");
                using var stream = entry.Open();
                stream.Write(fillerContent);
            }
        }
    }

    /// <summary>
    /// Disposes the last analyzer and removes the synthetic package temp directory.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _lastAnalyzer?.Dispose();
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort */ }
    }

    /// <summary>
    /// Drops the per-iteration analyzer so construction is measured from scratch.
    /// </summary>
    [IterationCleanup]
    public void IterationCleanup()
    {
        _lastAnalyzer?.Dispose();
        _lastAnalyzer = null;
    }

    // --- Standard package ---

    /// <summary>
    /// Measures package open plus manifest read on a standard two-DLL .nupkg.
    /// </summary>
    [Benchmark(Description = "Construction (2 DLLs, ~24MB)")]
    [BenchmarkCategory("Standard")]
    public NuGetPackageAnalyzer Construct()
        => _lastAnalyzer = new NuGetPackageAnalyzer(_nupkgPath);

    /// <summary>
    /// Measures extracting and analyzing the CoreLib DLL from inside a standard package.
    /// </summary>
    [Benchmark(Description = "OpenDll (CoreLib ~16MB)")]
    [BenchmarkCategory("Standard")]
    public AssemblyAnalyzer OpenDll()
    {
        using var pkg = new NuGetPackageAnalyzer(_nupkgPath);
        var dll = pkg.DllFiles.First(f => f.Name.Contains("CoreLib"));
        var analyzer = pkg.OpenDll(dll);
        analyzer.Dispose();
        return analyzer;
    }

    // --- Large package (120+ entries) ---

    /// <summary>
    /// Measures construction on a package with 120+ zip entries to characterize enumeration at scale.
    /// </summary>
    [Benchmark(Description = "Construction (120+ entries)")]
    [BenchmarkCategory("LargePackage")]
    public NuGetPackageAnalyzer Construct_LargePackage()
        => _lastAnalyzer = new NuGetPackageAnalyzer(_largeNupkgPath);

    /// <summary>
    /// Measures extracting and analyzing CoreLib from a package with many unrelated entries.
    /// </summary>
    [Benchmark(Description = "OpenDll from large package (CoreLib)")]
    [BenchmarkCategory("LargePackage")]
    public AssemblyAnalyzer OpenDll_LargePackage()
    {
        using var pkg = new NuGetPackageAnalyzer(_largeNupkgPath);
        var dll = pkg.DllFiles.First(f => f.Name.Contains("CoreLib"));
        var analyzer = pkg.OpenDll(dll);
        analyzer.Dispose();
        return analyzer;
    }
}
