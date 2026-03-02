using Hex1b;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Dotsider.Tests;

[Collection("SampleAssemblies")]
public class NuGetModeViewTests(SampleAssemblyFixture samples) : IDisposable
{
    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _hex1bApp;
    private NuGetState? _state;

    private (Hex1bTerminal terminal, Hex1bApp app) CreateNuGetApp()
    {
        _workload = new Hex1bAppWorkloadAdapter();
        _terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(_workload)
            .WithHeadless()
            .WithDimensions(120, 30)
            .Build();
        NuGetApp? nugetApp = null;
        _hex1bApp = new Hex1bApp(
            ctx =>
            {
                _state ??= new NuGetState(_hex1bApp!, samples.RichLibraryNupkg);
                nugetApp ??= new NuGetApp(_state);
                return Task.FromResult<Hex1bWidget>(nugetApp.Build(ctx));
            },
            new Hex1bAppOptions
            {
                WorkloadAdapter = _workload,
                EnableInputCoalescing = false
            });
        return (_terminal, _hex1bApp);
    }

    [Fact(Timeout = 10_000)]
    public async Task NuGetApp_Launches_ShowsPackageInfo()
    {
        var (terminal, app) = CreateNuGetApp();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var runTask = app.RunAsync(cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("RichLibrary") || s.ContainsText("nupkg"), TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 10_000)]
    public async Task NuGetApp_ShowsFileList()
    {
        var (terminal, app) = CreateNuGetApp();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var runTask = app.RunAsync(cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s =>
                s.ContainsText(".dll") || s.ContainsText(".nuspec") || s.ContainsText("DLL"),
                TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 10_000)]
    public async Task QuitKey_ExitsNuGetApp()
    {
        var (terminal, app) = CreateNuGetApp();
        var runTask = app.RunAsync(CancellationToken.None);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("RichLibrary") || s.ContainsText("nupkg"), TimeSpan.FromSeconds(3))
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
