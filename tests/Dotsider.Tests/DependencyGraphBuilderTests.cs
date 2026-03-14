using Dotsider.Core.Analysis;

namespace Dotsider.Tests;

[Collection("SampleAssemblies")]
public class DependencyGraphBuilderTests(SampleAssemblyFixture samples)
{
    [Fact(Timeout = 30_000)]
    public void HelloWorld_HasRootNode()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        var (nodes, edges) = DependencyGraphBuilder.Build(a);
        Assert.NotEmpty(nodes);
        Assert.Contains(nodes, n => n.IsRoot);
    }

    [Fact(Timeout = 30_000)]
    public void HelloWorld_RootNodeNameMatchesAssembly()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        var (nodes, _) = DependencyGraphBuilder.Build(a);
        var root = nodes.First(n => n.IsRoot);
        Assert.Equal("HelloWorld", root.Name);
    }

    [Fact(Timeout = 30_000)]
    public void RichLibrary_HasNewtonSoftNode()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var (nodes, _) = DependencyGraphBuilder.Build(a);
        Assert.Contains(nodes, n => n.Name == "Newtonsoft.Json");
    }

    [Fact(Timeout = 30_000)]
    public void RichLibrary_HasSystemTextJsonNode()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var (nodes, _) = DependencyGraphBuilder.Build(a);
        Assert.Contains(nodes, n => n.Name == "System.Text.Json");
    }

    [Fact(Timeout = 30_000)]
    public void RichLibrary_EdgeTypeRefCountPositive()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var (_, edges) = DependencyGraphBuilder.Build(a);
        Assert.NotEmpty(edges);
        Assert.Contains(edges, e => e.TypeRefCount > 0);
    }

    [Fact(Timeout = 30_000)]
    public void MinimalApi_HasManyNodes()
    {
        using var a = new AssemblyAnalyzer(samples.MinimalApiDll);
        var (nodes, _) = DependencyGraphBuilder.Build(a);
        Assert.True(nodes.Count > 1);
    }

    [Fact(Timeout = 30_000)]
    public void EmptyLib_HasRootAndMinimalRefs()
    {
        using var a = new AssemblyAnalyzer(samples.EmptyLibDll);
        var (nodes, _) = DependencyGraphBuilder.Build(a);
        Assert.Contains(nodes, n => n.IsRoot);
    }

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

    [Fact(Timeout = 30_000)]
    public void NoDuplicateNodeNames()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var (nodes, _) = DependencyGraphBuilder.Build(a);
        var names = nodes.Select(n => n.Name).ToList();
        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact(Timeout = 30_000)]
    public void OnlyOneRootNode()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var (nodes, _) = DependencyGraphBuilder.Build(a);
        Assert.Single(nodes, n => n.IsRoot);
    }
}
