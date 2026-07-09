using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the symbol-driven Size Map branch — <see cref="SizeAnalyzer.BuildFromSymbols"/> and
/// its precedence behind the mstat report — with synthetic merged symbols on every platform and
/// the real NativeAOT fixture where its symbol file exists.
/// </summary>
[TestClass]
public class SizeAnalyzerSymbolTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private static NativeSymbol Symbol(
        string name, ulong va, long size, NativeSymbolKind kind,
        string? managedName = null, bool exact = false) =>
        new(name, managedName, va, null, null, null, size, kind, null, null, exact, []);

    private static NativeSymbolInfo Info(params NativeSymbol[] symbols) =>
        new(symbols, NativeSymbolSource.NativePdb, NativeSymbolStatus.Loaded, "x.pdb", null);

    private static SizeNode? Find(SizeNode node, string name) =>
        node.Name == name ? node : node.Children.Select(c => Find(c, name)).FirstOrDefault(n => n is not null);

    /// <summary>
    /// Verifies joined functions land under assembly &gt; namespace &gt; type with
    /// <see cref="SizeNodeKind.Function"/> leaves, including a dotted method name
    /// (<c>.ctor</c>) split at the recovered type boundary.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BuildFromSymbols_JoinedFunctions_GroupByAssemblyNamespaceType()
    {
        var recovered = new RecoveredType[]
        {
            new("System.Foo", ["Bar", ".ctor"], "TestAsm"),
            new("Ns.Outer+Inner", ["Run"], "OtherAsm"),
        };
        var tree = SizeAnalyzer.BuildFromSymbols("app", recovered, Info(
            Symbol("TestAsm_System_Foo__Bar", 0x1000, 0x40, NativeSymbolKind.Function, "System.Foo.Bar", exact: true),
            Symbol("TestAsm_System_Foo___ctor", 0x1040, 0x10, NativeSymbolKind.Function, "System.Foo..ctor", exact: true),
            Symbol("OtherAsm_Ns_Outer_Inner__Run", 0x1050, 0x20, NativeSymbolKind.Function, "Ns.Outer+Inner.Run", exact: true)));

        var assembly = Find(tree, "TestAsm");
        Assert.IsNotNull(assembly);
        Assert.AreEqual(SizeNodeKind.Assembly, assembly.Kind);
        Assert.AreEqual(0x50, assembly.Size);

        var ns = Assert.ContainsSingle(assembly.Children);
        Assert.AreEqual("System", ns.Name);
        Assert.AreEqual(SizeNodeKind.Namespace, ns.Kind);

        var type = Assert.ContainsSingle(ns.Children);
        Assert.AreEqual("Foo", type.Name);
        Assert.AreEqual(SizeNodeKind.Type, type.Kind);
        Assert.HasCount(2, type.Children);
        TestAssert.All(type.Children, m => Assert.AreEqual(SizeNodeKind.Function, m.Kind));
        Assert.Contains(m => m.Name == ".ctor" && m.Size == 0x10, type.Children);

        var nested = Find(tree, "Outer+Inner");
        Assert.IsNotNull(nested);
        Assert.AreEqual("Ns", Find(tree, "OtherAsm")!.Children.Single().Name);
    }

    /// <summary>
    /// Verifies the categories: unjoined names in Runtime, boundaries in Unattributed, and each
    /// data kind under its own category with the matching node kind.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BuildFromSymbols_Categories_CarryEachKind()
    {
        var tree = SizeAnalyzer.BuildFromSymbols("app", [], Info(
            Symbol("RhpAssignRef", 0x1000, 0x30, NativeSymbolKind.Function),
            Symbol("sub_2000", 0x2000, 0x20, NativeSymbolKind.Boundary),
            Symbol("_ZTV6Widget", 0x3000, 0x18, NativeSymbolKind.MethodTable, "Widget (MethodTable)"),
            Symbol("__Str_abc", 0x3100, 0x10, NativeSymbolKind.FrozenObject),
            Symbol("__unbox_Widget", 0x3200, 0x08, NativeSymbolKind.Stub),
            Symbol("__GenericDict_List", 0x3300, 0x28, NativeSymbolKind.GenericDictionary),
            Symbol("__GCSTATICS_App", 0x3400, 0x38, NativeSymbolKind.Statics),
            Symbol("__readonlydata_x", 0x3500, 0x48, NativeSymbolKind.Data)));

        void AssertCategory(string name, long size, SizeNodeKind childKind)
        {
            var category = Find(tree, name);
            Assert.IsNotNull(category);
            Assert.AreEqual(SizeNodeKind.Category, category.Kind);
            Assert.AreEqual(size, category.Size);
            TestAssert.All(category.Children, c => Assert.AreEqual(childKind, c.Kind));
        }

        AssertCategory("Runtime", 0x30, SizeNodeKind.Function);
        AssertCategory("Unattributed", 0x20, SizeNodeKind.Function);
        AssertCategory("MethodTables", 0x18, SizeNodeKind.MethodTable);
        AssertCategory("Frozen Objects", 0x10, SizeNodeKind.FrozenObject);
        AssertCategory("Stubs", 0x08, SizeNodeKind.Function);
        AssertCategory("Generic Dictionaries", 0x28, SizeNodeKind.Blob);
        AssertCategory("Statics", 0x38, SizeNodeKind.Blob);
        AssertCategory("Data", 0x48, SizeNodeKind.Blob);

        Assert.AreEqual("Widget (MethodTable)", Find(tree, "MethodTables")!.Children.Single().Name);
    }

    /// <summary>
    /// Verifies no byte is counted twice: the root sums every sized merged symbol exactly once,
    /// and zero-size symbols contribute nothing.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BuildFromSymbols_RootSumsEachSymbolOnce()
    {
        var recovered = new RecoveredType[] { new("System.Foo", ["Bar"], "TestAsm") };
        var symbols = new[]
        {
            Symbol("a", 0x1000, 0x40, NativeSymbolKind.Function, "System.Foo.Bar", exact: true),
            Symbol("b", 0x2000, 0x30, NativeSymbolKind.Function),
            Symbol("c", 0x3000, 0x20, NativeSymbolKind.MethodTable),
            Symbol("d", 0x4000, 0, NativeSymbolKind.Function), // unsized: excluded
        };

        var tree = SizeAnalyzer.BuildFromSymbols("app", recovered, Info(symbols));

        Assert.AreEqual(0x90, tree.Size);
        Assert.AreEqual(tree.Size, tree.Children.Sum(c => c.Size));
    }

    /// <summary>
    /// Verifies the real fixture: with the mstat hidden, the Size Map builds from symbols
    /// (Function leaves, no IL <see cref="SizeNodeKind.Method"/> nodes), and with the mstat
    /// present it keeps precedence (Method leaves appear).
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void BuildSizeTree_MstatPrecedence_SymbolsCarryTheTreeWithoutIt()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");
        TestSkip.When(Samples.NativeAotConsoleMstat is null, "mstat not present");
        TestSkip.When(Samples.NativeAotConsoleSymbols is null, "native symbols not present");

        // mstat beside the exe: the report wins.
        using (var withMstat = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!))
        {
            var tree = SizeAnalyzer.BuildSizeTree(withMstat);
            Assert.Contains(n => n.Kind == SizeNodeKind.Method, Flatten(tree));
        }

        // Exe and symbols copied away from the mstat: symbols carry the tree.
        var dir = Directory.CreateTempSubdirectory("dotsider-sizesym-");
        try
        {
            var exeCopy = Path.Combine(dir.FullName, Path.GetFileName(Samples.NativeAotConsoleExe!));
            File.Copy(Samples.NativeAotConsoleExe!, exeCopy);
            CopySymbolsBeside(Samples.NativeAotConsoleExe!, Samples.NativeAotConsoleSymbols!, dir.FullName);

            using var analyzer = new AssemblyAnalyzer(exeCopy);
            Assert.IsNull(analyzer.Mstat);
            var tree = SizeAnalyzer.BuildSizeTree(analyzer);

            var nodes = Flatten(tree).ToList();
            Assert.Contains(n => n.Kind == SizeNodeKind.Function, nodes);
            Assert.DoesNotContain(n => n.Kind == SizeNodeKind.Method, nodes);
            Assert.IsGreaterThan(0, tree.Size);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    private static IEnumerable<SizeNode> Flatten(SizeNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var descendant in Flatten(child))
                yield return descendant;
        }
    }

    /// <summary>Copies the platform's symbol artifact beside a relocated exe.</summary>
    private static void CopySymbolsBeside(string exePath, string symbolsPath, string targetDir)
    {
        var exeDir = Path.GetDirectoryName(exePath)!;
        if (symbolsPath.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)
            || symbolsPath.EndsWith(".dbg", StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(symbolsPath, Path.Combine(targetDir, Path.GetFileName(symbolsPath)));
            return;
        }

        // macOS: recreate the dSYM bundle's DWARF path relative to the exe.
        var relative = Path.GetRelativePath(exeDir, symbolsPath);
        var target = Path.Combine(targetDir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(symbolsPath, target);
    }
}
