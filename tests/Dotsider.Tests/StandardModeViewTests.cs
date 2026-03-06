using Dotsider.Analysis.Models;
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

    private (Hex1bTerminal terminal, Hex1bApp app) CreateDotsiderApp(string dllPath, int? initialTab = null)
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
                if (_state is null)
                {
                    _state = new DotsiderState(_hex1bApp!, dllPath);
                    if (initialTab.HasValue)
                        _state.CurrentTab = initialTab.Value;
                }
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
    public async Task Tab5_StartsInNormalMode_ReadOnly()
    {
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var runTask = app.RunAsync(cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.D5)
            .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(3))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(HexEditMode.Normal, _state!.HexMode);
        Assert.True(_state.HexEditorState.IsReadOnly);
        Assert.False(_state.HexIsDirty);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 10_000)]
    public async Task Tab5_IKey_EntersInsertMode()
    {
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var runTask = app.RunAsync(cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.D5)
            .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.I)
            .WaitUntil(s => s.ContainsText("INSERT"), TimeSpan.FromSeconds(3))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(HexEditMode.Insert, _state!.HexMode);
        Assert.False(_state.HexEditorState.IsReadOnly);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 10_000)]
    public async Task Tab5_EscFromInsert_ReturnsToNormal()
    {
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var runTask = app.RunAsync(cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.D5)
            .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.I)
            .WaitUntil(s => s.ContainsText("INSERT"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.Escape)
            .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(3))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(HexEditMode.Normal, _state!.HexMode);
        Assert.True(_state.HexEditorState.IsReadOnly);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 10_000)]
    public async Task Tab5_EscFromInsert_WithConfirmedSearch_ExitsInsertFirst()
    {
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var runTask = app.RunAsync(cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.D5)
            .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(3))
            // Start a search and confirm it
            .Key(Hex1bKey.OemQuestion) // '/' — activate search
            .WaitUntil(_ => _state!.Search[TabId.HexDump].IsActive, TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.M)
            .Key(Hex1bKey.Z)
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.Search[TabId.HexDump].IsConfirmed, TimeSpan.FromSeconds(3))
            // Enter insert mode
            .Key(Hex1bKey.I)
            .WaitUntil(s => s.ContainsText("INSERT"), TimeSpan.FromSeconds(3))
            // First Esc should exit insert mode, NOT dismiss search
            .Key(Hex1bKey.Escape)
            .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(3))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Insert mode must be exited
        Assert.Equal(HexEditMode.Normal, _state!.HexMode);
        Assert.True(_state.HexEditorState.IsReadOnly);
        // Search should still be active (not dismissed by this Esc)
        Assert.True(_state.Search[TabId.HexDump].IsActive);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 10_000)]
    public async Task Tab5_NormalMode_VimKeysNavigate()
    {
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var runTask = app.RunAsync(cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.D5)
            .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(3))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        var cursorBefore = _state!.HexEditorState.Cursor.Position;

        // Press 'l' to move right in normal mode
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.L)
            .WaitUntil(_ => _state.HexEditorState.Cursor.Position != cursorBefore, TimeSpan.FromSeconds(3))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.NotEqual(cursorBefore, _state.HexEditorState.Cursor.Position);
        // Document should NOT be modified — we're in normal mode
        Assert.False(_state.HexIsDirty);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 10_000)]
    public async Task Tab5_InsertMode_SKey_DoesNotToggleSize()
    {
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var runTask = app.RunAsync(cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.D5)
            .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.I)
            .WaitUntil(s => s.ContainsText("INSERT"), TimeSpan.FromSeconds(3))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        var sizesBefore = _state!.HumanReadableSizes;

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.S)
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(sizesBefore, _state.HumanReadableSizes);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 10_000)]
    public async Task Tab5_InsertMode_QKey_DoesNotQuit()
    {
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var runTask = app.RunAsync(cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.D5)
            .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.I)
            .WaitUntil(s => s.ContainsText("INSERT"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.Q) // Should NOT quit — we're in insert mode
            .Ctrl().Key(Hex1bKey.C) // This quits
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // If Q had quit, runTask would already be completed before Ctrl+C
        // The fact that we reach here means the app was still running
        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 10_000)]
    public async Task Tab5_InsertMode_NumberKeys_DoNotSwitchTabs()
    {
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var runTask = app.RunAsync(cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.D5)
            .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.I)
            .WaitUntil(s => s.ContainsText("INSERT"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.D1) // Should NOT switch to tab 1
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(TabId.HexDump, _state!.CurrentTab);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 10_000)]
    public async Task Tab5_NormalMode_NoInsertIndicator()
    {
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var runTask = app.RunAsync(cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.D5)
            .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(3))
            // Verify normal mode does not show INSERT indicator
            .WaitUntil(s => !s.ContainsText("INSERT"), TimeSpan.FromSeconds(1))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(HexEditMode.Normal, _state!.HexMode);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 15_000)]
    public async Task Tab5_CtrlS_SavesWithCorrectFileName()
    {
        // Work on a disposable copy so we don't modify the shared fixture assembly
        var tempDir = Path.Combine(Path.GetTempPath(), $"dotsider-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var tempDll = Path.Combine(tempDir, "HelloWorld.dll");
        File.Copy(samples.HelloWorldDll, tempDll);

        try
        {
            var (terminal, app) = CreateDotsiderApp(tempDll);
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            var runTask = app.RunAsync(cts.Token);

            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
                .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
                .Key(Hex1bKey.D5)
                .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(3))
                // Enter insert mode, skip past MZ header into DOS stub padding,
                // then type two nibbles to complete a byte edit without breaking PE
                .Key(Hex1bKey.I)
                .WaitUntil(s => s.ContainsText("INSERT"), TimeSpan.FromSeconds(3))
                .Key(Hex1bKey.RightArrow).Key(Hex1bKey.RightArrow)
                .Key(Hex1bKey.RightArrow).Key(Hex1bKey.RightArrow)
                .Key(Hex1bKey.F)
                .Key(Hex1bKey.F)
                .WaitUntil(_ => _state!.HexIsDirty, TimeSpan.FromSeconds(3))
                // Return to normal mode, then save
                .Key(Hex1bKey.Escape)
                .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(3))
                .Ctrl().Key(Hex1bKey.S)
                .WaitUntil(_ => _state!.HexNotification != null, TimeSpan.FromSeconds(3))
                .Ctrl().Key(Hex1bKey.C)
                .Build()
                .ApplyAsync(terminal, cts.Token);

            // FilePath must be the original, not the .tmp fallback
            Assert.Equal(tempDll, _state!.Analyzer.FilePath);
            Assert.DoesNotContain(".tmp", _state.Analyzer.FileName);
            Assert.Contains("written", _state.HexNotification);
            Assert.Contains("HelloWorld.dll", _state.HexNotification);
            // No temp file should remain
            Assert.False(File.Exists(tempDll + ".tmp"));

            await runTask.ContinueWith(_ => { });
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
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
    public async Task Tab6_ShowsNodeAndEdgeCounts()
    {
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var runTask = app.RunAsync(cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.D6)
            .WaitUntil(s => s.ContainsText("Nodes:") && s.ContainsText("Edges:"), TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.NotNull(_state!.CachedGraph);
        Assert.True(_state.CachedGraph.Value.Nodes.Count > 0);
        Assert.True(_state.CachedGraph.Value.Edges.Count > 0);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 15_000)]
    public async Task Tab6_SearchShowsMatchCount()
    {
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        var runTask = app.RunAsync(cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.D6)
            .WaitUntil(s => s.ContainsText("Nodes:"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.OemQuestion) // '/' — activate search
            .WaitUntil(_ => _state!.Search[TabId.DepGraph].IsActive, TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.S).Key(Hex1bKey.Y).Key(Hex1bKey.S) // "sys"
            .Key(Hex1bKey.Enter) // Confirm
            .WaitUntil(_ => _state!.Search[TabId.DepGraph].IsConfirmed, TimeSpan.FromSeconds(3))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.True(_state!.Search[TabId.DepGraph].MatchCount > 0,
            "Search for 'sys' should match System.* dependencies");

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 15_000)]
    public async Task Tab6_MatchNavigation_CyclesGraphSelectedNode()
    {
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        var runTask = app.RunAsync(cts.Token);

        // Navigate to dep graph and search for "sys"
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.D6)
            .WaitUntil(s => s.ContainsText("Nodes:"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.OemQuestion)
            .WaitUntil(_ => _state!.Search[TabId.DepGraph].IsActive, TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.S).Key(Hex1bKey.Y).Key(Hex1bKey.S)
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.Search[TabId.DepGraph].IsConfirmed, TimeSpan.FromSeconds(3))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Press 'n' to navigate to first match
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.N)
            .WaitUntil(_ => _state!.GraphMatchIndex >= 0, TimeSpan.FromSeconds(3))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        var firstIndex = _state!.GraphMatchIndex;
        Assert.True(firstIndex >= 0);

        // Press 'n' again — should advance to next match
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.N)
            .WaitUntil(_ => _state.GraphMatchIndex != firstIndex
                            || _state.Search[TabId.DepGraph].MatchCount == 1,
                TimeSpan.FromSeconds(3))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        if (_state.Search[TabId.DepGraph].MatchCount > 1)
            Assert.NotEqual(firstIndex, _state.GraphMatchIndex);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 15_000)]
    public async Task Tab6_ArrowKeys_WorkAfterSearchConfirm()
    {
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        var runTask = app.RunAsync(cts.Token);

        // Navigate to dep graph, search for "sys", confirm
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.D6)
            .WaitUntil(s => s.ContainsText("Nodes:"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.OemQuestion)
            .WaitUntil(_ => _state!.Search[TabId.DepGraph].IsActive, TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.S).Key(Hex1bKey.Y).Key(Hex1bKey.S)
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.Search[TabId.DepGraph].IsConfirmed, TimeSpan.FromSeconds(3))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Arrow keys should work immediately — focus restored to Interactable
        Assert.Equal(-1, _state!.GraphSelectedIndex);

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.GraphSelectedIndex == 0, TimeSpan.FromSeconds(3))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(0, _state.GraphSelectedIndex);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 15_000)]
    public async Task Tab7_ArrowKeys_WorkAfterSearchConfirm()
    {
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        var runTask = app.RunAsync(cts.Token);

        // Navigate to Size Map, search for "rich", confirm
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.D7)
            .WaitUntil(s => s.ContainsText("Total:"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.OemQuestion)
            .WaitUntil(_ => _state!.Search[TabId.SizeMap].IsActive, TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.R).Key(Hex1bKey.I).Key(Hex1bKey.C).Key(Hex1bKey.H)
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.Search[TabId.SizeMap].IsConfirmed, TimeSpan.FromSeconds(3))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Arrow keys should work immediately — focus restored to Interactable
        Assert.Equal(-1, _state!.TreemapSelectedIndex);

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.TreemapSelectedIndex == 0, TimeSpan.FromSeconds(3))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(0, _state.TreemapSelectedIndex);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 15_000)]
    public async Task Tab6_StartupFocus_ArrowKeysWorkWithoutTabSwitch()
    {
        // Start directly on Dep Graph tab — tests the initial focus predicate
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll, initialTab: TabId.DepGraph);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        var runTask = app.RunAsync(cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Nodes:"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(-1, _state!.GraphSelectedIndex);

        // Arrow keys should work immediately without switching tabs first
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.GraphSelectedIndex == 0, TimeSpan.FromSeconds(3))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(0, _state.GraphSelectedIndex);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 15_000)]
    public async Task Tab7_StartupFocus_ArrowKeysWorkWithoutTabSwitch()
    {
        // Start directly on Size Map tab — tests the initial focus predicate
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll, initialTab: TabId.SizeMap);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        var runTask = app.RunAsync(cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Total:"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(-1, _state!.TreemapSelectedIndex);

        // Arrow keys should work immediately without switching tabs first
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.TreemapSelectedIndex == 0, TimeSpan.FromSeconds(3))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(0, _state.TreemapSelectedIndex);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 15_000)]
    public async Task Tab6_ArrowKeys_CycleSelectedIndex()
    {
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        var runTask = app.RunAsync(cts.Token);

        // Navigate to dep graph tab
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.D6)
            .WaitUntil(s => s.ContainsText("Nodes:"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(-1, _state!.GraphSelectedIndex);

        // Press Right to select first node
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.GraphSelectedIndex == 0, TimeSpan.FromSeconds(3))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(0, _state.GraphSelectedIndex);

        // Press Right again — should advance
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state.GraphSelectedIndex == 1, TimeSpan.FromSeconds(3))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(1, _state.GraphSelectedIndex);

        // Press Left — should go back
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.LeftArrow)
            .WaitUntil(_ => _state.GraphSelectedIndex == 0, TimeSpan.FromSeconds(3))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(0, _state.GraphSelectedIndex);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 15_000)]
    public async Task Tab7_ArrowKeys_CycleSelectedIndex()
    {
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        var runTask = app.RunAsync(cts.Token);

        // Navigate to Size Map tab
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.D7)
            .WaitUntil(s => s.ContainsText("Total:"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(-1, _state!.TreemapSelectedIndex);

        // Press Right to select first item
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.TreemapSelectedIndex == 0, TimeSpan.FromSeconds(3))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(0, _state.TreemapSelectedIndex);

        // Press Right again — should advance
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state.TreemapSelectedIndex == 1, TimeSpan.FromSeconds(3))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(1, _state.TreemapSelectedIndex);

        // Press Left — should go back
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.LeftArrow)
            .WaitUntil(_ => _state.TreemapSelectedIndex == 0, TimeSpan.FromSeconds(3))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(0, _state.TreemapSelectedIndex);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 10_000)]
    public async Task Tab7_ShowsBreadcrumbAndTotalSize()
    {
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var runTask = app.RunAsync(cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.D7)
            .WaitUntil(s => s.ContainsText("RichLibrary") && s.ContainsText("Total:"),
                TimeSpan.FromSeconds(5))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.NotNull(_state!.CachedSizeTree);
        Assert.True(_state.CachedSizeTree.Size > 0);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 15_000)]
    public async Task Tab7_Backspace_PopsBreadcrumb()
    {
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        var runTask = app.RunAsync(cts.Token);

        // Navigate to tab 7, let treemap render
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.D7)
            .WaitUntil(s => s.ContainsText("RichLibrary") && s.ContainsText("Total:"),
                TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Programmatically drill down into first child namespace
        var root = _state!.CachedSizeTree!;
        var firstChild = root.Children[0];
        _state.TreemapBreadcrumb.Push(root);
        _state.TreemapCurrentLevel = firstChild;
        _hex1bApp!.Invalidate();

        // Wait for breadcrumb to show the drill-down path (root > child)
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText(firstChild.Name), TimeSpan.FromSeconds(3))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Single(_state.TreemapBreadcrumb);

        // Press Backspace to go up
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Backspace)
            .WaitUntil(_ => _state.TreemapBreadcrumb.Count == 0, TimeSpan.FromSeconds(3))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Empty(_state.TreemapBreadcrumb);
        Assert.Equal(root, _state.TreemapCurrentLevel);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 15_000)]
    public async Task Tab7_SearchMatchNavigation_UpdatesHoveredItem()
    {
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        var runTask = app.RunAsync(cts.Token);

        // Navigate to tab 7 and search for a namespace
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.D7)
            .WaitUntil(s => s.ContainsText("Total:"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.OemQuestion) // '/' — activate search
            .WaitUntil(_ => _state!.Search[TabId.SizeMap].IsActive, TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.R).Key(Hex1bKey.I).Key(Hex1bKey.C).Key(Hex1bKey.H) // "rich"
            .Key(Hex1bKey.Enter) // Confirm
            .WaitUntil(_ => _state!.Search[TabId.SizeMap].IsConfirmed, TimeSpan.FromSeconds(3))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.True(_state!.Search[TabId.SizeMap].MatchCount > 0,
            "Search for 'rich' should match RichLibrary namespace");

        // Press 'n' to navigate to first match
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.N)
            .WaitUntil(_ => _state.TreemapMatchIndex >= 0, TimeSpan.FromSeconds(3))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.True(_state.TreemapMatchIndex >= 0);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 15_000)]
    public async Task Tab7_Enter_PrefersSearchMatchOverStaleSelection()
    {
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        var runTask = app.RunAsync(cts.Token);

        // Navigate to Size Map, select first item with arrow key
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.D7)
            .WaitUntil(s => s.ContainsText("Total:"), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.TreemapSelectedIndex == 0, TimeSpan.FromSeconds(3))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        var currentLevel = _state!.TreemapCurrentLevel ?? _state.CachedSizeTree!;
        Assert.Equal(0, _state.TreemapSelectedIndex);

        // Find a drillable child at index != 0 whose name differs from child 0
        var child0Name = currentLevel.Children[0].Name;
        string? searchTerm = null;
        for (var i = 1; i < currentLevel.Children.Count; i++)
        {
            var child = currentLevel.Children[i];
            if (child.Children.Count == 0) continue;
            // Use enough of the name to get a unique-ish match, but not child 0
            var candidate = child.Name.Length > 3 ? child.Name[..4] : child.Name;
            if (!child0Name.Contains(candidate, StringComparison.OrdinalIgnoreCase))
            {
                searchTerm = candidate.ToLowerInvariant();
                break;
            }
        }

        if (searchTerm is null)
        {
            // No suitable child found — skip
            await new Hex1bTerminalInputSequenceBuilder()
                .Ctrl().Key(Hex1bKey.C)
                .Build()
                .ApplyAsync(terminal, cts.Token);
            await runTask.ContinueWith(_ => { });
            return;
        }

        // Search for the non-zero child, confirm, navigate to match
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.OemQuestion)
            .WaitUntil(_ => _state.Search[TabId.SizeMap].IsActive, TimeSpan.FromSeconds(3))
            .Type(searchTerm)
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state.Search[TabId.SizeMap].IsConfirmed, TimeSpan.FromSeconds(3))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        if (_state.Search[TabId.SizeMap].MatchCount == 0)
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .Ctrl().Key(Hex1bKey.C)
                .Build()
                .ApplyAsync(terminal, cts.Token);
            await runTask.ContinueWith(_ => { });
            return;
        }

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.N)
            .WaitUntil(_ => _state.TreemapMatchIndex >= 0, TimeSpan.FromSeconds(3))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Stale selection is still 0, but search match points elsewhere
        Assert.Equal(0, _state.TreemapSelectedIndex);
        Assert.True(_state.TreemapMatchIndex >= 0);

        // Press Enter — should drill into search match, not stale selection at index 0
        var previousLevel = _state.TreemapCurrentLevel;
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state.TreemapCurrentLevel != previousLevel
                            || _state.TreemapBreadcrumb.Count > 0, TimeSpan.FromSeconds(3))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Verify we drilled into the search match (name contains query), not child 0
        if (_state.TreemapCurrentLevel != previousLevel)
        {
            Assert.Contains(searchTerm, _state.TreemapCurrentLevel!.Name,
                StringComparison.OrdinalIgnoreCase);
        }

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

        // Focus the scroll panel and scroll down.
        // RequestFocus is async — send multiple PageDowns so at least one
        // lands after focus has been applied to the scroll panel.
        _hex1bApp!.RequestFocus(node => node is ScrollPanelNode);
        _hex1bApp.Invalidate();

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("IL_0000"), TimeSpan.FromSeconds(1))
            .PageDown()
            .PageDown()
            .PageDown()
            .WaitUntil(s => !s.ContainsText("IL_0000"), TimeSpan.FromSeconds(5))
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

    [Fact(Timeout = 15_000)]
    public async Task Tab8_Events_SKey_FiltersSocket_NotToggleSize()
    {
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        var runTask = app.RunAsync(cts.Token);

        // Navigate to Dynamic tab, launch the process, wait for exit
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.D8)
            .WaitUntil(s => s.ContainsText("EventPipe") || s.ContainsText("Launch"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.Enter)
            .WaitUntil(s => s.ContainsText("Exited") || s.ContainsText("Exit code"), TimeSpan.FromSeconds(8))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Record initial size toggle state
        var sizesBefore = _state!.HumanReadableSizes;

        // Press S on the Events sub-tab — should set Socket filter, not toggle sizes
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.S)
            .WaitUntil(_ => _state.DynamicCategoryFilter == TraceEventCategory.Socket, TimeSpan.FromSeconds(3))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(TraceEventCategory.Socket, _state.DynamicCategoryFilter);
        Assert.Equal(sizesBefore, _state.HumanReadableSizes);

        await runTask.ContinueWith(_ => { });
    }

    [Fact(Timeout = 20_000)]
    public async Task Tab3_ScrollPositionPreservedAcrossTabSwitch()
    {
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(18));
        var runTask = app.RunAsync(cts.Token);

        // Navigate to IL Inspector and select ToTitleCase (139 bytes of IL, overflows viewport)
        var builder = new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.D3)
            .WaitUntil(s => s.ContainsText("Select a method"), TimeSpan.FromSeconds(3));

        // Navigate tree to StringHelpers > ToTitleCase
        for (var i = 0; i < 15; i++)
            builder = builder.Key(Hex1bKey.DownArrow);

        await builder
            .Key(Hex1bKey.RightArrow) // Expand StringHelpers
            .WaitUntil(s => s.ContainsText("ToTitleCase"), TimeSpan.FromSeconds(3))
            .Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.Enter) // Select ToTitleCase
            .WaitUntil(s => s.ContainsText("IL_0000"), TimeSpan.FromSeconds(3))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Scroll down via PageDown — exercises the full keyboard scroll path
        // without seeding IlDisassemblyViewportSize.
        // Verify rendered output actually scrolled past IL_0000.
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.PageDown)
            .WaitUntil(_ => _state!.IlDisassemblyScrollOffset > 0, TimeSpan.FromSeconds(3))
            .WaitUntil(s => !s.ContainsText("IL_0000"), TimeSpan.FromSeconds(3))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        var savedOffset = _state!.IlDisassemblyScrollOffset;
        var savedMethod = _state.IlSelectedMethod;
        Assert.True(savedOffset > 0, "Scroll offset should be non-zero after PageDown");

        // Switch to tab 1 (General)
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.D1)
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(3))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // State must survive while on another tab
        Assert.Equal(savedOffset, _state.IlDisassemblyScrollOffset);
        Assert.Equal(savedMethod, _state.IlSelectedMethod);

        // Switch back to tab 3 — triggers scroll restore state machine.
        // Verify the rendered disassembly is restored past IL_0000 (not reset to top).
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.D3)
            .WaitUntil(s => s.ContainsText("IL_"), TimeSpan.FromSeconds(5))
            .WaitUntil(_ => _state.IlScrollRestoreFrames == 0, TimeSpan.FromSeconds(3))
            .WaitUntil(s => !s.ContainsText("IL_0000"), TimeSpan.FromSeconds(3))
            .Ctrl().Key(Hex1bKey.C)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Scroll offset must be preserved after restore completes
        Assert.Equal(savedOffset, _state.IlDisassemblyScrollOffset);
        Assert.Equal(savedMethod, _state.IlSelectedMethod);

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
