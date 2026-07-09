using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// The auxiliary surfaces the ReadyToRun work lights up over the real fixture: the COR header's
/// <see cref="ClrHeader.ManagedNativeHeader"/> points at the R2R header, the PE "R2R Sections" tab
/// lists the crossgen2 sections, and the Size Map attributes the precompiled native code by method
/// rather than IL bytes.
/// </summary>
[TestClass]
public class ReadyToRunSurfaceTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private const string SkipReason = "ReadyToRun crossgen2 publish did not run on this leg.";

    /// <summary>The COR header's managed-native-header directory points at the R2R header.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ClrHeader_ManagedNativeHeader_IsPopulated()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(Samples.ReadyToRunConsoleDll!);

        Assert.IsNotNull(analyzer.ClrHeader);
        Assert.IsGreaterThan(0, analyzer.ClrHeader!.ManagedNativeHeader.Size, "an R2R image points its managed-native-header at the READYTORUN_HEADER");
    }

    /// <summary>The PE section table lists the crossgen2 sections (RuntimeFunctions, MethodDefEntryPoints).</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ReadyToRunSections_ListCrossgenSections()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(Samples.ReadyToRunConsoleDll!);

        var sections = analyzer.ReadyToRunSections;
        Assert.IsNotEmpty(sections);
        // RuntimeFunctions = 102 and MethodDefEntryPoints = 103 are always present in a normal image.
        Assert.Contains(s => s.SectionId == 102, sections);
        Assert.Contains(s => s.SectionId == 103, sections);
    }

    /// <summary>The Size Map sizes the precompiled native code by method, not IL bytes.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void SizeMap_AttributesPrecompiledNativeCode()
    {
        TestSkip.When(Samples.ReadyToRunConsoleDll is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(Samples.ReadyToRunConsoleDll!);

        var tree = SizeAnalyzer.BuildSizeTree(analyzer);
        Assert.IsGreaterThan(0, tree.Size, "the size tree should carry the precompiled native bytes");

        // The tree attributes native code down to the method: a known type appears as a node whose
        // size accounts for its precompiled methods (never zero for a type with a native body).
        var greeter = FindNode(tree, n => n.Name.Contains("Greeter"));
        Assert.IsNotNull(greeter);
        Assert.IsGreaterThan(0, greeter!.Size);
    }

    private static SizeNode? FindNode(SizeNode node, Func<SizeNode, bool> predicate)
    {
        if (predicate(node)) return node;
        foreach (var child in node.Children)
            if (FindNode(child, predicate) is { } found)
                return found;
        return null;
    }
}
