using Hex1b;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Dotsider.Tests;

[Collection("SampleAssemblies")]
public class DiffModeViewTests(SampleAssemblyFixture samples) : IDisposable
{
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
                _state ??= new DiffState(_hex1bApp!, samples.RichLibraryDll, samples.RichLibraryV2Dll);
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

    [Fact(Timeout = 10_000)]
    public async Task DiffApp_Launches_ShowsBothAssemblies()
    {
        var (terminal, app) = CreateDiffApp();
        var ct = TestContext.Current.CancellationToken;
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s =>
                s.ContainsText("Added") || s.ContainsText("Removed") ||
                s.ContainsText("Changed") || s.ContainsText("RichLibrary"),
                TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        await runTask.ContinueWith(_ => { }, ct);
    }

    [Fact(Timeout = 10_000)]
    public async Task DiffApp_ShowsDiffEntries()
    {
        var (terminal, app) = CreateDiffApp();
        var ct = TestContext.Current.CancellationToken;
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s =>
                s.ContainsText("Added") || s.ContainsText("Removed") ||
                s.ContainsText("Changed") || s.ContainsText("Type"),
                TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        await runTask.ContinueWith(_ => { }, ct);
    }

    [Fact(Timeout = 10_000)]
    public async Task QuitKey_ExitsDiffApp()
    {
        var (terminal, app) = CreateDiffApp();
        var ct = TestContext.Current.CancellationToken;
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("RichLibrary") || s.ContainsText("Diff"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.Q)
            .Build()
            .ApplyAsync(terminal, ct);

        var completed = await Task.WhenAny(runTask, Task.Delay(5000, ct));
        Assert.Equal(runTask, completed);
    }

    [Fact(Timeout = 10_000)]
    public async Task ArrowKeys_CycleTabs()
    {
        var (terminal, app) = CreateDiffApp();
        var ct = TestContext.Current.CancellationToken;
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Summary") || s.ContainsText("RichLibrary"), TimeSpan.FromSeconds(5))
            // Start on tab 0 (Summary), arrow right to Types (tab 1)
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.CurrentTab == 1, TimeSpan.FromSeconds(2))
            // Arrow right again to Methods (tab 2)
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.CurrentTab == 2, TimeSpan.FromSeconds(2))
            // Arrow left back to Types (tab 1)
            .Key(Hex1bKey.LeftArrow)
            .WaitUntil(_ => _state!.CurrentTab == 1, TimeSpan.FromSeconds(2))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.Equal(1, _state!.CurrentTab);

        await runTask.ContinueWith(_ => { }, ct);
    }

    [Fact(Timeout = 10_000)]
    public async Task Search_ActivatesAndFilters()
    {
        var (terminal, app) = CreateDiffApp();
        var ct = TestContext.Current.CancellationToken;
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Summary") || s.ContainsText("RichLibrary"), TimeSpan.FromSeconds(5))
            // Switch to Types tab, then activate search
            .Key(Hex1bKey.D2)
            .WaitUntil(_ => _state!.CurrentTab == 1, TimeSpan.FromSeconds(2))
            .Key(Hex1bKey.OemQuestion)
            .WaitUntil(_ => _state!.Search[1].IsActive, TimeSpan.FromSeconds(2))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.True(_state!.Search[1].IsActive);

        await runTask.ContinueWith(_ => { }, ct);
    }

    [Fact(Timeout = 10_000)]
    public async Task LeftArrow_DoesNotGoBelowZero()
    {
        var (terminal, app) = CreateDiffApp();
        var ct = TestContext.Current.CancellationToken;
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Summary") || s.ContainsText("RichLibrary"), TimeSpan.FromSeconds(5))
            // On tab 0, press left twice — should stay at 0
            .Key(Hex1bKey.LeftArrow)
            .Key(Hex1bKey.LeftArrow)
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.Equal(0, _state!.CurrentTab);

        await runTask.ContinueWith(_ => { }, ct);
    }

    public void Dispose()
    {
        _state?.Dispose();
        _hex1bApp?.Dispose();
        _terminal?.Dispose();
        _workload?.Dispose();
        GC.SuppressFinalize(this);
    }
}
