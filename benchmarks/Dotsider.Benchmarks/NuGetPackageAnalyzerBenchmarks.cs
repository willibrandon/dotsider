using System.IO.Compression;
using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using Dotsider.Analysis;

namespace Dotsider.Benchmarks;

/// <summary>
/// Benchmarks for <see cref="NuGetPackageAnalyzer"/> construction and DLL extraction.
/// Creates a synthetic .nupkg from a BCL assembly in GlobalSetup.
/// </summary>
[MemoryDiagnoser]
public class NuGetPackageAnalyzerBenchmarks
{
    private string _nupkgPath = null!;
    private string _tempDir = null!;
    private NuGetPackageAnalyzer? _lastAnalyzer;

    [GlobalSetup]
    public void Setup()
    {
        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        var coreLibPath = Path.Combine(runtimeDir, "System.Private.CoreLib.dll");
        var xmlPath = Path.Combine(runtimeDir, "System.Private.Xml.dll");

        _tempDir = Path.Combine(Path.GetTempPath(), "dotsider-bench-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _nupkgPath = Path.Combine(_tempDir, "BenchPackage.1.0.0.nupkg");

        using var zip = ZipFile.Open(_nupkgPath, ZipArchiveMode.Create);

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

    [GlobalCleanup]
    public void Cleanup()
    {
        _lastAnalyzer?.Dispose();
        try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true); }
        catch { /* best effort */ }
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _lastAnalyzer?.Dispose();
        _lastAnalyzer = null;
    }

    [Benchmark(Description = "Construction (2 DLLs, ~24MB)")]
    public NuGetPackageAnalyzer Construct()
        => _lastAnalyzer = new NuGetPackageAnalyzer(_nupkgPath);

    [Benchmark(Description = "OpenDll (CoreLib ~16MB)")]
    public AssemblyAnalyzer OpenDll()
    {
        using var pkg = new NuGetPackageAnalyzer(_nupkgPath);
        var dll = pkg.DllFiles.First(f => f.Name.Contains("CoreLib"));
        var analyzer = pkg.OpenDll(dll);
        analyzer.Dispose();
        return analyzer;
    }
}
