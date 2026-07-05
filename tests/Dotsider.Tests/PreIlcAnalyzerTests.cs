using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Tests;

/// <summary>
/// Tests for the analyzer-level pre-ILC surface: probe gating, companion attach/detach
/// lifecycle, generation-guarded index builds, sidecar path fallbacks, and correlation
/// against the real fixture.
/// </summary>
[Collection("SampleAssemblies")]
public class PreIlcAnalyzerTests(SampleAssemblyFixture samples) : IDisposable
{
    private readonly List<string> _tempFiles = [];

    private string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"dotsider-preilc-an-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempFiles.Add(dir);
        return dir;
    }

    /// <summary>Verifies the probe is gated on the Native AOT binary kind.</summary>
    [Fact(Timeout = 30_000)]
    public void PreIlcSidecars_ManagedAssemblyAndApphost_Null()
    {
        using var managed = new AssemblyAnalyzer(samples.HelloWorldDll);
        Assert.Null(managed.PreIlcSidecars);
        Assert.Null(managed.AttachPreIlcCompanions());

        using var apphost = new AssemblyAnalyzer(samples.HelloWorldExe);
        Assert.Null(apphost.PreIlcSidecars);
    }

    /// <summary>Verifies the fixture AOT exe probes its full sidecar set.</summary>
    [Fact(Timeout = 30_000)]
    public void PreIlcSidecars_FixtureAotExe_Found()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        using var analyzer = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);
        var sidecars = analyzer.PreIlcSidecars;

        Assert.NotNull(sidecars);
        Assert.True(sidecars!.HasAttachableCompanion);
        Assert.Equal(PreIlcAssemblyOrigin.IlcResponseFile, sidecars.Origin);
    }

    /// <summary>Verifies attach is idempotent and the set carries readable metadata.</summary>
    [Fact(Timeout = 30_000)]
    public void AttachPreIlcCompanions_Idempotent()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        using var analyzer = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);
        var set = analyzer.AttachPreIlcCompanions();

        Assert.NotNull(set);
        Assert.Same(set, analyzer.AttachPreIlcCompanions());
        Assert.Same(set, analyzer.PreIlcCompanions);
        Assert.True(set!.Root.HasMetadata);
        Assert.Equal("NativeAotConsole", set.Root.AssemblyName);
        Assert.Same(set.Root, set.All[0]);
    }

    /// <summary>Verifies attach returns null when nothing attachable was probed.</summary>
    [Fact(Timeout = 30_000)]
    public void AttachPreIlcCompanions_NoSidecars_Null()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var dir = NewTempDir();
        var exe = Path.Combine(dir, "NativeAotConsole.exe");
        File.Copy(samples.NativeAotConsoleExe!, exe);

        using var analyzer = new AssemblyAnalyzer(exe);
        Assert.Null(analyzer.PreIlcSidecars);
        Assert.Null(analyzer.AttachPreIlcCompanions());
        Assert.Null(analyzer.ManagedNativeIndex);
    }

    /// <summary>Verifies detach disposes the companions and drops the index.</summary>
    [Fact(Timeout = 30_000)]
    public void DetachPreIlcCompanions_DisposesSetAndDropsIndex()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        using var analyzer = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);
        var set = analyzer.AttachPreIlcCompanions();
        Assert.NotNull(set);

        analyzer.DetachPreIlcCompanions();

        Assert.Null(analyzer.PreIlcCompanions);
        Assert.Null(analyzer.ManagedNativeIndex);
        Assert.Throws<ObjectDisposedException>(() => set!.Root.TypeDefs);
    }

    /// <summary>Verifies disposing the owner disposes the attached companions transitively.</summary>
    [Fact(Timeout = 30_000)]
    public void Dispose_DisposesAttachedCompanions()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var analyzer = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);
        var set = analyzer.AttachPreIlcCompanions();
        Assert.NotNull(set);

        analyzer.Dispose();

        Assert.Throws<ObjectDisposedException>(() => set!.Root.MethodDefs);
    }

    /// <summary>Verifies the index is null before attach and builds once after.</summary>
    [Fact(Timeout = 60_000)]
    public void ManagedNativeIndex_BuildsAfterAttach()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        using var analyzer = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);
        Assert.Null(analyzer.ManagedNativeIndex);

        analyzer.AttachPreIlcCompanions();
        var index = analyzer.ManagedNativeIndex;

        Assert.NotNull(index);
        Assert.Same(index, analyzer.ManagedNativeIndex);
        Assert.True(index!.Methods.Count > 0);
    }

    /// <summary>Verifies a stale build after detach never publishes; the reader never throws while the analyzer lives.</summary>
    [Fact(Timeout = 60_000)]
    public async Task ManagedNativeIndex_RacingDetach_NeverPublishesStale()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        using var analyzer = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var reader = Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
                _ = analyzer.ManagedNativeIndex;
        }, cts.Token);

        for (var i = 0; i < 5; i++)
        {
            analyzer.AttachPreIlcCompanions();
            await Task.Delay(50, TestContext.Current.CancellationToken);
            analyzer.DetachPreIlcCompanions();
        }

        await cts.CancelAsync();
        await reader;

        Assert.Null(analyzer.PreIlcCompanions);
        Assert.Null(analyzer.ManagedNativeIndex);
    }

    /// <summary>Verifies a build racing the owner's dispose abandons cleanly (returns or ODE only).</summary>
    [Fact(Timeout = 60_000)]
    public async Task ManagedNativeIndex_RacingDispose_AbandonsCleanly()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var analyzer = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);
        analyzer.AttachPreIlcCompanions();

        var build = Task.Run(() =>
        {
            try { _ = analyzer.ManagedNativeIndex; }
            catch (ObjectDisposedException) { /* documented outcome */ }
        }, TestContext.Current.CancellationToken);

        analyzer.Dispose();
        await build;
    }

    /// <summary>Verifies MstatPath/DgmlPath fall back to the obj tree, sibling wins for mstat, codegen-first for DGML.</summary>
    [Fact(Timeout = 30_000)]
    public void MstatAndDgmlPaths_ObjTreeFallbackAndCodegenPrecedence()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var root = NewTempDir();
        var exeDir = Path.Combine(root, "Proj", "bin", "Release", "net10.0", "win-x64", "publish");
        Directory.CreateDirectory(exeDir);
        var nativeDir = Path.Combine(root, "Proj", "obj", "Release", "net10.0", "win-x64", "native");
        Directory.CreateDirectory(nativeDir);

        var exe = Path.Combine(exeDir, "NativeAotConsole.exe");
        File.Copy(samples.NativeAotConsoleExe!, exe);

        var objMstat = Path.Combine(nativeDir, "NativeAotConsole.mstat");
        File.WriteAllBytes(objMstat, [1]);
        File.WriteAllBytes(Path.Combine(exeDir, "NativeAotConsole.scan.dgml.xml"), [1]);
        var objCodegen = Path.Combine(nativeDir, "NativeAotConsole.codegen.dgml.xml");
        File.WriteAllBytes(objCodegen, [1]);

        using var analyzer = new AssemblyAnalyzer(exe);
        Assert.Equal(objMstat, analyzer.MstatPath);
        Assert.Equal(objCodegen, analyzer.DgmlPath); // codegen-first across locations

        var siblingMstat = Path.Combine(exeDir, "NativeAotConsole.mstat");
        File.WriteAllBytes(siblingMstat, [1]);
        using var analyzer2 = new AssemblyAnalyzer(exe);
        Assert.Equal(siblingMstat, analyzer2.MstatPath);
    }

    /// <summary>Verifies sidecar stems strip library extensions: a Native AOT .dll finds its mstat.</summary>
    [Fact(Timeout = 30_000)]
    public void FindSidecar_LibraryStem_FindsMstat()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        var dir = NewTempDir();
        var lib = Path.Combine(dir, "SomeAotLib.dll");
        File.Copy(samples.NativeAotConsoleExe!, lib); // AOT by content, library by name
        var mstat = Path.Combine(dir, "SomeAotLib.mstat");
        File.WriteAllBytes(mstat, [1]);

        using var analyzer = new AssemblyAnalyzer(lib);
        Assert.Equal(BinaryKind.NativeAot, analyzer.BinaryKind);
        Assert.Equal(mstat, analyzer.MstatPath);
    }

    /// <summary>Verifies the fixture Greeter correlations end to end, including ctor and accessor.</summary>
    [Fact(Timeout = 60_000)]
    public void ManagedNativeIndex_FixtureGreeterCorrelations()
    {
        Assert.SkipWhen(samples.NativeAotConsoleExe is null, "NativeAOT sample was not built");

        using var analyzer = new AssemblyAnalyzer(samples.NativeAotConsoleExe!);
        analyzer.AttachPreIlcCompanions();
        var index = analyzer.ManagedNativeIndex;
        Assert.NotNull(index);

        var greeter = index!.Methods.Where(m => m.Method.DeclaringType == "Greeter").ToList();
        Assert.NotEmpty(greeter);

        var describe = Assert.Single(greeter, m => m.Method.Name == "Describe");
        Assert.Equal(MethodCorrelationStatus.CorrelatedExact, describe.Status);
        Assert.True(describe.NativeSize > 0);

        var greets = greeter.Where(m => m.Method.Name == "Greet").ToList();
        Assert.Equal(2, greets.Count);
        Assert.All(greets, g => Assert.Equal(MethodCorrelationStatus.CorrelatedAmbiguous, g.Status));
        Assert.All(greets, g => Assert.Equal(0, g.NativeSize));
        Assert.True(greets[0].SharedCandidateSize > 0);

        var ctor = Assert.Single(greeter, m => m.Method.Name == ".ctor");
        Assert.NotEqual(MethodCorrelationStatus.NotInNativeImage, ctor.Status);

        var getName = Assert.Single(greeter, m => m.Method.Name == "get_Name");
        Assert.NotEqual(MethodCorrelationStatus.NotInNativeImage, getName.Status);

        var never = Assert.Single(greeter, m => m.Method.Name == "NeverCalled");
        Assert.Equal(MethodCorrelationStatus.NotInNativeImage, never.Status);

        var withSymbol = greeter.First(m => m.NativeSymbols.Count > 0);
        var reverse = index.FindByAddress(withSymbol.NativeSymbols[0].VirtualAddress);
        Assert.NotNull(reverse);
        Assert.Equal("Greeter", reverse!.Method.DeclaringType);
    }

    /// <summary>Verifies the ownership contract is structural: no public disposal surface at all.</summary>
    [Fact(Timeout = 30_000)]
    public void PreIlcCompanionSet_NoPublicDisposal()
    {
        Assert.False(typeof(IDisposable).IsAssignableFrom(typeof(PreIlcCompanionSet)));
        Assert.Null(typeof(PreIlcCompanionSet).GetMethod("Dispose"));
    }

    /// <summary>Disposes test resources created during the run.</summary>
    public void Dispose()
    {
        foreach (var path in _tempFiles)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
                else if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
            }
            catch { /* best effort */ }
        }
        GC.SuppressFinalize(this);
    }
}
