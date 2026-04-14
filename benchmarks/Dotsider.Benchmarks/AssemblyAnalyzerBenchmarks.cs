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

    /// <summary>
    /// Locates BCL assemblies, pre-reads CoreLib bytes, builds RichLibrary, and picks a known MethodDef token for resolution.
    /// </summary>
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
    /// <summary>
    /// Drops the analyzer created during each iteration so construction is measured from scratch.
    /// </summary>
    [IterationCleanup]
    public void CleanupConstruction()
    {
        _lastAnalyzer?.Dispose();
        _lastAnalyzer = null;
    }

    // --- Construction ---

    /// <summary>
    /// Measures cold analyzer construction for the ~16MB CoreLib assembly.
    /// </summary>
    [Benchmark(Description = "CoreLib (~16MB)")]
    [BenchmarkCategory("Construction")]
    public AssemblyAnalyzer ConstructCoreLib()
        => _lastAnalyzer = new AssemblyAnalyzer(_coreLibPath);

    /// <summary>
    /// Measures cold analyzer construction for the ~8MB Xml assembly.
    /// </summary>
    [Benchmark(Description = "Xml (~8MB)")]
    [BenchmarkCategory("Construction")]
    public AssemblyAnalyzer ConstructXml()
        => _lastAnalyzer = new AssemblyAnalyzer(_xmlPath);

    /// <summary>
    /// Exercises the byte[] constructor path used when an assembly is extracted from a single-file bundle.
    /// </summary>
    [Benchmark(Description = "CoreLib from byte[] (bundle model)")]
    [BenchmarkCategory("ByteConstructor")]
    public AssemblyAnalyzer ConstructFromBytes_CoreLib()
        => _lastAnalyzer = new AssemblyAnalyzer(
            _coreLibBytes, _coreLibPath,
            sourceBundlePath: "/fake/bundle.exe",
            displayName: "System.Private.CoreLib.dll");

    // --- Existing metadata table enumeration (fresh analyzer per call) ---

    /// <summary>
    /// First-touch enumeration of the CoreLib TypeDef table.
    /// </summary>
    [Benchmark(Description = "CoreLib TypeDefs")]
    [BenchmarkCategory("TypeDefs")]
    public int EnumerateTypeDefsCoreLib()
    {
        using var analyzer = new AssemblyAnalyzer(_coreLibPath);
        return analyzer.TypeDefs.Count;
    }

    /// <summary>
    /// First-touch enumeration of the Xml TypeDef table.
    /// </summary>
    [Benchmark(Description = "Xml TypeDefs")]
    [BenchmarkCategory("TypeDefs")]
    public int EnumerateTypeDefsXml()
    {
        using var analyzer = new AssemblyAnalyzer(_xmlPath);
        return analyzer.TypeDefs.Count;
    }

    /// <summary>
    /// First-touch enumeration of the CoreLib MethodDef table.
    /// </summary>
    [Benchmark(Description = "CoreLib MethodDefs")]
    [BenchmarkCategory("MethodDefs")]
    public int EnumerateMethodDefsCoreLib()
    {
        using var analyzer = new AssemblyAnalyzer(_coreLibPath);
        return analyzer.MethodDefs.Count;
    }

    /// <summary>
    /// First-touch enumeration of the Xml MethodDef table.
    /// </summary>
    [Benchmark(Description = "Xml MethodDefs")]
    [BenchmarkCategory("MethodDefs")]
    public int EnumerateMethodDefsXml()
    {
        using var analyzer = new AssemblyAnalyzer(_xmlPath);
        return analyzer.MethodDefs.Count;
    }

    // --- New lazy-initialized metadata properties (fresh analyzer = first-touch) ---

    /// <summary>
    /// First-touch cost of materializing the AssemblyRef table.
    /// </summary>
    [Benchmark(Description = "CoreLib AssemblyRefs")]
    [BenchmarkCategory("AssemblyRefs")]
    public int AssemblyRefs_CoreLib()
    {
        using var analyzer = new AssemblyAnalyzer(_coreLibPath);
        return analyzer.AssemblyRefs.Count;
    }

    /// <summary>
    /// First-touch cost of materializing the TypeRef table.
    /// </summary>
    [Benchmark(Description = "CoreLib TypeRefs")]
    [BenchmarkCategory("TypeRefs")]
    public int TypeRefs_CoreLib()
    {
        using var analyzer = new AssemblyAnalyzer(_coreLibPath);
        return analyzer.TypeRefs.Count;
    }

    /// <summary>
    /// Exercises MemberRef enumeration, which decodes method and field signatures.
    /// </summary>
    [Benchmark(Description = "CoreLib MemberRefs (signature decoding)")]
    [BenchmarkCategory("MemberRefs")]
    public int MemberRefs_CoreLib()
    {
        using var analyzer = new AssemblyAnalyzer(_coreLibPath);
        return analyzer.MemberRefs.Count;
    }

    /// <summary>
    /// Exercises FieldDef enumeration including field signature decoding.
    /// </summary>
    [Benchmark(Description = "CoreLib FieldDefs (signature decoding)")]
    [BenchmarkCategory("FieldDefs")]
    public int FieldDefs_CoreLib()
    {
        using var analyzer = new AssemblyAnalyzer(_coreLibPath);
        return analyzer.FieldDefs.Count;
    }

    /// <summary>
    /// First-touch cost of materializing the CustomAttribute table.
    /// </summary>
    [Benchmark(Description = "CoreLib CustomAttributes")]
    [BenchmarkCategory("CustomAttributes")]
    public int CustomAttributes_CoreLib()
    {
        using var analyzer = new AssemblyAnalyzer(_coreLibPath);
        return analyzer.CustomAttributes.Count;
    }

    /// <summary>
    /// First-touch cost of materializing embedded manifest resource metadata.
    /// </summary>
    [Benchmark(Description = "CoreLib Resources")]
    [BenchmarkCategory("Resources")]
    public int Resources_CoreLib()
    {
        using var analyzer = new AssemblyAnalyzer(_coreLibPath);
        return analyzer.Resources.Count;
    }

    /// <summary>
    /// First-touch cost of enumerating PE sections.
    /// </summary>
    [Benchmark(Description = "CoreLib Sections")]
    [BenchmarkCategory("Sections")]
    public int Sections_CoreLib()
    {
        using var analyzer = new AssemblyAnalyzer(_coreLibPath);
        return analyzer.Sections.Count;
    }

    // --- Token resolution ---

    /// <summary>
    /// Resolves a MethodDef token to its display name via the token dispatch table.
    /// </summary>
    [Benchmark(Description = "ResolveToken (MethodDef → name)")]
    [BenchmarkCategory("ResolveToken")]
    public string ResolveToken_CoreLib()
    {
        using var analyzer = new AssemblyAnalyzer(_coreLibPath);
        return analyzer.ResolveToken(_resolveTokenTarget);
    }

    // --- Assembly resolution (exercises 6-step probe chain) ---

    /// <summary>
    /// Hits step 2 of the probe chain: CoreLib is found in the running runtime directory.
    /// </summary>
    [Benchmark(Description = "ResolveAssembly step 2 (runtime dir)")]
    [BenchmarkCategory("ResolveAssembly")]
    public object? ResolveAssembly_RuntimeDir()
        => AssemblyAnalyzer.ResolveAssembly(
            _richLibraryDll, "System.Private.CoreLib");

    /// <summary>
    /// Hits step 6 of the probe chain: resolution falls through to a shared ASP.NET runtime pack.
    /// </summary>
    [Benchmark(Description = "ResolveAssembly step 6 (shared framework)")]
    [BenchmarkCategory("ResolveAssembly")]
    public object? ResolveAssembly_SharedFramework()
        => AssemblyAnalyzer.ResolveAssembly(
            _richLibraryDll, "Microsoft.AspNetCore.Routing",
            targetFramework: ".NETCoreApp,Version=v10.0",
            preferredRuntimePack: "Microsoft.AspNetCore.App");

    /// <summary>
    /// Worst case: walks all six probe steps without finding the assembly.
    /// </summary>
    [Benchmark(Description = "ResolveAssembly miss (all 6 steps)")]
    [BenchmarkCategory("ResolveAssembly")]
    public object? ResolveAssembly_Miss()
        => AssemblyAnalyzer.ResolveAssembly(
            _richLibraryDll, "NonExistent.Assembly.That.Does.Not.Exist");
}
