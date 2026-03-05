using Hex1b;
using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Dotsider.Tests;

[Collection("SampleAssemblies")]
public class StandardModeViewTests(SampleAssemblyFixture samples) : IDisposable
{
    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _hex1bApp;
    private DotsiderState? _state;

    private (Hex1bTerminal terminal, Hex1bApp app) CreateDotsiderApp(string dllPath)
    {
        _workload = new Hex1bAppWorkloadAdapter();
        _terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(_workload)
            .WithHeadless()
            .WithDimensions(120, 30)
            .Build();
        DotsiderApp? dotsiderApp = null;
        _hex1bApp = new Hex1bApp(
            ctx =>
            {
                _state ??= new DotsiderState(_hex1bApp!, dllPath);
                dotsiderApp ??= new DotsiderApp(_state);
                return Task.FromResult<Hex1bWidget>(dotsiderApp.Build(ctx));
            },
            new Hex1bAppOptions
            {
                WorkloadAdapter = _workload,
                EnableInputCoalescing = false
            });
        return (_terminal, _hex1bApp);
    }

    [Fact(Timeout = 10_000)]
    public async Task App_Launches_ShowsAssemblyName()
    {
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var runTask = app.RunAsync(cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 10_000)]
    public async Task Tab2_ShowsMetadata()
    {
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var runTask = app.RunAsync(cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Type("2") // Tab 2 — PE/Metadata
            .WaitUntil(s => s.ContainsText("Sections") || s.ContainsText("TypeDef"), TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 10_000)]
    public async Task Tab3_ShowsIlInspector()
    {
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var runTask = app.RunAsync(cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.D3) // Tab 3 — IL Inspector
            .WaitUntil(s => s.ContainsText("Select a method") || s.ContainsText("IL"), TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 10_000)]
    public async Task Tab4_ShowsStrings()
    {
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var runTask = app.RunAsync(cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.D4) // Tab 4 — Strings
            .WaitUntil(s => s.ContainsText("Offset") || s.ContainsText("Value") || s.ContainsText("User Strings"), TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 10_000)]
    public async Task Tab5_ShowsHexDump()
    {
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var runTask = app.RunAsync(cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.D5) // Tab 5 — Hex Dump
            .WaitUntil(s => s.ContainsText("4D 5A") || s.ContainsText("00000"), TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 10_000)]
    public async Task Tab6_ShowsDepGraph()
    {
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var runTask = app.RunAsync(cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.D6) // Tab 6 — Dep Graph
            .WaitUntil(s => s.ContainsText("Newtonsoft") || s.ContainsText("System.Runtime"), TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 10_000)]
    public async Task Tab7_ShowsSizeMap()
    {
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var runTask = app.RunAsync(cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.D7) // Tab 7 — Size Map
            .WaitUntil(s => !s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 10_000)]
    public async Task Tab8_Library_ShowsNoEntryPoint()
    {
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var runTask = app.RunAsync(cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.D8) // Tab 8 — Dynamic
            .WaitUntil(s => s.ContainsText("entry point") || s.ContainsText("library"), TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 10_000)]
    public async Task Tab8_Exe_ShowsLaunchPrompt()
    {
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var runTask = app.RunAsync(cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.D8) // Tab 8 — Dynamic
            .WaitUntil(s => s.ContainsText("Enter") || s.ContainsText("Launch") || s.ContainsText("EventPipe"), TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 15_000)]
    public async Task Tab8_SearchAfterProcessExit_NoGlobalBindingConflict()
    {
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        var runTask = app.RunAsync(cts.Token);

        // Navigate to Dynamic tab and launch the process
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.D8)
            .WaitUntil(s => s.ContainsText("EventPipe") || s.ContainsText("Launch"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.Enter) // Launch process
            .WaitUntil(s => s.ContainsText("Exited") || s.ContainsText("Exit code"), TimeSpan.FromSeconds(8))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Process has exited — activating search must not crash with
        // "Global binding conflict: Enter is already registered"
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.OemQuestion) // '/' — activate search
            .WaitUntil(_ => _state!.Search[TabId.Dynamic].IsActive, TimeSpan.FromSeconds(3))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.True(_state!.Search[TabId.Dynamic].IsActive);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 10_000)]
    public async Task General_EnterOnReference_DrillsIntoAssembly()
    {
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var runTask = app.RunAsync(cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            // Focus starts on the dependency table; Enter to drill into the first ref
            .Key(Hex1bKey.Enter)
            // After drill-down, the title bar should no longer show "HelloWorld.dll"
            .WaitUntil(s => !s.ContainsText("HelloWorld.dll"), TimeSpan.FromSeconds(3))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 10_000)]
    public async Task Tab3_ArrowKeysWorkImmediately()
    {
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var runTask = app.RunAsync(cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.D3) // Tab 3 — IL Inspector
            .WaitUntil(s => s.ContainsText("Select a method"), TimeSpan.FromSeconds(3))
            // Arrow keys should work immediately without clicking first
            .Key(Hex1bKey.DownArrow) // Move to Program
            .Key(Hex1bKey.RightArrow) // Expand Program
            .WaitUntil(s => s.ContainsText(".ctor"), TimeSpan.FromSeconds(3))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 15_000)]
    public async Task Tab3_DisassemblyPaneScrolls()
    {
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        var runTask = app.RunAsync(cts.Token);

        // Navigate to StringHelpers.ToTitleCase (139 bytes of IL, overflows viewport)
        var builder = new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.D3)
            .WaitUntil(s => s.ContainsText("Select a method"), TimeSpan.FromSeconds(3));

        for (var i = 0; i < 15; i++)
            builder = builder.Key(Hex1bKey.DownArrow);

        await builder
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(s => s.ContainsText("ToTitleCase"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.Enter)
            .WaitUntil(s => s.ContainsText("IL_0000"), TimeSpan.FromSeconds(3))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Focus the scroll panel and scroll down
        _hex1bApp!.RequestFocus(node => node is ScrollPanelNode);
        _hex1bApp.Invalidate();

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("IL_0000"), TimeSpan.FromSeconds(1))
            .PageDown()
            .WaitUntil(s => !s.ContainsText("IL_0000"), TimeSpan.FromSeconds(3))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 10_000)]
    public async Task Tab4_ArrowKeysCycleSubTabs()
    {
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var runTask = app.RunAsync(cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.D4) // Tab 4 — Strings
            .WaitUntil(s => s.ContainsText("User Strings"), TimeSpan.FromSeconds(3))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Verify starting state
        Assert.Equal(0, _state!.StringsSourceTab);

        // Right arrow → sub-tab 1
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state.StringsSourceTab == 1, TimeSpan.FromSeconds(3))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(1, _state.StringsSourceTab);

        // Right arrow → sub-tab 2
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state.StringsSourceTab == 2, TimeSpan.FromSeconds(3))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(2, _state.StringsSourceTab);

        // Left arrow → back to sub-tab 1
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.LeftArrow)
            .WaitUntil(_ => _state.StringsSourceTab == 1, TimeSpan.FromSeconds(3))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(1, _state.StringsSourceTab);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 10_000)]
    public async Task Tab4_ArrowKeysDuringSearchEditing_DoNotSwitchSubTab()
    {
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var runTask = app.RunAsync(cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.D4) // Tab 4 — Strings
            .WaitUntil(s => s.ContainsText("User Strings"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.OemQuestion) // '/' — activate search
            .WaitUntil(_ => _state!.Search[TabId.Strings].IsActive, TimeSpan.FromSeconds(3))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(0, _state!.StringsSourceTab);

        // Arrow keys during search editing should NOT switch sub-tabs
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .Key(Hex1bKey.RightArrow)
            .Key(Hex1bKey.LeftArrow)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(0, _state.StringsSourceTab);
        Assert.True(_state.Search[TabId.Strings].IsActive);

        // Dismiss search, then arrows should work again
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => !_state.Search[TabId.Strings].IsActive, TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state.StringsSourceTab == 1, TimeSpan.FromSeconds(3))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(1, _state.StringsSourceTab);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 10_000)]
    public async Task QuitKey_ExitsApp()
    {
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var runTask = app.RunAsync(CancellationToken.None);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.Q) // q = quit
            .Build()
            .ApplyAsync(terminal);

        // App should exit after q key
        var completed = await Task.WhenAny(runTask, Task.Delay(5000));
        Assert.Equal(runTask, completed);
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
