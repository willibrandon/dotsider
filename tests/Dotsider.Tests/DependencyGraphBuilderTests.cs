using Dotsider.Core.Analysis;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Dependency Graph Builder.
/// </summary>
[Collection("SampleAssemblies")]
public class DependencyGraphBuilderTests(SampleAssemblyFixture samples)
{
    /// <summary>
    /// Verifies hello world has root node.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void HelloWorld_HasRootNode()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        var (nodes, edges) = DependencyGraphBuilder.Build(a);
        Assert.NotEmpty(nodes);
        Assert.Contains(nodes, n => n.IsRoot);
    }

    /// <summary>
    /// Verifies hello world root node name matches assembly.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void HelloWorld_RootNodeNameMatchesAssembly()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        var (nodes, _) = DependencyGraphBuilder.Build(a);
        var root = nodes.First(n => n.IsRoot);
        Assert.Equal("HelloWorld", root.Name);
    }

    /// <summary>
    /// Verifies rich library has newton soft node.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibrary_HasNewtonSoftNode()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var (nodes, _) = DependencyGraphBuilder.Build(a);
        Assert.Contains(nodes, n => n.Name == "Newtonsoft.Json");
    }

    /// <summary>
    /// Verifies rich library has system text json node.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibrary_HasSystemTextJsonNode()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var (nodes, _) = DependencyGraphBuilder.Build(a);
        Assert.Contains(nodes, n => n.Name == "System.Text.Json");
    }

    /// <summary>
    /// Verifies rich library edge type ref count positive.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibrary_EdgeTypeRefCountPositive()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var (_, edges) = DependencyGraphBuilder.Build(a);
        Assert.NotEmpty(edges);
        Assert.Contains(edges, e => e.TypeRefCount > 0);
    }

    /// <summary>
    /// Verifies minimal api has many nodes.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void MinimalApi_HasManyNodes()
    {
        using var a = new AssemblyAnalyzer(samples.MinimalApiDll);
        var (nodes, _) = DependencyGraphBuilder.Build(a);
        Assert.True(nodes.Count > 1);
    }

    /// <summary>
    /// Verifies empty lib has root and minimal refs.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void EmptyLib_HasRootAndMinimalRefs()
    {
        using var a = new AssemblyAnalyzer(samples.EmptyLibDll);
        var (nodes, _) = DependencyGraphBuilder.Build(a);
        Assert.Contains(nodes, n => n.IsRoot);
    }

    /// <summary>
    /// Verifies all edges reference existing nodes.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void AllEdgesReferenceExistingNodes()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var (nodes, edges) = DependencyGraphBuilder.Build(a);
        var nodeNames = nodes.Select(n => n.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var edge in edges)
        {
            Assert.Contains(edge.SourceName, nodeNames);
            Assert.Contains(edge.TargetName, nodeNames);
        }
    }

    /// <summary>
    /// Verifies no duplicate node names.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NoDuplicateNodeNames()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var (nodes, _) = DependencyGraphBuilder.Build(a);
        var names = nodes.Select(n => n.Name).ToList();
        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    /// <summary>
    /// Verifies only one root node.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void OnlyOneRootNode()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var (nodes, _) = DependencyGraphBuilder.Build(a);
        Assert.Single(nodes, n => n.IsRoot);
    }
}
