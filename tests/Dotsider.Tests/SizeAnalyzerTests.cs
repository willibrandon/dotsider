using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Size Analyzer.
/// </summary>
[Collection("SampleAssemblies")]
public class SizeAnalyzerTests(SampleAssemblyFixture samples)
{
    /// <summary>
    /// Verifies rich library root node is assembly.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibrary_RootNodeIsAssembly()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var tree = SizeAnalyzer.BuildSizeTree(a);
        Assert.Equal(SizeNodeKind.Assembly, tree.Kind);
    }

    /// <summary>
    /// Verifies rich library has namespace children.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibrary_HasNamespaceChildren()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var tree = SizeAnalyzer.BuildSizeTree(a);
        Assert.NotEmpty(tree.Children);
        Assert.Contains(tree.Children, c => c.Kind == SizeNodeKind.Namespace);
    }

    /// <summary>
    /// Verifies rich library namespace has type children.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibrary_NamespaceHasTypeChildren()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var tree = SizeAnalyzer.BuildSizeTree(a);
        var ns = tree.Children.FirstOrDefault(c => c.Kind == SizeNodeKind.Namespace && c.Children.Count > 0);
        Assert.NotNull(ns);
        Assert.Contains(ns.Children, c => c.Kind == SizeNodeKind.Type);
    }

    /// <summary>
    /// Verifies rich library type has method children.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibrary_TypeHasMethodChildren()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var tree = SizeAnalyzer.BuildSizeTree(a);
        var type = tree.Children
            .SelectMany(ns => ns.Children)
            .FirstOrDefault(t => t.Kind == SizeNodeKind.Type && t.Children.Count > 0);
        Assert.NotNull(type);
        Assert.Contains(type.Children, c => c.Kind == SizeNodeKind.Method);
    }

    /// <summary>
    /// Verifies rich library method sizes positive.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibrary_MethodSizesPositive()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var tree = SizeAnalyzer.BuildSizeTree(a);
        var methods = tree.Children
            .SelectMany(ns => ns.Children)
            .SelectMany(t => t.Children)
            .Where(m => m.Kind == SizeNodeKind.Method)
            .ToList();
        Assert.NotEmpty(methods);
        Assert.All(methods, m => Assert.True(m.Size >= 0));
        Assert.Contains(methods, m => m.Size > 0);
    }

    /// <summary>
    /// Verifies hello world simpler tree.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void HelloWorld_SimplerTree()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        var tree = SizeAnalyzer.BuildSizeTree(a);
        Assert.Equal(SizeNodeKind.Assembly, tree.Kind);
        Assert.True(tree.Size > 0);
    }

    /// <summary>
    /// Verifies empty lib minimal tree.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void EmptyLib_MinimalTree()
    {
        using var a = new AssemblyAnalyzer(samples.EmptyLibDll);
        var tree = SizeAnalyzer.BuildSizeTree(a);
        Assert.Equal(SizeNodeKind.Assembly, tree.Kind);
    }

    /// <summary>
    /// Verifies native lib methods with bodies have size.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NativeLib_MethodsWithBodiesHaveSize()
    {
        using var a = new AssemblyAnalyzer(samples.NativeLibDll);
        var tree = SizeAnalyzer.BuildSizeTree(a);
        Assert.True(tree.Size > 0);
    }

    /// <summary>
    /// Verifies rich library root size is positive.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RichLibrary_RootSizeIsPositive()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var tree = SizeAnalyzer.BuildSizeTree(a);
        Assert.True(tree.Size > 0);
    }

    /// <summary>
    /// Verifies complex app has namespaces.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ComplexApp_HasNamespaces()
    {
        using var a = new AssemblyAnalyzer(samples.ComplexAppDll);
        var tree = SizeAnalyzer.BuildSizeTree(a);
        Assert.NotEmpty(tree.Children);
    }

    /// <summary>
    /// Verifies method leaf nodes full path contains token.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void MethodLeafNodes_FullPathContainsToken()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var tree = SizeAnalyzer.BuildSizeTree(a);
        var methods = tree.Children
            .SelectMany(ns => ns.Children)
            .SelectMany(t => t.Children)
            .Where(m => m.Kind == SizeNodeKind.Method)
            .ToList();
        Assert.NotEmpty(methods);
        // Every method FullPath should contain :: and @0x for token disambiguation
        Assert.All(methods, m =>
        {
            Assert.Contains("::", m.FullPath);
            Assert.Contains("@0x", m.FullPath);
        });
    }

    /// <summary>
    /// Verifies method leaf nodes tokens are unique.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void MethodLeafNodes_TokensAreUnique()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var tree = SizeAnalyzer.BuildSizeTree(a);
        var fullPaths = tree.Children
            .SelectMany(ns => ns.Children)
            .SelectMany(t => t.Children)
            .Where(m => m.Kind == SizeNodeKind.Method)
            .Select(m => m.FullPath)
            .ToList();
        Assert.Equal(fullPaths.Count, fullPaths.Distinct().Count());
    }
}
