using Hex1b;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Nu Get State.
/// </summary>
[TestClass]
public class NuGetStateTests : IDisposable
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _app;

    private Hex1bApp CreateApp()
    {
        _workload = new Hex1bAppWorkloadAdapter();
        _terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(_workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();
        _app = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
            new Hex1bAppOptions { WorkloadAdapter = _workload });
        return _app;
    }

    /// <summary>
    /// Verifies construct package metadata populated.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Construct_PackageMetadataPopulated()
    {
        var app = CreateApp();
        using var state = new NuGetState(app, Samples.RichLibraryNupkg);
        Assert.AreEqual("RichLibrary", state.Package.PackageId);
        Assert.AreEqual("2.5.1", state.Package.PackageVersion);
    }

    /// <summary>
    /// Verifies construct file list populated.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Construct_FileListPopulated()
    {
        var app = CreateApp();
        using var state = new NuGetState(app, Samples.RichLibraryNupkg);
        Assert.IsNotEmpty(state.Package.Files);
        Assert.IsNotEmpty(state.Package.DllFiles);
    }

    /// <summary>
    /// Verifies is browsing package default true.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void IsBrowsingPackage_DefaultTrue()
    {
        var app = CreateApp();
        using var state = new NuGetState(app, Samples.RichLibraryNupkg);
        Assert.IsTrue(state.IsBrowsingPackage);
    }

    /// <summary>
    /// Verifies drill into dll creates inspector state.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void DrillInto_DllCreatesInspectorState()
    {
        var app = CreateApp();
        using var state = new NuGetState(app, Samples.RichLibraryNupkg);
        var dll = state.Package.DllFiles[0];
        var analyzer = state.Package.OpenDll(dll);
        state.SelectedDllState = new DotsiderState(app, analyzer);
        state.SelectedDllEntry = dll;
        state.IsBrowsingPackage = false;
        Assert.IsFalse(state.IsBrowsingPackage);
        Assert.IsNotNull(state.SelectedDllState);
    }

    /// <summary>
    /// Verifies dispose cleans up.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Dispose_CleansUp()
    {
        var app = CreateApp();
        var state = new NuGetState(app, Samples.RichLibraryNupkg);
        state.Dispose();
        state.Dispose(); // idempotent
    }

    /// <summary>
    /// Disposes test resources created during the run.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _app?.Dispose();
        _terminal?.Dispose();
        _workload?.Dispose();
    }
}
