using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the analyzer-level pre-ILC surface: probe gating, companion attach/detach
/// lifecycle, generation-guarded index builds, sidecar path fallbacks, and correlation
/// against the real fixture.
/// </summary>
[TestClass]
public class PreIlcAnalyzerTests : IDisposable
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private readonly List<string> _tempFiles = [];

    private string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"dotsider-preilc-an-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempFiles.Add(dir);
        return dir;
    }

    /// <summary>Verifies the probe is gated on the Native AOT binary kind.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void PreIlcSidecars_ManagedAssemblyAndApphost_Null()
    {
        using var managed = new AssemblyAnalyzer(Samples.HelloWorldDll);
        Assert.IsNull(managed.PreIlcSidecars);
        Assert.IsNull(managed.AttachPreIlcCompanions());

        using var apphost = new AssemblyAnalyzer(Samples.HelloWorldExe);
        Assert.IsNull(apphost.PreIlcSidecars);
    }

    /// <summary>Verifies the fixture AOT exe probes its full sidecar set.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void PreIlcSidecars_FixtureAotExe_Found()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        using var analyzer = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);
        var sidecars = analyzer.PreIlcSidecars;

        Assert.IsNotNull(sidecars);
        Assert.IsTrue(sidecars!.HasAttachableCompanion);
        Assert.AreEqual(PreIlcAssemblyOrigin.IlcResponseFile, sidecars.Origin);
    }

    /// <summary>Verifies attach is idempotent and the set carries readable metadata.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void AttachPreIlcCompanions_Idempotent()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        using var analyzer = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);
        var set = analyzer.AttachPreIlcCompanions();

        Assert.IsNotNull(set);
        Assert.AreSame(set, analyzer.AttachPreIlcCompanions());
        Assert.AreSame(set, analyzer.PreIlcCompanions);
        Assert.IsTrue(set!.Root.HasMetadata);
        Assert.AreEqual("NativeAotConsole", set.Root.AssemblyName);
        Assert.AreSame(set.Root, set.All[0]);
    }

    /// <summary>Verifies attach returns null when nothing attachable was probed.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void AttachPreIlcCompanions_NoSidecars_Null()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var dir = NewTempDir();
        var exe = Path.Combine(dir, "NativeAotConsole.exe");
        File.Copy(Samples.NativeAotConsoleExe!, exe);

        using var analyzer = new AssemblyAnalyzer(exe);
        Assert.IsNull(analyzer.PreIlcSidecars);
        Assert.IsNull(analyzer.AttachPreIlcCompanions());
        Assert.IsNull(analyzer.ManagedNativeIndex);
    }

    /// <summary>Verifies detach disposes the companions and drops the index.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void DetachPreIlcCompanions_DisposesSetAndDropsIndex()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        using var analyzer = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);
        var set = analyzer.AttachPreIlcCompanions();
        Assert.IsNotNull(set);

        analyzer.DetachPreIlcCompanions();

        Assert.IsNull(analyzer.PreIlcCompanions);
        Assert.IsNull(analyzer.ManagedNativeIndex);
        Assert.ThrowsExactly<ObjectDisposedException>(() => set!.Root.TypeDefs);
    }

    /// <summary>Verifies disposing the owner disposes the attached companions transitively.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Dispose_DisposesAttachedCompanions()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var analyzer = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);
        var set = analyzer.AttachPreIlcCompanions();
        Assert.IsNotNull(set);

        analyzer.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => set!.Root.MethodDefs);
    }

    /// <summary>Verifies the index is null before attach and builds once after.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ManagedNativeIndex_BuildsAfterAttach()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        using var analyzer = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);
        Assert.IsNull(analyzer.ManagedNativeIndex);

        analyzer.AttachPreIlcCompanions();
        var index = analyzer.ManagedNativeIndex;

        Assert.IsNotNull(index);
        Assert.AreSame(index, analyzer.ManagedNativeIndex);
        Assert.IsGreaterThan(0, index!.Methods.Count);
    }

    /// <summary>Verifies a stale build after detach never publishes; the reader never throws while the analyzer lives.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ManagedNativeIndex_RacingDetach_NeverPublishesStale()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        using var analyzer = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);

        var reader = Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
                _ = analyzer.ManagedNativeIndex;
        }, cts.Token);

        for (var i = 0; i < 5; i++)
        {
            analyzer.AttachPreIlcCompanions();
            await Task.Delay(50, CancellationToken.None);
            analyzer.DetachPreIlcCompanions();
        }

        await cts.CancelAsync();
        await reader;

        Assert.IsNull(analyzer.PreIlcCompanions);
        Assert.IsNull(analyzer.ManagedNativeIndex);
    }

    /// <summary>Verifies a build racing the owner's dispose abandons cleanly (returns or ODE only).</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ManagedNativeIndex_RacingDispose_AbandonsCleanly()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var analyzer = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);
        analyzer.AttachPreIlcCompanions();

        var build = Task.Run(() =>
        {
            try { _ = analyzer.ManagedNativeIndex; }
            catch (ObjectDisposedException) { /* documented outcome */ }
        }, CancellationToken.None);

        analyzer.Dispose();
        await build;
    }

    /// <summary>Verifies MstatPath/DgmlPath fall back to the obj tree, sibling wins for mstat, codegen-first for DGML.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void MstatAndDgmlPaths_ObjTreeFallbackAndCodegenPrecedence()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var root = NewTempDir();
        var exeDir = Path.Combine(root, "Proj", "bin", "Release", "net10.0", "win-x64", "publish");
        Directory.CreateDirectory(exeDir);
        var nativeDir = Path.Combine(root, "Proj", "obj", "Release", "net10.0", "win-x64", "native");
        Directory.CreateDirectory(nativeDir);

        var exe = Path.Combine(exeDir, "NativeAotConsole.exe");
        File.Copy(Samples.NativeAotConsoleExe!, exe);

        var objMstat = Path.Combine(nativeDir, "NativeAotConsole.mstat");
        File.WriteAllBytes(objMstat, [1]);
        File.WriteAllBytes(Path.Combine(exeDir, "NativeAotConsole.scan.dgml.xml"), [1]);
        var objCodegen = Path.Combine(nativeDir, "NativeAotConsole.codegen.dgml.xml");
        File.WriteAllBytes(objCodegen, [1]);

        using var analyzer = new AssemblyAnalyzer(exe);
        Assert.AreEqual(objMstat, analyzer.MstatPath);
        Assert.AreEqual(objCodegen, analyzer.DgmlPath); // codegen-first across locations

        var siblingMstat = Path.Combine(exeDir, "NativeAotConsole.mstat");
        File.WriteAllBytes(siblingMstat, [1]);
        using var analyzer2 = new AssemblyAnalyzer(exe);
        Assert.AreEqual(siblingMstat, analyzer2.MstatPath);
    }

    /// <summary>Verifies sidecar stems strip library extensions: a Native AOT .dll finds its mstat.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void FindSidecar_LibraryStem_FindsMstat()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var dir = NewTempDir();
        var lib = Path.Combine(dir, "SomeAotLib.dll");
        File.Copy(Samples.NativeAotConsoleExe!, lib); // AOT by content, library by name
        var mstat = Path.Combine(dir, "SomeAotLib.mstat");
        File.WriteAllBytes(mstat, [1]);

        using var analyzer = new AssemblyAnalyzer(lib);
        Assert.AreEqual(BinaryKind.NativeAot, analyzer.BinaryKind);
        Assert.AreEqual(mstat, analyzer.MstatPath);
    }

    /// <summary>Verifies the fixture Greeter correlations end to end, including ctor and accessor.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void ManagedNativeIndex_FixtureGreeterCorrelations()
    {
        TestSkip.When(Samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        using var analyzer = new AssemblyAnalyzer(Samples.NativeAotConsoleExe!);
        analyzer.AttachPreIlcCompanions();
        var index = analyzer.ManagedNativeIndex;
        Assert.IsNotNull(index);

        var greeter = index!.Methods.Where(m => m.Method.DeclaringType == "Greeter").ToList();
        Assert.IsNotEmpty(greeter);

        var describe = Assert.ContainsSingle(m => m.Method.Name == "Describe", greeter);
        Assert.AreEqual(MethodCorrelationStatus.CorrelatedExact, describe.Status);
        Assert.IsGreaterThan(0, describe.NativeSize);

        var greets = greeter.Where(m => m.Method.Name == "Greet").ToList();
        Assert.HasCount(2, greets);
        TestAssert.All(greets, g => Assert.AreEqual(MethodCorrelationStatus.CorrelatedAmbiguous, g.Status));
        TestAssert.All(greets, g => Assert.AreEqual(0, g.NativeSize));
        Assert.IsGreaterThan(0, greets[0].SharedCandidateSize);

        var ctor = Assert.ContainsSingle(m => m.Method.Name == ".ctor", greeter);
        Assert.AreNotEqual(MethodCorrelationStatus.NotInNativeImage, ctor.Status);

        var getName = Assert.ContainsSingle(m => m.Method.Name == "get_Name", greeter);
        Assert.AreNotEqual(MethodCorrelationStatus.NotInNativeImage, getName.Status);

        var never = Assert.ContainsSingle(m => m.Method.Name == "NeverCalled", greeter);
        Assert.AreEqual(MethodCorrelationStatus.NotInNativeImage, never.Status);

        var withSymbol = greeter.First(m => m.NativeSymbols.Count > 0);
        var reverse = index.FindByAddress(withSymbol.NativeSymbols[0].VirtualAddress);
        Assert.IsNotNull(reverse);
        Assert.AreEqual("Greeter", reverse!.Method.DeclaringType);
    }

    /// <summary>Verifies the ownership contract is structural: no public disposal surface at all.</summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void PreIlcCompanionSet_NoPublicDisposal()
    {
        Assert.IsFalse(typeof(IDisposable).IsAssignableFrom(typeof(PreIlcCompanionSet)));
        Assert.IsNull(typeof(PreIlcCompanionSet).GetMethod("Dispose"));
    }

    /// <summary>Disposes test resources created during the run.</summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        foreach (var path in _tempFiles)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
                else if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            }
            catch { /* best effort */ }
        }
    }
}
