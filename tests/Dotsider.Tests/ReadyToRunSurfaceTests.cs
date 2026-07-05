using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// The auxiliary surfaces the ReadyToRun work lights up over the real fixture: the COR header's
/// <see cref="ClrHeader.ManagedNativeHeader"/> points at the R2R header, the PE "R2R Sections" tab
/// lists the crossgen2 sections, and the Size Map attributes the precompiled native code by method
/// rather than IL bytes.
/// </summary>
[Collection("SampleAssemblies")]
public class ReadyToRunSurfaceTests(SampleAssemblyFixture samples)
{
    private const string SkipReason = "ReadyToRun crossgen2 publish did not run on this leg.";

    /// <summary>The COR header's managed-native-header directory points at the R2R header.</summary>
    [Fact(Timeout = 30_000)]
    public void ClrHeader_ManagedNativeHeader_IsPopulated()
    {
        Assert.SkipWhen(samples.ReadyToRunConsoleDll is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(samples.ReadyToRunConsoleDll!);

        Assert.NotNull(analyzer.ClrHeader);
        Assert.True(analyzer.ClrHeader!.ManagedNativeHeader.Size > 0,
            "an R2R image points its managed-native-header at the READYTORUN_HEADER");
    }

    /// <summary>The PE section table lists the crossgen2 sections (RuntimeFunctions, MethodDefEntryPoints).</summary>
    [Fact(Timeout = 30_000)]
    public void ReadyToRunSections_ListCrossgenSections()
    {
        Assert.SkipWhen(samples.ReadyToRunConsoleDll is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(samples.ReadyToRunConsoleDll!);

        var sections = analyzer.ReadyToRunSections;
        Assert.NotEmpty(sections);
        // RuntimeFunctions = 102 and MethodDefEntryPoints = 103 are always present in a normal image.
        Assert.Contains(sections, s => s.SectionId == 102);
        Assert.Contains(sections, s => s.SectionId == 103);
    }

    /// <summary>The Size Map sizes the precompiled native code by method, not IL bytes.</summary>
    [Fact(Timeout = 30_000)]
    public void SizeMap_AttributesPrecompiledNativeCode()
    {
        Assert.SkipWhen(samples.ReadyToRunConsoleDll is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(samples.ReadyToRunConsoleDll!);

        var tree = SizeAnalyzer.BuildSizeTree(analyzer);
        Assert.True(tree.Size > 0, "the size tree should carry the precompiled native bytes");

        // The tree attributes native code down to the method: a known type appears as a node whose
        // size accounts for its precompiled methods (never zero for a type with a native body).
        var greeter = FindNode(tree, n => n.Name.Contains("Greeter"));
        Assert.NotNull(greeter);
        Assert.True(greeter!.Size > 0);
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
