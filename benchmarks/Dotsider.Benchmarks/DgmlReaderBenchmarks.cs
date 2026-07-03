using BenchmarkDotNet.Attributes;
using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Benchmarks;

/// <summary>
/// Benchmarks for <see cref="DgmlReader"/> and <see cref="DgmlGraph.PathToRoot(string)"/>
/// against the real dependency graphs the NativeAotConsole sample publishes. The codegen
/// graph is the smaller of the two; the scan graph roughly doubles the node count.
/// </summary>
[MemoryDiagnoser]
public class DgmlReaderBenchmarks
{
    private string _codegenPath = null!;
    private string _scanPath = null!;
    private DgmlGraph _graph = null!;
    private string _deepLabel = null!;

    /// <summary>
    /// Publishes the Native AOT sample (cached across benchmark classes), locates the DGML
    /// sidecars, and picks a deep method node for the chain query.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        BenchmarkHelpers.PublishNativeAotSample("samples/NativeAotConsole");
        var exe = BenchmarkHelpers.GetPublishPath("samples/NativeAotConsole", "NativeAotConsole");
        var dir = Path.GetDirectoryName(exe)!;
        _codegenPath = Path.Combine(dir, "NativeAotConsole.codegen.dgml.xml");
        _scanPath = Path.Combine(dir, "NativeAotConsole.scan.dgml.xml");
        if (!File.Exists(_codegenPath) || !File.Exists(_scanPath))
            throw new InvalidOperationException($"DGML sidecars not found next to {exe}");

        _graph = DgmlReader.Read(_codegenPath)
            ?? throw new InvalidOperationException("codegen DGML failed to parse");

        // The last compiled method is as far from the roots as the report gets.
        var mstat = MstatReader.Read(Path.Combine(dir, "NativeAotConsole.mstat"))
            ?? throw new InvalidOperationException("mstat failed to parse");
        _deepLabel = mstat.Methods.Last(m => m.NodeName is not null && _graph.FindNodeByLabel(m.NodeName) is not null).NodeName!;
    }

    /// <summary>Streams and indexes the codegen dependency graph.</summary>
    /// <returns>The parsed graph, returned so the JIT cannot elide the work.</returns>
    [Benchmark(Description = "Read codegen DGML")]
    public DgmlGraph? ReadCodegen() => DgmlReader.Read(_codegenPath);

    /// <summary>Streams and indexes the larger scan dependency graph.</summary>
    /// <returns>The parsed graph, returned so the JIT cannot elide the work.</returns>
    [Benchmark(Description = "Read scan DGML")]
    public DgmlGraph? ReadScan() => DgmlReader.Read(_scanPath);

    /// <summary>Walks a deep method node back to a dependency root.</summary>
    /// <returns>The chain, returned so the JIT cannot elide the work.</returns>
    [Benchmark(Description = "PathToRoot on a deep method node")]
    public IReadOnlyList<DgmlPathStep> PathToRootDeepNode() => _graph.PathToRoot(_deepLabel);
}
