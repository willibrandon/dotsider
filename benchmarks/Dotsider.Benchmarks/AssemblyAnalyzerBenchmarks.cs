using System.Runtime.InteropServices;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Dotsider.Core.Analysis;

namespace Dotsider.Benchmarks;

/// <summary>
/// Benchmarks for <see cref="AssemblyAnalyzer"/> construction, lazy-initialized metadata
/// properties (first-touch enumeration), token resolution, assembly resolution, and
/// the <c>byte[]</c> constructor used for bundle-extracted assemblies.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class AssemblyAnalyzerBenchmarks
{
    private string _coreLibPath = null!;
    private string _xmlPath = null!;
    private byte[] _coreLibBytes = null!;
    private string _richLibraryDll = null!;
    private int _resolveTokenTarget;
    private AssemblyAnalyzer? _lastAnalyzer;

    [GlobalSetup]
    public void Setup()
    {
        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        _coreLibPath = Path.Combine(runtimeDir, "System.Private.CoreLib.dll");
        _xmlPath = Path.Combine(runtimeDir, "System.Private.Xml.dll");

        if (!File.Exists(_coreLibPath))
            throw new FileNotFoundException($"BCL assembly not found: {_coreLibPath}");
        if (!File.Exists(_xmlPath))
            throw new FileNotFoundException($"BCL assembly not found: {_xmlPath}");

        // Pre-load CoreLib bytes for the byte[] constructor benchmark
        _coreLibBytes = File.ReadAllBytes(_coreLibPath);

        // Build RichLibrary for ResolveAssembly benchmarks — its bin dir won't
        // contain BCL assemblies app-local, forcing deeper probe steps
        BenchmarkHelpers.BuildSample("samples/RichLibrary");
        _richLibraryDll = BenchmarkHelpers.GetBuildPath("samples/RichLibrary", "RichLibrary.dll");

        // Find a known MethodDef token for ResolveToken benchmark
        using var tempAnalyzer = new AssemblyAnalyzer(_coreLibPath);
        var firstMethod = tempAnalyzer.MethodDefs.FirstOrDefault(m => m.Name == "ToString");
        _resolveTokenTarget = firstMethod?.Token ?? tempAnalyzer.MethodDefs[0].Token;
    }

    // IterationCleanup with no explicit InvocationCount/UnrollFactor
    // gives 1 invocation per iteration. Do not add those attributes to
    // Construction benchmarks without switching to a list-based cleanup.
    [IterationCleanup]
    public void CleanupConstruction()
    {
        _lastAnalyzer?.Dispose();
        _lastAnalyzer = null;
    }

    // --- Construction ---

    [Benchmark(Description = "CoreLib (~16MB)")]
    [BenchmarkCategory("Construction")]
    public AssemblyAnalyzer ConstructCoreLib()
        => _lastAnalyzer = new AssemblyAnalyzer(_coreLibPath);

    [Benchmark(Description = "Xml (~8MB)")]
    [BenchmarkCategory("Construction")]
    public AssemblyAnalyzer ConstructXml()
        => _lastAnalyzer = new AssemblyAnalyzer(_xmlPath);

    [Benchmark(Description = "CoreLib from byte[] (bundle model)")]
    [BenchmarkCategory("ByteConstructor")]
    public AssemblyAnalyzer ConstructFromBytes_CoreLib()
        => _lastAnalyzer = new AssemblyAnalyzer(
            _coreLibBytes, _coreLibPath,
            sourceBundlePath: "/fake/bundle.exe",
            displayName: "System.Private.CoreLib.dll");

    // --- Existing metadata table enumeration (fresh analyzer per call) ---

    [Benchmark(Description = "CoreLib TypeDefs")]
    [BenchmarkCategory("TypeDefs")]
    public int EnumerateTypeDefsCoreLib()
    {
        using var analyzer = new AssemblyAnalyzer(_coreLibPath);
        return analyzer.TypeDefs.Count;
    }

    [Benchmark(Description = "Xml TypeDefs")]
    [BenchmarkCategory("TypeDefs")]
    public int EnumerateTypeDefsXml()
    {
        using var analyzer = new AssemblyAnalyzer(_xmlPath);
        return analyzer.TypeDefs.Count;
    }

    [Benchmark(Description = "CoreLib MethodDefs")]
    [BenchmarkCategory("MethodDefs")]
    public int EnumerateMethodDefsCoreLib()
    {
        using var analyzer = new AssemblyAnalyzer(_coreLibPath);
        return analyzer.MethodDefs.Count;
    }

    [Benchmark(Description = "Xml MethodDefs")]
    [BenchmarkCategory("MethodDefs")]
    public int EnumerateMethodDefsXml()
    {
        using var analyzer = new AssemblyAnalyzer(_xmlPath);
        return analyzer.MethodDefs.Count;
    }

    // --- New lazy-initialized metadata properties (fresh analyzer = first-touch) ---

    [Benchmark(Description = "CoreLib AssemblyRefs")]
    [BenchmarkCategory("AssemblyRefs")]
    public int AssemblyRefs_CoreLib()
    {
        using var analyzer = new AssemblyAnalyzer(_coreLibPath);
        return analyzer.AssemblyRefs.Count;
    }

    [Benchmark(Description = "CoreLib TypeRefs")]
    [BenchmarkCategory("TypeRefs")]
    public int TypeRefs_CoreLib()
    {
        using var analyzer = new AssemblyAnalyzer(_coreLibPath);
        return analyzer.TypeRefs.Count;
    }

    [Benchmark(Description = "CoreLib MemberRefs (signature decoding)")]
    [BenchmarkCategory("MemberRefs")]
    public int MemberRefs_CoreLib()
    {
        using var analyzer = new AssemblyAnalyzer(_coreLibPath);
        return analyzer.MemberRefs.Count;
    }

    [Benchmark(Description = "CoreLib FieldDefs (signature decoding)")]
    [BenchmarkCategory("FieldDefs")]
    public int FieldDefs_CoreLib()
    {
        using var analyzer = new AssemblyAnalyzer(_coreLibPath);
        return analyzer.FieldDefs.Count;
    }

    [Benchmark(Description = "CoreLib CustomAttributes")]
    [BenchmarkCategory("CustomAttributes")]
    public int CustomAttributes_CoreLib()
    {
        using var analyzer = new AssemblyAnalyzer(_coreLibPath);
        return analyzer.CustomAttributes.Count;
    }

    [Benchmark(Description = "CoreLib Resources")]
    [BenchmarkCategory("Resources")]
    public int Resources_CoreLib()
    {
        using var analyzer = new AssemblyAnalyzer(_coreLibPath);
        return analyzer.Resources.Count;
    }

    [Benchmark(Description = "CoreLib Sections")]
    [BenchmarkCategory("Sections")]
    public int Sections_CoreLib()
    {
        using var analyzer = new AssemblyAnalyzer(_coreLibPath);
        return analyzer.Sections.Count;
    }

    // --- Token resolution ---

    [Benchmark(Description = "ResolveToken (MethodDef → name)")]
    [BenchmarkCategory("ResolveToken")]
    public string ResolveToken_CoreLib()
    {
        using var analyzer = new AssemblyAnalyzer(_coreLibPath);
        return analyzer.ResolveToken(_resolveTokenTarget);
    }

    // --- Assembly resolution (exercises 6-step probe chain) ---

    [Benchmark(Description = "ResolveAssembly step 2 (runtime dir)")]
    [BenchmarkCategory("ResolveAssembly")]
    public object? ResolveAssembly_RuntimeDir()
        => AssemblyAnalyzer.ResolveAssembly(
            _richLibraryDll, "System.Private.CoreLib");

    [Benchmark(Description = "ResolveAssembly step 6 (shared framework)")]
    [BenchmarkCategory("ResolveAssembly")]
    public object? ResolveAssembly_SharedFramework()
        => AssemblyAnalyzer.ResolveAssembly(
            _richLibraryDll, "Microsoft.AspNetCore.Routing",
            targetFramework: ".NETCoreApp,Version=v10.0",
            preferredRuntimePack: "Microsoft.AspNetCore.App");

    [Benchmark(Description = "ResolveAssembly miss (all 6 steps)")]
    [BenchmarkCategory("ResolveAssembly")]
    public object? ResolveAssembly_Miss()
        => AssemblyAnalyzer.ResolveAssembly(
            _richLibraryDll, "NonExistent.Assembly.That.Does.Not.Exist");
}
