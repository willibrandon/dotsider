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
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var runTask = app.RunAsync(cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s =>
                s.ContainsText("Added") || s.ContainsText("Removed") ||
                s.ContainsText("Changed") || s.ContainsText("RichLibrary"),
                TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 10_000)]
    public async Task DiffApp_ShowsDiffEntries()
    {
        var (terminal, app) = CreateDiffApp();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var runTask = app.RunAsync(cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s =>
                s.ContainsText("Added") || s.ContainsText("Removed") ||
                s.ContainsText("Changed") || s.ContainsText("Type"),
                TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 10_000)]
    public async Task QuitKey_ExitsDiffApp()
    {
        var (terminal, app) = CreateDiffApp();
        var runTask = app.RunAsync(CancellationToken.None);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("RichLibrary") || s.ContainsText("Diff"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.Q)
            .Build()
            .ApplyAsync(terminal);

        var completed = await Task.WhenAny(runTask, Task.Delay(5000));
        Assert.Equal(runTask, completed);
    }

    public void Dispose()
    {
        _state?.Dispose();
        _hex1bApp?.Dispose();
        _terminal?.Dispose();
        _workload?.Dispose();
    }
}
