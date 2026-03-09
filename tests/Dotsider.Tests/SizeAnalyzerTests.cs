using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

[Collection("SampleAssemblies")]
public class SizeAnalyzerTests(SampleAssemblyFixture samples)
{
    [Fact(Timeout = 5_000)]
    public void RichLibrary_RootNodeIsAssembly()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var disasm = new IlDisassembler(a);
        var tree = SizeAnalyzer.BuildSizeTree(a, disasm);
        Assert.Equal(SizeNodeKind.Assembly, tree.Kind);
    }

    [Fact(Timeout = 5_000)]
    public void RichLibrary_HasNamespaceChildren()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var disasm = new IlDisassembler(a);
        var tree = SizeAnalyzer.BuildSizeTree(a, disasm);
        Assert.NotEmpty(tree.Children);
        Assert.Contains(tree.Children, c => c.Kind == SizeNodeKind.Namespace);
    }

    [Fact(Timeout = 5_000)]
    public void RichLibrary_NamespaceHasTypeChildren()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var disasm = new IlDisassembler(a);
        var tree = SizeAnalyzer.BuildSizeTree(a, disasm);
        var ns = tree.Children.FirstOrDefault(c => c.Kind == SizeNodeKind.Namespace && c.Children.Count > 0);
        Assert.NotNull(ns);
        Assert.Contains(ns.Children, c => c.Kind == SizeNodeKind.Type);
    }

    [Fact(Timeout = 5_000)]
    public void RichLibrary_TypeHasMethodChildren()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var disasm = new IlDisassembler(a);
        var tree = SizeAnalyzer.BuildSizeTree(a, disasm);
        var type = tree.Children
            .SelectMany(ns => ns.Children)
            .FirstOrDefault(t => t.Kind == SizeNodeKind.Type && t.Children.Count > 0);
        Assert.NotNull(type);
        Assert.Contains(type.Children, c => c.Kind == SizeNodeKind.Method);
    }

    [Fact(Timeout = 5_000)]
    public void RichLibrary_MethodSizesPositive()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var disasm = new IlDisassembler(a);
        var tree = SizeAnalyzer.BuildSizeTree(a, disasm);
        var methods = tree.Children
            .SelectMany(ns => ns.Children)
            .SelectMany(t => t.Children)
            .Where(m => m.Kind == SizeNodeKind.Method)
            .ToList();
        Assert.NotEmpty(methods);
        Assert.All(methods, m => Assert.True(m.Size >= 0));
        Assert.Contains(methods, m => m.Size > 0);
    }

    [Fact(Timeout = 5_000)]
    public void HelloWorld_SimplerTree()
    {
        using var a = new AssemblyAnalyzer(samples.HelloWorldDll);
        var disasm = new IlDisassembler(a);
        var tree = SizeAnalyzer.BuildSizeTree(a, disasm);
        Assert.Equal(SizeNodeKind.Assembly, tree.Kind);
        Assert.True(tree.Size > 0);
    }

    [Fact(Timeout = 5_000)]
    public void EmptyLib_MinimalTree()
    {
        using var a = new AssemblyAnalyzer(samples.EmptyLibDll);
        var disasm = new IlDisassembler(a);
        var tree = SizeAnalyzer.BuildSizeTree(a, disasm);
        Assert.Equal(SizeNodeKind.Assembly, tree.Kind);
    }

    [Fact(Timeout = 5_000)]
    public void NativeLib_MethodsWithBodiesHaveSize()
    {
        using var a = new AssemblyAnalyzer(samples.NativeLibDll);
        var disasm = new IlDisassembler(a);
        var tree = SizeAnalyzer.BuildSizeTree(a, disasm);
        Assert.True(tree.Size > 0);
    }

    [Fact(Timeout = 5_000)]
    public void RichLibrary_RootSizeIsPositive()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var disasm = new IlDisassembler(a);
        var tree = SizeAnalyzer.BuildSizeTree(a, disasm);
        Assert.True(tree.Size > 0);
    }

    [Fact(Timeout = 5_000)]
    public void ComplexApp_HasNamespaces()
    {
        using var a = new AssemblyAnalyzer(samples.ComplexAppDll);
        var disasm = new IlDisassembler(a);
        var tree = SizeAnalyzer.BuildSizeTree(a, disasm);
        Assert.NotEmpty(tree.Children);
    }

    [Fact(Timeout = 5_000)]
    public void MethodLeafNodes_FullPathContainsToken()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var disasm = new IlDisassembler(a);
        var tree = SizeAnalyzer.BuildSizeTree(a, disasm);
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

    [Fact(Timeout = 5_000)]
    public void MethodLeafNodes_TokensAreUnique()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var disasm = new IlDisassembler(a);
        var tree = SizeAnalyzer.BuildSizeTree(a, disasm);
        var fullPaths = tree.Children
            .SelectMany(ns => ns.Children)
            .SelectMany(t => t.Children)
            .Where(m => m.Kind == SizeNodeKind.Method)
            .Select(m => m.FullPath)
            .ToList();
        Assert.Equal(fullPaths.Count, fullPaths.Distinct().Count());
    }
}
