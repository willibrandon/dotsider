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
[TestClass]
public class ReadyToRunCompositeTests
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private const string SkipReason = "ReadyToRun composite publish did not run on this leg.";

    /// <summary>Opened directly, a composite resolves its components from siblings by name and MVID.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void GlobalComposite_ResolvesComponentsByNameAndMvid()
    {
        TestSkip.When(Samples.ReadyToRunCompositeImage is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(Samples.ReadyToRunCompositeImage!);

        var info = analyzer.ReadyToRunInfo;
        Assert.IsNotNull(info);
        Assert.IsTrue(info!.IsComposite);
        Assert.IsTrue(analyzer.IsReadyToRun);
        // Code lives in the composite itself.
        Assert.AreSame(analyzer, analyzer.ReadyToRunCodeImage);

        // The app components resolve from siblings; identity is validated by MVID, not just name.
        var lib = analyzer.ReadyToRunComponents.FirstOrDefault(
            c => c.Mvid == Samples.ReadyToRunComponentLibMvid);
        Assert.IsNotNull(lib);
        Assert.IsTrue(lib!.MetadataAvailable, "the component beside the composite must resolve by MVID");
        Assert.AreEqual("ReadyToRunComponentLib.dll", Path.GetFileName(lib.ResolvedPath));

        // A method from that component is named through the resolved metadata and is precompiled.
        var add = analyzer.ReadyToRunMethods.FirstOrDefault(
            m => m.Name == "Add" && m.DeclaringType is not null && m.DeclaringType.Contains("Calculator"));
        Assert.IsNotNull(add);
        Assert.IsNotEmpty(add!.CodeRanges);
    }

    /// <summary>A component DLL routes its native code image to the owner composite (a different file).</summary>
    /// <summary>A composite's instantiated generics are attributed to their owning component with names.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void GlobalComposite_GenericInstantiations_AreOwnedByComponents()
    {
        TestSkip.When(Samples.ReadyToRunCompositeImage is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(Samples.ReadyToRunCompositeImage!);

        // A module override inside the instance signature identifies the owning component; the
        // instantiation resolves there rather than being an unnamed manifest method.
        var owned = analyzer.ReadyToRunMethods
            .Where(m => m.IsGenericInstantiation && m.DeclaringType is not null && m.Mvid != Guid.Empty)
            .ToList();
        Assert.IsNotEmpty(owned);

        // A component-owned instantiation carries that component's MVID (not the empty manifest MVID).
        var componentMvids = analyzer.ReadyToRunComponents
            .Where(c => c.MetadataAvailable).Select(c => c.Mvid).ToHashSet();
        Assert.Contains(g => componentMvids.Contains(g.Mvid), owned);
    }

    /// <summary>The composite Size Map attributes native code across its components (not a zero tree).</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void GlobalComposite_SizeMap_SpansComponents()
    {
        TestSkip.When(Samples.ReadyToRunCompositeImage is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(Samples.ReadyToRunCompositeImage!);

        // The composite has no own metadata; the size tree must be built from the component-resolved
        // method entries, so it is non-zero and spans more than one assembly subtree.
        var tree = SizeAnalyzer.BuildSizeTree(analyzer);
        Assert.IsGreaterThan(0, tree.Size, "a composite size tree must carry the precompiled native bytes");
        Assert.IsGreaterThan(1, tree.Children.Count, "a composite size tree should span multiple component assemblies");
    }

    /// <summary>A component's precompiled native resolves cross-module call targets through the manifest.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Component_Native_ResolvesCrossModuleCallTargets()
    {
        TestSkip.When(Samples.ReadyToRunCompositeComponent is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(Samples.ReadyToRunCompositeComponent!);

        // Program.<Main>$ calls into other components (Console, the component library); the import
        // resolver names those cross-module targets rather than leaving bare addresses.
        var result = ReadyToRunCorrelationQuery.Resolve(
            analyzer, "<Main>$", CancellationToken.None);
        Assert.AreEqual(ReadyToRunQueryOutcome.Resolved, result.Outcome);
        Assert.AreEqual(ReadyToRunNativeAvailability.Precompiled, result.Report!.Availability);
        Assert.IsNotNull(result.Report.NativeText);
        // Both framework and application-component targets are resolved through their exact module
        // identities rather than the component analyzer's unrelated metadata fallback.
        Assert.Contains("Console.WriteLine", result.Report.NativeText);
        Assert.Contains("Calculator.Add", result.Report.NativeText);
        Assert.Contains("Calculator.Multiply", result.Report.NativeText);
    }

    /// <summary>A component DLL routes its native code image to the owner composite (a different file).</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Component_RoutesCodeImageToOwnerComposite()
    {
        TestSkip.When(Samples.ReadyToRunComponentLibDll is null, SkipReason);
        using var analyzer = new AssemblyAnalyzer(Samples.ReadyToRunComponentLibDll!);

        var info = analyzer.ReadyToRunInfo;
        Assert.IsNotNull(info);
        Assert.IsTrue(info!.IsComponent);
        Assert.AreEqual("ReadyToRunComposite.r2r.dll", info.OwnerCompositeExecutable);

        // The native code image is the owner composite — a different file than this component.
        var codeImage = analyzer.ReadyToRunCodeImage;
        Assert.IsNotNull(codeImage);
        Assert.AreEqual("ReadyToRunComposite.r2r.dll", Path.GetFileName(codeImage!.FilePath));
        Assert.AreNotSame(analyzer, codeImage);

        // Its precompiled methods are the ones owned by this component.
        Assert.IsNotEmpty(analyzer.ReadyToRunMethods);
        TestAssert.All(analyzer.ReadyToRunMethods,
            m => Assert.AreEqual(Samples.ReadyToRunComponentLibMvid, m.Mvid));
    }

    /// <summary>A component relocated away from its owner composite reports OwnerCompositeMissing, not IL-only.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Component_WithoutOwnerComposite_IsOwnerCompositeMissing()
    {
        TestSkip.When(Samples.ReadyToRunComponentLibDll is null, SkipReason);

        // Relocate the real component DLL away from its owner composite: a genuine "owner missing" case.
        var temp = Path.Combine(Path.GetTempPath(), $"r2r-orphan-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);
        try
        {
            var orphan = Path.Combine(temp, Path.GetFileName(Samples.ReadyToRunComponentLibDll!));
            File.Copy(Samples.ReadyToRunComponentLibDll!, orphan);

            using var analyzer = new AssemblyAnalyzer(orphan);
            Assert.IsTrue(analyzer.ReadyToRunInfo!.IsComponent);
            // No owner composite on disk → no code image, distinct from "not precompiled".
            Assert.IsNull(analyzer.ReadyToRunCodeImage);

            var result = ReadyToRunCorrelationQuery.Resolve(
                analyzer, "Calculator.Add", CancellationToken.None);
            Assert.AreEqual(ReadyToRunQueryOutcome.Resolved, result.Outcome);
            Assert.AreEqual(ReadyToRunNativeAvailability.OwnerCompositeMissing, result.Report!.Availability);
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    /// <summary>A composite relocated away from its siblings reports ComponentMetadataUnavailable, not IL-only.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Composite_WithoutSiblings_IsComponentMetadataUnavailable()
    {
        TestSkip.When(Samples.ReadyToRunCompositeImage is null, SkipReason);

        // Relocate only the composite away from its component siblings: metadata can't be resolved.
        var temp = Path.Combine(Path.GetTempPath(), $"r2r-lonely-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);
        try
        {
            var lonely = Path.Combine(temp, Path.GetFileName(Samples.ReadyToRunCompositeImage!));
            File.Copy(Samples.ReadyToRunCompositeImage!, lonely);

            using var analyzer = new AssemblyAnalyzer(lonely);
            Assert.IsTrue(analyzer.ReadyToRunInfo!.IsComposite);
            // Every component is unresolved without its sibling metadata.
            Assert.IsNotEmpty(analyzer.ReadyToRunComponents);
            TestAssert.All(analyzer.ReadyToRunComponents, c => Assert.IsFalse(c.MetadataAvailable));

            // A precompiled method still has native code; correlating by its address surfaces the
            // distinct "component metadata unavailable" state rather than a generic IL-only one.
            var withCode = analyzer.ReadyToRunMethods.First(m => m.CodeRanges.Count > 0);
            var address = withCode.CodeRanges[0].VirtualAddress;
            var result = ReadyToRunCorrelationQuery.Resolve(
                analyzer, $"0x{address:x}", CancellationToken.None);
            Assert.AreEqual(ReadyToRunQueryOutcome.Resolved, result.Outcome);
            Assert.AreEqual(ReadyToRunNativeAvailability.ComponentMetadataUnavailable, result.Report!.Availability);
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }
}
