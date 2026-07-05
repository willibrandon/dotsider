using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Composite ReadyToRun resolution in both directions, asserted against the real self-contained
/// composite publish. Opened directly, the metadata-less <c>*.r2r.dll</c> resolves its components by
/// name + MVID from the siblings beside it; opened as a component, a DLL routes its native code image
/// to the owner composite. The honest availability states — <c>OwnerCompositeMissing</c> and
/// <c>ComponentMetadataUnavailable</c> — are exercised by relocating a real image away from the
/// siblings it needs, never by fabricating bytes.
/// </summary>
[Collection("SampleAssemblies")]
public class ReadyToRunCompositeTests(SampleAssemblyFixture samples)
{
    private const string SkipReason = "ReadyToRun composite publish did not run on this leg.";

    /// <summary>Opened directly, a composite resolves its components from siblings by name and MVID.</summary>
    [Fact(Timeout = 30_000)]
    public void GlobalComposite_ResolvesComponentsByNameAndMvid()
    {
        Assert.SkipWhen(samples.ReadyToRunCompositeImage is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(samples.ReadyToRunCompositeImage!);

        var info = analyzer.ReadyToRunInfo;
        Assert.NotNull(info);
        Assert.True(info!.IsComposite);
        Assert.True(analyzer.IsReadyToRun);
        // Code lives in the composite itself.
        Assert.Same(analyzer, analyzer.ReadyToRunCodeImage);

        // The app components resolve from siblings; identity is validated by MVID, not just name.
        var lib = analyzer.ReadyToRunComponents.FirstOrDefault(
            c => c.Mvid == samples.ReadyToRunComponentLibMvid);
        Assert.NotNull(lib);
        Assert.True(lib!.MetadataAvailable, "the component beside the composite must resolve by MVID");
        Assert.Equal("ReadyToRunComponentLib.dll", Path.GetFileName(lib.ResolvedPath));

        // A method from that component is named through the resolved metadata and is precompiled.
        var add = analyzer.ReadyToRunMethods.FirstOrDefault(
            m => m.Name == "Add" && m.DeclaringType is not null && m.DeclaringType.Contains("Calculator"));
        Assert.NotNull(add);
        Assert.NotEmpty(add!.CodeRanges);
    }

    /// <summary>A component DLL routes its native code image to the owner composite (a different file).</summary>
    /// <summary>A composite's instantiated generics are attributed to their owning component with names.</summary>
    [Fact(Timeout = 30_000)]
    public void GlobalComposite_GenericInstantiations_AreOwnedByComponents()
    {
        Assert.SkipWhen(samples.ReadyToRunCompositeImage is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(samples.ReadyToRunCompositeImage!);

        // A module override inside the instance signature identifies the owning component; the
        // instantiation resolves there rather than being an unnamed manifest method.
        var owned = analyzer.ReadyToRunMethods
            .Where(m => m.IsGenericInstantiation && m.DeclaringType is not null && m.Mvid != Guid.Empty)
            .ToList();
        Assert.NotEmpty(owned);

        // A component-owned instantiation carries that component's MVID (not the empty manifest MVID).
        var componentMvids = analyzer.ReadyToRunComponents
            .Where(c => c.MetadataAvailable).Select(c => c.Mvid).ToHashSet();
        Assert.Contains(owned, g => componentMvids.Contains(g.Mvid));
    }

    /// <summary>The composite Size Map attributes native code across its components (not a zero tree).</summary>
    [Fact(Timeout = 30_000)]
    public void GlobalComposite_SizeMap_SpansComponents()
    {
        Assert.SkipWhen(samples.ReadyToRunCompositeImage is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(samples.ReadyToRunCompositeImage!);

        // The composite has no own metadata; the size tree must be built from the component-resolved
        // method entries, so it is non-zero and spans more than one assembly subtree.
        var tree = SizeAnalyzer.BuildSizeTree(analyzer);
        Assert.True(tree.Size > 0, "a composite size tree must carry the precompiled native bytes");
        Assert.True(tree.Children.Count > 1, "a composite size tree should span multiple component assemblies");
    }

    /// <summary>A component's precompiled native resolves cross-module call targets through the manifest.</summary>
    [Fact(Timeout = 30_000)]
    public void Component_Native_ResolvesCrossModuleCallTargets()
    {
        Assert.SkipWhen(samples.ReadyToRunCompositeComponent is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(samples.ReadyToRunCompositeComponent!);

        // Program.<Main>$ calls into other components (Console, the component library); the import
        // resolver names those cross-module targets rather than leaving bare addresses.
        var result = ReadyToRunCorrelationQuery.Resolve(
            analyzer, "<Main>$", TestContext.Current.CancellationToken);
        Assert.Equal(ReadyToRunQueryOutcome.Resolved, result.Outcome);
        Assert.Equal(ReadyToRunNativeAvailability.Precompiled, result.Report!.Availability);
        Assert.NotNull(result.Report.NativeText);
        // The disassembly names a call target rather than only raw hex addresses.
        Assert.Contains("WriteLine", result.Report.NativeText);
    }

    /// <summary>A component DLL routes its native code image to the owner composite (a different file).</summary>
    [Fact(Timeout = 30_000)]
    public void Component_RoutesCodeImageToOwnerComposite()
    {
        Assert.SkipWhen(samples.ReadyToRunComponentLibDll is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(samples.ReadyToRunComponentLibDll!);

        var info = analyzer.ReadyToRunInfo;
        Assert.NotNull(info);
        Assert.True(info!.IsComponent);
        Assert.Equal("ReadyToRunComposite.r2r.dll", info.OwnerCompositeExecutable);

        // The native code image is the owner composite — a different file than this component.
        var codeImage = analyzer.ReadyToRunCodeImage;
        Assert.NotNull(codeImage);
        Assert.Equal("ReadyToRunComposite.r2r.dll", Path.GetFileName(codeImage!.FilePath));
        Assert.NotSame(analyzer, codeImage);

        // Its precompiled methods are the ones owned by this component.
        Assert.NotEmpty(analyzer.ReadyToRunMethods);
        Assert.All(analyzer.ReadyToRunMethods,
            m => Assert.Equal(samples.ReadyToRunComponentLibMvid, m.Mvid));
    }

    /// <summary>A component relocated away from its owner composite reports OwnerCompositeMissing, not IL-only.</summary>
    [Fact(Timeout = 30_000)]
    public void Component_WithoutOwnerComposite_IsOwnerCompositeMissing()
    {
        Assert.SkipWhen(samples.ReadyToRunComponentLibDll is null, SkipReason);

        // Relocate the real component DLL away from its owner composite: a genuine "owner missing" case.
        var temp = Path.Combine(Path.GetTempPath(), $"r2r-orphan-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);
        try
        {
            var orphan = Path.Combine(temp, Path.GetFileName(samples.ReadyToRunComponentLibDll!));
            File.Copy(samples.ReadyToRunComponentLibDll!, orphan);

            using var analyzer = new AssemblyAnalyzer(orphan);
            Assert.True(analyzer.ReadyToRunInfo!.IsComponent);
            // No owner composite on disk → no code image, distinct from "not precompiled".
            Assert.Null(analyzer.ReadyToRunCodeImage);

            var result = ReadyToRunCorrelationQuery.Resolve(
                analyzer, "Calculator.Add", TestContext.Current.CancellationToken);
            Assert.Equal(ReadyToRunQueryOutcome.Resolved, result.Outcome);
            Assert.Equal(ReadyToRunNativeAvailability.OwnerCompositeMissing, result.Report!.Availability);
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    /// <summary>A composite relocated away from its siblings reports ComponentMetadataUnavailable, not IL-only.</summary>
    [Fact(Timeout = 30_000)]
    public void Composite_WithoutSiblings_IsComponentMetadataUnavailable()
    {
        Assert.SkipWhen(samples.ReadyToRunCompositeImage is null, SkipReason);

        // Relocate only the composite away from its component siblings: metadata can't be resolved.
        var temp = Path.Combine(Path.GetTempPath(), $"r2r-lonely-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);
        try
        {
            var lonely = Path.Combine(temp, Path.GetFileName(samples.ReadyToRunCompositeImage!));
            File.Copy(samples.ReadyToRunCompositeImage!, lonely);

            using var analyzer = new AssemblyAnalyzer(lonely);
            Assert.True(analyzer.ReadyToRunInfo!.IsComposite);
            // Every component is unresolved without its sibling metadata.
            Assert.NotEmpty(analyzer.ReadyToRunComponents);
            Assert.All(analyzer.ReadyToRunComponents, c => Assert.False(c.MetadataAvailable));

            // A precompiled method still has native code; correlating by its address surfaces the
            // distinct "component metadata unavailable" state rather than a generic IL-only one.
            var withCode = analyzer.ReadyToRunMethods.First(m => m.CodeRanges.Count > 0);
            var address = withCode.CodeRanges[0].VirtualAddress;
            var result = ReadyToRunCorrelationQuery.Resolve(
                analyzer, $"0x{address:x}", TestContext.Current.CancellationToken);
            Assert.Equal(ReadyToRunQueryOutcome.Resolved, result.Outcome);
            Assert.Equal(ReadyToRunNativeAvailability.ComponentMetadataUnavailable, result.Report!.Availability);
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }
}
