using Hex1b;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Diff State.
/// </summary>
[TestClass]
public class DiffStateTests : IDisposable
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
    /// Verifies construct both analyzers accessible.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Construct_BothAnalyzersAccessible()
    {
        var app = CreateApp();
        using var state = new DiffState(app, Samples.RichLibraryDll, Samples.RichLibraryV2Dll);
        Assert.IsNotNull(state.Left);
        Assert.IsNotNull(state.Right);
        Assert.AreEqual("RichLibrary", state.Left.AssemblyName);
        Assert.AreEqual("RichLibrary", state.Right.AssemblyName);
    }

    /// <summary>
    /// Verifies construct diff result populated.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Construct_DiffResultPopulated()
    {
        var app = CreateApp();
        using var state = new DiffState(app, Samples.RichLibraryDll, Samples.RichLibraryV2Dll);
        Assert.IsNotNull(state.DiffResult);
        Assert.IsNotEmpty(state.DiffResult.TypeDiffs);
    }

    /// <summary>
    /// Verifies default filter mode is all.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void DefaultFilterMode_IsAll()
    {
        var app = CreateApp();
        using var state = new DiffState(app, Samples.RichLibraryDll, Samples.RichLibraryV2Dll);
        Assert.AreEqual(DiffFilterMode.All, state.FilterMode);
    }

    /// <summary>
    /// Verifies tab switching works.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void TabSwitching_Works()
    {
        var app = CreateApp();
        using var state = new DiffState(app, Samples.RichLibraryDll, Samples.RichLibraryV2Dll);
        state.CurrentTab = 2;
        Assert.AreEqual(2, state.CurrentTab);
    }

    /// <summary>
    /// Verifies dispose cleans up.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Dispose_CleansUp()
    {
        var app = CreateApp();
        var state = new DiffState(app, Samples.RichLibraryDll, Samples.RichLibraryV2Dll);
        state.Dispose();
        state.Dispose(); // idempotent — should not throw
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
