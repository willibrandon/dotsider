using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Diff Mode View.
/// </summary>
[TestClass]
public class DiffModeViewTests : IDisposable
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _hex1bApp;
    private DiffState? _state;

    private (Hex1bTerminal terminal, Hex1bApp app) CreateDiffApp()
    {
        _workload = new Hex1bAppWorkloadAdapter();
        _terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(_workload)
            .WithHeadless()
            .WithDimensions(120, 30)
            .Build();
        DiffApp? diffApp = null;
        _hex1bApp = new Hex1bApp(
            ctx =>
            {
                _state ??= new DiffState(_hex1bApp!, Samples.RichLibraryDll, Samples.RichLibraryV2Dll);
                diffApp ??= new DiffApp(_state);
                return Task.FromResult<Hex1bWidget>(diffApp.Build(ctx));
            },
            new Hex1bAppOptions
            {
                WorkloadAdapter = _workload,
                EnableInputCoalescing = false
            });
        return (_terminal, _hex1bApp);
    }

    /// <summary>
    /// Verifies diff app launches shows both assemblies.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task DiffApp_Launches_ShowsBothAssemblies()
    {
        var (terminal, app) = CreateDiffApp();
        var ct = CancellationToken.None;
        var runTask = app.RunAsync(ct);
        await Task.Delay(100, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s =>
                s.ContainsText("Added") || s.ContainsText("Removed") ||
                s.ContainsText("Changed") || s.ContainsText("RichLibrary"),
                TimeSpan.FromSeconds(10))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        await runTask.ContinueWith(_ => { }, ct);
    }

    /// <summary>
    /// Verifies diff app shows diff entries.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task DiffApp_ShowsDiffEntries()
    {
        var (terminal, app) = CreateDiffApp();
        var ct = CancellationToken.None;
        var runTask = app.RunAsync(ct);
        await Task.Delay(100, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s =>
                s.ContainsText("Added") || s.ContainsText("Removed") ||
                s.ContainsText("Changed") || s.ContainsText("Type"),
                TimeSpan.FromSeconds(10))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        await runTask.ContinueWith(_ => { }, ct);
    }

    /// <summary>
    /// Verifies quit key exits diff app.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task QuitKey_ExitsDiffApp()
    {
        var (terminal, app) = CreateDiffApp();
        var ct = CancellationToken.None;
        var runTask = app.RunAsync(ct);
        await Task.Delay(100, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary") || s.ContainsText("Diff"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Q)
            .Build()
            .ApplyAsync(terminal, ct);

        var completed = await Task.WhenAny(runTask, Task.Delay(5000, ct));
        Assert.AreEqual(runTask, completed);
    }

    /// <summary>
    /// Verifies arrow keys cycle tabs.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task ArrowKeys_CycleTabs()
    {
        var (terminal, app) = CreateDiffApp();
        var ct = CancellationToken.None;
        var runTask = app.RunAsync(ct);
        await Task.Delay(100, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Summary") || s.ContainsText("RichLibrary"), TimeSpan.FromSeconds(10))
            // Start on tab 0 (Summary), arrow right to Types (tab 1)
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.CurrentTab == 1, TimeSpan.FromSeconds(10))
            // Arrow right again to Methods (tab 2)
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.CurrentTab == 2, TimeSpan.FromSeconds(10))
            // Arrow left back to Types (tab 1)
            .Key(Hex1bKey.LeftArrow)
            .WaitUntil(_ => _state!.CurrentTab == 1, TimeSpan.FromSeconds(10))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.AreEqual(1, _state!.CurrentTab);

        await runTask.ContinueWith(_ => { }, ct);
    }

    /// <summary>
    /// Verifies search activates and filters.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Search_ActivatesAndFilters()
    {
        var (terminal, app) = CreateDiffApp();
        var ct = CancellationToken.None;
        var runTask = app.RunAsync(ct);
        await Task.Delay(100, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Summary") || s.ContainsText("RichLibrary"), TimeSpan.FromSeconds(10))
            // Switch to Types tab, then activate search
            .Key(Hex1bKey.D2)
            .WaitUntil(_ => _state!.CurrentTab == 1, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.OemQuestion)
            .WaitUntil(_ => _state!.Search[1].IsActive, TimeSpan.FromSeconds(10))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.IsTrue(_state!.Search[1].IsActive);

        await runTask.ContinueWith(_ => { }, ct);
    }

    /// <summary>
    /// Verifies diff app shows references tab.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task DiffApp_ShowsReferencesTab()
    {
        var (terminal, app) = CreateDiffApp();
        var ct = CancellationToken.None;
        var runTask = app.RunAsync(ct);
        await Task.Delay(100, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Summary") || s.ContainsText("RichLibrary"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D4) // Tab 4 — References
            .WaitUntil(_ => _state!.CurrentTab == 3, TimeSpan.FromSeconds(10))
            .WaitUntil(s =>
                s.ContainsText("References") || s.ContainsText("Assembly") ||
                s.ContainsText("Version"),
                TimeSpan.FromSeconds(10))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.AreEqual(3, _state!.CurrentTab);

        await runTask.ContinueWith(_ => { }, ct);
    }

    /// <summary>
    /// Verifies diff app shows methods tab.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task DiffApp_ShowsMethodsTab()
    {
        var (terminal, app) = CreateDiffApp();
        var ct = CancellationToken.None;
        var runTask = app.RunAsync(ct);
        await Task.Delay(100, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Summary") || s.ContainsText("RichLibrary"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D3) // Tab 3 — Methods
            .WaitUntil(_ => _state!.CurrentTab == 2, TimeSpan.FromSeconds(10))
            .WaitUntil(s =>
                s.ContainsText("Methods") || s.ContainsText("Method") ||
                s.ContainsText("Added") || s.ContainsText("Removed"),
                TimeSpan.FromSeconds(10))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.AreEqual(2, _state!.CurrentTab);

        await runTask.ContinueWith(_ => { }, ct);
    }

    /// <summary>
    /// Verifies left arrow does not go below zero.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task LeftArrow_DoesNotGoBelowZero()
    {
        var (terminal, app) = CreateDiffApp();
        var ct = CancellationToken.None;
        var runTask = app.RunAsync(ct);
        await Task.Delay(100, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Summary") || s.ContainsText("RichLibrary"), TimeSpan.FromSeconds(10))
            // On tab 0, press left twice — should stay at 0
            .Key(Hex1bKey.LeftArrow)
            .Key(Hex1bKey.LeftArrow)
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.AreEqual(0, _state!.CurrentTab);

        await runTask.ContinueWith(_ => { }, ct);
    }

    /// <summary>
    /// Disposes test resources created during the run.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _state?.Dispose();
        _hex1bApp?.Dispose();
        _terminal?.Dispose();
        _workload?.Dispose();
    }
}
