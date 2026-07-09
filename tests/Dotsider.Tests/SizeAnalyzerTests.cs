using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Size Analyzer.
/// </summary>
[TestClass]
public class SizeAnalyzerTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    /// <summary>
    /// Verifies rich library root node is assembly.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibrary_RootNodeIsAssembly()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var tree = SizeAnalyzer.BuildSizeTree(a);
        Assert.AreEqual(SizeNodeKind.Assembly, tree.Kind);
    }

    /// <summary>
    /// Verifies rich library has namespace children.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibrary_HasNamespaceChildren()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var tree = SizeAnalyzer.BuildSizeTree(a);
        Assert.IsNotEmpty(tree.Children);
        Assert.Contains(c => c.Kind == SizeNodeKind.Namespace, tree.Children);
    }

    /// <summary>
    /// Verifies rich library namespace has type children.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibrary_NamespaceHasTypeChildren()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var tree = SizeAnalyzer.BuildSizeTree(a);
        var ns = tree.Children.FirstOrDefault(c => c.Kind == SizeNodeKind.Namespace && c.Children.Count > 0);
        Assert.IsNotNull(ns);
        Assert.Contains(c => c.Kind == SizeNodeKind.Type, ns.Children);
    }

    /// <summary>
    /// Verifies rich library type has method children.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibrary_TypeHasMethodChildren()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var tree = SizeAnalyzer.BuildSizeTree(a);
        var type = tree.Children
            .SelectMany(ns => ns.Children)
            .FirstOrDefault(t => t.Kind == SizeNodeKind.Type && t.Children.Count > 0);
        Assert.IsNotNull(type);
        Assert.Contains(c => c.Kind == SizeNodeKind.Method, type.Children);
    }

    /// <summary>
    /// Verifies rich library method sizes positive.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibrary_MethodSizesPositive()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var tree = SizeAnalyzer.BuildSizeTree(a);
        var methods = tree.Children
            .SelectMany(ns => ns.Children)
            .SelectMany(t => t.Children)
            .Where(m => m.Kind == SizeNodeKind.Method)
            .ToList();
        Assert.IsNotEmpty(methods);
        TestAssert.All(methods, m => Assert.IsGreaterThanOrEqualTo(0, m.Size));
        Assert.Contains(m => m.Size > 0, methods);
    }

    /// <summary>
    /// Verifies hello world simpler tree.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void HelloWorld_SimplerTree()
    {
        using var a = new AssemblyAnalyzer(Samples.HelloWorldDll);
        var tree = SizeAnalyzer.BuildSizeTree(a);
        Assert.AreEqual(SizeNodeKind.Assembly, tree.Kind);
        Assert.IsGreaterThan(0, tree.Size);
    }

    /// <summary>
    /// Verifies empty lib minimal tree.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void EmptyLib_MinimalTree()
    {
        using var a = new AssemblyAnalyzer(Samples.EmptyLibDll);
        var tree = SizeAnalyzer.BuildSizeTree(a);
        Assert.AreEqual(SizeNodeKind.Assembly, tree.Kind);
    }

    /// <summary>
    /// Verifies native lib methods with bodies have size.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeLib_MethodsWithBodiesHaveSize()
    {
        using var a = new AssemblyAnalyzer(Samples.NativeLibDll);
        var tree = SizeAnalyzer.BuildSizeTree(a);
        Assert.IsGreaterThan(0, tree.Size);
    }

    /// <summary>
    /// Verifies rich library root size is positive.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void RichLibrary_RootSizeIsPositive()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var tree = SizeAnalyzer.BuildSizeTree(a);
        Assert.IsGreaterThan(0, tree.Size);
    }

    /// <summary>
    /// Verifies complex app has namespaces.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ComplexApp_HasNamespaces()
    {
        using var a = new AssemblyAnalyzer(Samples.ComplexAppDll);
        var tree = SizeAnalyzer.BuildSizeTree(a);
        Assert.IsNotEmpty(tree.Children);
    }

    /// <summary>
    /// Verifies method leaf nodes full path contains token.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void MethodLeafNodes_FullPathContainsToken()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var tree = SizeAnalyzer.BuildSizeTree(a);
        var methods = tree.Children
            .SelectMany(ns => ns.Children)
            .SelectMany(t => t.Children)
            .Where(m => m.Kind == SizeNodeKind.Method)
            .ToList();
        Assert.IsNotEmpty(methods);
        // Every method FullPath should contain :: and @0x for token disambiguation
        TestAssert.All(methods, m =>
        {
            Assert.Contains("::", m.FullPath);
            Assert.Contains("@0x", m.FullPath);
        });
    }

    /// <summary>
    /// Verifies method leaf nodes tokens are unique.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void MethodLeafNodes_TokensAreUnique()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var tree = SizeAnalyzer.BuildSizeTree(a);
        var fullPaths = tree.Children
            .SelectMany(ns => ns.Children)
            .SelectMany(t => t.Children)
            .Where(m => m.Kind == SizeNodeKind.Method)
            .Select(m => m.FullPath)
            .ToList();
        Assert.HasCount(fullPaths.Count, fullPaths.Distinct());
    }

    /// <summary>
    /// Verifies the AOT tree's root holds assembly subtrees beside category buckets when the
    /// mstat sidecar is present.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeAot_RootHasAssemblyAndCategoryChildren()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        using var a = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);
        var tree = SizeAnalyzer.BuildSizeTree(a);

        Assert.AreEqual(SizeNodeKind.Assembly, tree.Kind);
        Assert.IsGreaterThan(0, tree.Size);
        Assert.Contains(c => c.Kind == SizeNodeKind.Assembly && c.Name == "System.Private.CoreLib", tree.Children);
        Assert.Contains(c => c.Kind == SizeNodeKind.Category && c.Name == "Blobs", tree.Children);
        Assert.Contains(c => c.Kind == SizeNodeKind.Category && c.Name == "Frozen Objects", tree.Children);
    }

    /// <summary>
    /// Verifies assembly subtrees nest namespace &gt; type &gt; leaf, and leaves carry the
    /// dependency-graph node name that powers why-chains.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeAot_AssemblySubtreesNestToLeavesWithNodeNames()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        using var a = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);
        var tree = SizeAnalyzer.BuildSizeTree(a);

        var assembly = tree.Children.First(c => c.Kind == SizeNodeKind.Assembly && c.Name == "System.Private.CoreLib");
        var ns = assembly.Children[0];
        Assert.AreEqual(SizeNodeKind.Namespace, ns.Kind);
        var type = ns.Children[0];
        Assert.AreEqual(SizeNodeKind.Type, type.Kind);
        var leaves = type.Children;
        Assert.IsNotEmpty(leaves);
        TestAssert.All(leaves, l => Assert.IsTrue(l.Kind is SizeNodeKind.Method or SizeNodeKind.MethodTable));
        Assert.Contains(l => l.AotNodeName is not null, leaves);
    }

    /// <summary>
    /// Verifies sizes sum exactly at every level of an assembly subtree — the MethodTable
    /// leaf exists precisely so nothing is attributed to a parent without a child to show it.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeAot_SizesSumExactlyThroughTheTree()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        using var a = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);
        var tree = SizeAnalyzer.BuildSizeTree(a);

        Assert.AreEqual(tree.Children.Sum(c => c.Size), tree.Size);
        foreach (var assembly in tree.Children.Where(c => c.Kind == SizeNodeKind.Assembly))
        {
            Assert.AreEqual(assembly.Children.Sum(n => n.Size), assembly.Size);
            foreach (var ns in assembly.Children)
            {
                Assert.AreEqual(ns.Children.Sum(t => t.Size), ns.Size);
                foreach (var type in ns.Children)
                    Assert.AreEqual(type.Children.Sum(m => m.Size), type.Size);
            }
        }
    }

    /// <summary>
    /// Verifies the double-count guard: the blob buckets that 2.1+ re-reports as detail
    /// sections are excluded from the Blobs category when those sections have entries.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeAot_CategoryBucketsExcludeDoubleCountedBlobs()
    {
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "mstat sidecar was not produced");

        using var a = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);
        Assert.IsNotNull(a.Mstat);
        Assert.IsNotEmpty(a.Mstat.FrozenObjects);

        var tree = SizeAnalyzer.BuildSizeTree(a);
        var blobs = tree.Children.First(c => c.Name == "Blobs");

        Assert.DoesNotContain(b => b.Name == "ArrayOfFrozenObjects", blobs.Children);
        Assert.DoesNotContain(b => b.Name == "FieldRvaData", blobs.Children);
        Assert.Contains(c => c.Name == "Frozen Objects", tree.Children);
        Assert.Contains(c => c.Name == "RVA Fields", tree.Children);
    }

    /// <summary>
    /// Verifies an AOT binary copied away from every sidecar still carries a Size Map: the
    /// unwind-data boundaries fill it at symbol fidelity (no IL <see cref="SizeNodeKind.Method"/>
    /// nodes), instead of the pre-symbol empty tree.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void NativeAot_WithoutSidecar_BuildsTreeFromBoundaries()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var dir = Directory.CreateTempSubdirectory("dotsider-sizemap-");
        try
        {
            var exeCopy = Path.Combine(dir.FullName, Path.GetFileName(Samples.NativeAotConsoleExe!));
            File.Copy(Samples.NativeAotConsoleExe!, exeCopy);
            using var a = new AssemblyAnalyzer(exeCopy);

            var tree = SizeAnalyzer.BuildSizeTree(a);

            Assert.IsGreaterThan(0, tree.Size);
            Assert.IsNotEmpty(tree.Children);
            Assert.DoesNotContain(c => c.Kind == SizeNodeKind.Method, tree.Children);
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
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Managed_TreeCarriesNoAotNodeNames()
    {
        using var a = new AssemblyAnalyzer(Samples.RichLibraryDll);
        var tree = SizeAnalyzer.BuildSizeTree(a);

        var all = new List<SizeNode>();
        void Walk(SizeNode n) { all.Add(n); foreach (var c in n.Children) Walk(c); }
        Walk(tree);

        TestAssert.All(all, n => Assert.IsNull(n.AotNodeName));
    }
}
