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

    /// <summary>
    /// Verifies the AOT tree's root holds assembly subtrees beside category buckets when the
    /// mstat sidecar is present.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NativeAot_RootHasAssemblyAndCategoryChildren()
    {
        Assert.SkipWhen(samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        using var a = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);
        var tree = SizeAnalyzer.BuildSizeTree(a);

        Assert.Equal(SizeNodeKind.Assembly, tree.Kind);
        Assert.True(tree.Size > 0);
        Assert.Contains(tree.Children, c => c.Kind == SizeNodeKind.Assembly && c.Name == "System.Private.CoreLib");
        Assert.Contains(tree.Children, c => c.Kind == SizeNodeKind.Category && c.Name == "Blobs");
        Assert.Contains(tree.Children, c => c.Kind == SizeNodeKind.Category && c.Name == "Frozen Objects");
    }

    /// <summary>
    /// Verifies assembly subtrees nest namespace &gt; type &gt; leaf, and leaves carry the
    /// dependency-graph node name that powers why-chains.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NativeAot_AssemblySubtreesNestToLeavesWithNodeNames()
    {
        Assert.SkipWhen(samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        using var a = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);
        var tree = SizeAnalyzer.BuildSizeTree(a);

        var assembly = tree.Children.First(c => c.Kind == SizeNodeKind.Assembly && c.Name == "System.Private.CoreLib");
        var ns = assembly.Children[0];
        Assert.Equal(SizeNodeKind.Namespace, ns.Kind);
        var type = ns.Children[0];
        Assert.Equal(SizeNodeKind.Type, type.Kind);
        var leaves = type.Children;
        Assert.NotEmpty(leaves);
        Assert.All(leaves, l => Assert.True(l.Kind is SizeNodeKind.Method or SizeNodeKind.MethodTable));
        Assert.Contains(leaves, l => l.AotNodeName is not null);
    }

    /// <summary>
    /// Verifies sizes sum exactly at every level of an assembly subtree — the MethodTable
    /// leaf exists precisely so nothing is attributed to a parent without a child to show it.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NativeAot_SizesSumExactlyThroughTheTree()
    {
        Assert.SkipWhen(samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        using var a = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);
        var tree = SizeAnalyzer.BuildSizeTree(a);

        Assert.Equal(tree.Children.Sum(c => c.Size), tree.Size);
        foreach (var assembly in tree.Children.Where(c => c.Kind == SizeNodeKind.Assembly))
        {
            Assert.Equal(assembly.Children.Sum(n => n.Size), assembly.Size);
            foreach (var ns in assembly.Children)
            {
                Assert.Equal(ns.Children.Sum(t => t.Size), ns.Size);
                foreach (var type in ns.Children)
                    Assert.Equal(type.Children.Sum(m => m.Size), type.Size);
            }
        }
    }

    /// <summary>
    /// Verifies the double-count guard: the blob buckets that 2.1+ re-reports as detail
    /// sections are excluded from the Blobs category when those sections have entries.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NativeAot_CategoryBucketsExcludeDoubleCountedBlobs()
    {
        Assert.SkipWhen(samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        using var a = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);
        Assert.NotNull(a.Mstat);
        Assert.NotEmpty(a.Mstat.FrozenObjects);

        var tree = SizeAnalyzer.BuildSizeTree(a);
        var blobs = tree.Children.First(c => c.Name == "Blobs");

        Assert.DoesNotContain(blobs.Children, b => b.Name == "ArrayOfFrozenObjects");
        Assert.DoesNotContain(blobs.Children, b => b.Name == "FieldRvaData");
        Assert.Contains(tree.Children, c => c.Name == "Frozen Objects");
        Assert.Contains(tree.Children, c => c.Name == "RVA Fields");
    }

    /// <summary>
    /// Verifies an AOT binary without an mstat sidecar falls back to the metadata path: an
    /// empty tree rather than a throw.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void NativeAot_WithoutSidecar_FallsBackToEmptyTree()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var dir = Directory.CreateTempSubdirectory("dotsider-sizemap-");
        try
        {
            var exeCopy = Path.Combine(dir.FullName, Path.GetFileName(samples.NativeAotConsoleExe!));
            File.Copy(samples.NativeAotConsoleExe!, exeCopy);
            using var a = new AssemblyAnalyzer(exeCopy);

            var tree = SizeAnalyzer.BuildSizeTree(a);

            Assert.Equal(0, tree.Size);
            Assert.Empty(tree.Children);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Verifies managed trees are unchanged by the AOT additions: no node carries an AOT
    /// node name, so serialized output stays identical.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void Managed_TreeCarriesNoAotNodeNames()
    {
        using var a = new AssemblyAnalyzer(samples.RichLibraryDll);
        var tree = SizeAnalyzer.BuildSizeTree(a);

        var all = new List<SizeNode>();
        void Walk(SizeNode n) { all.Add(n); foreach (var c in n.Children) Walk(c); }
        Walk(tree);

        Assert.All(all, n => Assert.Null(n.AotNodeName));
    }
}
