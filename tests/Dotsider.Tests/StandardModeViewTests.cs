using Dotsider.Core.Analysis.Models;
using Dotsider.Views;
using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
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

    [Fact(Timeout = 30_000)]
    public async Task App_Launches_ShowsAssemblyName()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab2_ShowsMetadata()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Type("2") // Key 2 → PE/Metadata (TabId 1)
            .WaitUntil(s => s.ContainsText("Sections") || s.ContainsText("TypeDef"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab3_ShowsIlInspector()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D3) // Tab 3 — IL Inspector
            .WaitUntil(s => s.ContainsText("Select a method") || s.ContainsText("IL"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab4_ShowsStrings()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D4) // Tab 4 — Strings
            .WaitUntil(s => s.ContainsText("Offset") || s.ContainsText("Value") || s.ContainsText("User Strings"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab5_ShowsHexDump()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D5) // Key 5 → Hex Dump (TabId 4)
            .WaitUntil(s => s.ContainsText("4D 5A") || s.ContainsText("00000"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab5_StartsInNormalMode_ReadOnly()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D5)
            .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(HexEditMode.Normal, _state!.HexMode);
        Assert.True(_state.HexEditorState.IsReadOnly);
        Assert.False(_state.HexIsDirty);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab5_IKey_EntersInsertMode()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D5)
            .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.I)
            .WaitUntil(s => s.ContainsText("INSERT"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        await Task.Delay(100, cts.Token);
        Assert.Equal(HexEditMode.Insert, _state!.HexMode);
        Assert.False(_state.HexEditorState.IsReadOnly);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab5_EscFromInsert_ReturnsToNormal()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D5)
            .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.I)
            .WaitUntil(s => s.ContainsText("INSERT"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Escape)
            .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(HexEditMode.Normal, _state!.HexMode);
        Assert.True(_state.HexEditorState.IsReadOnly);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab5_EscFromInsert_WithConfirmedSearch_ExitsInsertFirst()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D5)
            .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(10))
            // Start a search and confirm it
            .Key(Hex1bKey.OemQuestion) // '/' — activate search
            .WaitUntil(_ => _state!.Search[TabId.HexDump].IsActive, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.M)
            .Key(Hex1bKey.Z)
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.Search[TabId.HexDump].IsConfirmed, TimeSpan.FromSeconds(10))
            // Enter insert mode
            .Key(Hex1bKey.I)
            .WaitUntil(s => s.ContainsText("INSERT"), TimeSpan.FromSeconds(10))
            // First Esc should exit insert mode, NOT dismiss search
            .Key(Hex1bKey.Escape)
            .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Insert mode must be exited
        Assert.Equal(HexEditMode.Normal, _state!.HexMode);
        Assert.True(_state.HexEditorState.IsReadOnly);
        // Search should still be active (not dismissed by this Esc)
        Assert.True(_state.Search[TabId.HexDump].IsActive);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab5_NormalMode_VimKeysNavigate()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D5)
            .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        var cursorBefore = _state!.HexEditorState.Cursor.Position;

        // Press 'l' to move right in normal mode
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.L)
            .WaitUntil(_ => _state.HexEditorState.Cursor.Position != cursorBefore, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.NotEqual(cursorBefore, _state.HexEditorState.Cursor.Position);
        // Document should NOT be modified — we're in normal mode
        Assert.False(_state.HexIsDirty);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab5_InsertMode_SKey_DoesNotToggleSize()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D5)
            .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.I)
            .WaitUntil(s => s.ContainsText("INSERT"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        var sizesBefore = _state!.HumanReadableSizes;

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.S)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(sizesBefore, _state.HumanReadableSizes);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab5_InsertMode_QKey_DoesNotQuit()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D5)
            .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.I)
            .WaitUntil(s => s.ContainsText("INSERT"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Q) // Should NOT quit — we're in insert mode
            .Ctrl().Key(Hex1bKey.C) // This quits
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // If Q had quit, runTask would already be completed before Ctrl+C
        // The fact that we reach here means the app was still running
        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab5_InsertMode_NumberKeys_DoNotSwitchTabs()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D5)
            .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.I)
            .WaitUntil(s => s.ContainsText("INSERT"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D1) // Should NOT switch to tab 1
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(TabId.HexDump, _state!.CurrentTab);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab5_NormalMode_NoInsertIndicator()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D5)
            .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(10))
            // Verify normal mode does not show INSERT indicator
            .WaitUntil(s => !s.ContainsText("INSERT"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(HexEditMode.Normal, _state!.HexMode);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab5_CtrlS_SavesWithCorrectFileName()
    {
        // Work on a disposable copy so we don't modify the shared fixture assembly
        var tempDir = Path.Combine(Path.GetTempPath(), $"dotsider-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var tempDll = Path.Combine(tempDir, "HelloWorld.dll");
        File.Copy(samples.HelloWorldDll, tempDll);

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            var (terminal, app) = CreateDotsiderApp(tempDll);
            var runTask = app.RunAsync(cts.Token);
            await Task.Delay(100, cts.Token);

            await new Hex1bTerminalInputSequenceBuilder()
                .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
                .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
                .Key(Hex1bKey.D5)
                .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(10))
                // Enter insert mode, skip past MZ header into DOS stub padding,
                // then type two nibbles to complete a byte edit without breaking PE
                .Key(Hex1bKey.I)
                .WaitUntil(s => s.ContainsText("INSERT"), TimeSpan.FromSeconds(10))
                .Key(Hex1bKey.RightArrow).Key(Hex1bKey.RightArrow)
                .Key(Hex1bKey.RightArrow).Key(Hex1bKey.RightArrow)
                .Key(Hex1bKey.F)
                .Key(Hex1bKey.F)
                .WaitUntil(_ => _state!.HexIsDirty, TimeSpan.FromSeconds(10))
                // Return to normal mode, then save
                .Key(Hex1bKey.Escape)
                .WaitUntil(s => s.ContainsText("i: Edit"), TimeSpan.FromSeconds(10))
                .Ctrl().Key(Hex1bKey.S)
                .WaitUntil(_ => _state!.HexNotification != null, TimeSpan.FromSeconds(10))
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

            cts.Cancel();
        await runTask;
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab6_ShowsDepGraph()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D6) // Tab 6 — Dep Graph
            .WaitUntil(s => s.ContainsText("Newtonsoft") || s.ContainsText("System.Runtime"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab7_ShowsSizeMap()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D7) // Tab 7 — Size Map
            .WaitUntil(s => !s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab6_ShowsNodeAndEdgeCounts()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D6)
            .WaitUntil(s => s.ContainsText("Nodes:") && s.ContainsText("Edges:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.NotNull(_state!.CachedGraph);
        Assert.True(_state.CachedGraph.Value.Nodes.Count > 0);
        Assert.True(_state.CachedGraph.Value.Edges.Count > 0);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab6_SearchShowsMatchCount()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D6)
            .WaitUntil(s => s.ContainsText("Nodes:"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.OemQuestion) // '/' — activate search
            .WaitUntil(_ => _state!.Search[TabId.DepGraph].IsActive, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.S).Key(Hex1bKey.Y).Key(Hex1bKey.S) // "sys"
            .Key(Hex1bKey.Enter) // Confirm
            .WaitUntil(_ => _state!.Search[TabId.DepGraph].IsConfirmed, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.True(_state!.Search[TabId.DepGraph].MatchCount > 0,
            "Search for 'sys' should match System.* dependencies");

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab6_MatchNavigation_CyclesGraphSelectedNode()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        //Navigate to dep graph and search for "sys"
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D6)
            .WaitUntil(s => s.ContainsText("Nodes:"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.OemQuestion)
            .WaitUntil(_ => _state!.Search[TabId.DepGraph].IsActive, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.S).Key(Hex1bKey.Y).Key(Hex1bKey.S)
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.Search[TabId.DepGraph].IsConfirmed, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Press 'n' to navigate to first match
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.N)
            .WaitUntil(_ => _state!.GraphMatchIndex >= 0, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        var firstIndex = _state!.GraphMatchIndex;
        Assert.True(firstIndex >= 0);

        // Press 'n' again — should advance to next match
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.N)
            .WaitUntil(_ => _state.GraphMatchIndex != firstIndex
                            || _state.Search[TabId.DepGraph].MatchCount == 1,
                TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        if (_state.Search[TabId.DepGraph].MatchCount > 1)
            Assert.NotEqual(firstIndex, _state.GraphMatchIndex);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab6_ArrowKeys_WorkAfterSearchConfirm()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        //Navigate to dep graph, search for "sys", confirm
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D6)
            .WaitUntil(s => s.ContainsText("Nodes:"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.OemQuestion)
            .WaitUntil(_ => _state!.Search[TabId.DepGraph].IsActive, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.S).Key(Hex1bKey.Y).Key(Hex1bKey.S)
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.Search[TabId.DepGraph].IsConfirmed, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Arrow keys should work immediately — focus restored to Interactable
        Assert.Equal(-1, _state!.GraphSelectedIndex);

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.GraphSelectedIndex == 0, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(0, _state.GraphSelectedIndex);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab7_ArrowKeys_WorkAfterSearchConfirm()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        //Navigate to Size Map, search for "rich", confirm
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D7)
            .WaitUntil(s => s.ContainsText("Total:"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.OemQuestion)
            .WaitUntil(_ => _state!.Search[TabId.SizeMap].IsActive, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.R).Key(Hex1bKey.I).Key(Hex1bKey.C).Key(Hex1bKey.H)
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.Search[TabId.SizeMap].IsConfirmed, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Arrow keys should work immediately — focus restored to Interactable
        Assert.Equal(-1, _state!.TreemapSelectedIndex);

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.TreemapSelectedIndex == 0, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(0, _state.TreemapSelectedIndex);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab6_StartupFocus_ArrowKeysWorkWithoutTabSwitch()
    {
        // Start directly on Dep Graph tab — tests the initial focus predicate
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll, initialTab: TabId.DepGraph);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Nodes:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(-1, _state!.GraphSelectedIndex);

        // Arrow keys should work immediately without switching tabs first
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.GraphSelectedIndex == 0, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(0, _state.GraphSelectedIndex);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab7_StartupFocus_ArrowKeysWorkWithoutTabSwitch()
    {
        // Start directly on Size Map tab — tests the initial focus predicate
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll, initialTab: TabId.SizeMap);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Total:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(-1, _state!.TreemapSelectedIndex);

        // Arrow keys should work immediately without switching tabs first
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.TreemapSelectedIndex == 0, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(0, _state.TreemapSelectedIndex);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab6_ArrowKeys_CycleSelectedIndex()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        //Navigate to dep graph tab
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D6)
            .WaitUntil(s => s.ContainsText("Nodes:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(-1, _state!.GraphSelectedIndex);

        // Press Right to select first node
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.GraphSelectedIndex == 0, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(0, _state.GraphSelectedIndex);

        // Press Right again — should advance
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state.GraphSelectedIndex == 1, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(1, _state.GraphSelectedIndex);

        // Press Left — should go back
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.LeftArrow)
            .WaitUntil(_ => _state.GraphSelectedIndex == 0, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(0, _state.GraphSelectedIndex);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab7_ArrowKeys_CycleSelectedIndex()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        //Navigate to Size Map tab
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D7)
            .WaitUntil(s => s.ContainsText("Total:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(-1, _state!.TreemapSelectedIndex);

        // Press Right to select first item
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.TreemapSelectedIndex == 0, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(0, _state.TreemapSelectedIndex);

        // Press Right again — should advance
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state.TreemapSelectedIndex == 1, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(1, _state.TreemapSelectedIndex);

        // Press Left — should go back
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.LeftArrow)
            .WaitUntil(_ => _state.TreemapSelectedIndex == 0, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(0, _state.TreemapSelectedIndex);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab7_ShowsBreadcrumbAndTotalSize()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D7)
            .WaitUntil(s => s.ContainsText("RichLibrary") && s.ContainsText("Total:"),
                TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.NotNull(_state!.CachedSizeTree);
        Assert.True(_state.CachedSizeTree.Size > 0);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab7_Backspace_PopsBreadcrumb()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        //Navigate to tab 7, let treemap render
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D7)
            .WaitUntil(s => s.ContainsText("RichLibrary") && s.ContainsText("Total:"),
                TimeSpan.FromSeconds(10))
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
            .WaitUntil(s => s.ContainsText(firstChild.Name), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Single(_state.TreemapBreadcrumb);

        // Press Backspace to go up
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Backspace)
            .WaitUntil(_ => _state.TreemapBreadcrumb.Count == 0, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Empty(_state.TreemapBreadcrumb);
        Assert.Equal(root, _state.TreemapCurrentLevel);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab7_SearchMatchNavigation_UpdatesHoveredItem()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        //Navigate to tab 7 and search for a namespace
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D7)
            .WaitUntil(s => s.ContainsText("Total:"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.OemQuestion) // '/' — activate search
            .WaitUntil(_ => _state!.Search[TabId.SizeMap].IsActive, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.R).Key(Hex1bKey.I).Key(Hex1bKey.C).Key(Hex1bKey.H) // "rich"
            .Key(Hex1bKey.Enter) // Confirm
            .WaitUntil(_ => _state!.Search[TabId.SizeMap].IsConfirmed, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.True(_state!.Search[TabId.SizeMap].MatchCount > 0,
            "Search for 'rich' should match RichLibrary namespace");

        // Press 'n' to navigate to first match
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.N)
            .WaitUntil(_ => _state.TreemapMatchIndex >= 0, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.True(_state.TreemapMatchIndex >= 0);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab7_Enter_PrefersSearchMatchOverStaleSelection()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        // Navigate to Size Map, select first item with arrow key
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D7)
            .WaitUntil(s => s.ContainsText("Total:"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.TreemapSelectedIndex == 0, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        await Task.Delay(100, cts.Token);
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
            cts.Cancel();
            await runTask;
            return;
        }

        // Search for the non-zero child, confirm, navigate to match
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.OemQuestion)
            .WaitUntil(_ => _state.Search[TabId.SizeMap].IsActive, TimeSpan.FromSeconds(10))
            .Type(searchTerm)
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state.Search[TabId.SizeMap].IsConfirmed, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        if (_state.Search[TabId.SizeMap].MatchCount == 0)
        {
            cts.Cancel();
            await runTask;
            return;
        }

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.N)
            .WaitUntil(_ => _state.TreemapMatchIndex >= 0, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        await Task.Delay(100, cts.Token);

        // Stale selection is still 0, but search match points elsewhere
        Assert.Equal(0, _state.TreemapSelectedIndex);
        Assert.True(_state.TreemapMatchIndex >= 0);

        // Press Enter — should drill into search match, not stale selection at index 0
        var previousLevel = _state.TreemapCurrentLevel;
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state.TreemapCurrentLevel != previousLevel
                            || _state.TreemapBreadcrumb.Count > 0, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        await Task.Delay(100, cts.Token);

        // Verify we drilled into the search match (name contains query), not child 0
        if (_state.TreemapCurrentLevel != previousLevel)
        {
            Assert.Contains(searchTerm, _state.TreemapCurrentLevel!.Name,
                StringComparison.OrdinalIgnoreCase);
        }

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab8_Library_ShowsNoEntryPoint()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D8) // Tab 8 — Dynamic
            .WaitUntil(s => s.ContainsText("entry point") || s.ContainsText("library"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab8_Exe_ShowsLaunchPrompt()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D8) // Tab 8 — Dynamic
            .WaitUntil(s => s.ContainsText("Enter") || s.ContainsText("Launch") || s.ContainsText("EventPipe"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab8_Exe_IdleView_ShowsAssemblyInfoAndProviders()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        //Verify idle view shows assembly info and provider list
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D8)
            .WaitUntil(s => s.ContainsText("EventPipe"), TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly:"), TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Entry Point:"), TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Providers:"), TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("CLR Runtime"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Null(_state!.Tracer);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 60_000)]
    public async Task Tab8_SubTabNavigation_ArrowKeysCycle()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        //Launch process and wait for exit so sub-tabs are visible
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D8)
            .WaitUntil(s => s.ContainsText("EventPipe") || s.ContainsText("Launch"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.Tracer?.ProcessState
                is TraceProcessState.Exited or TraceProcessState.Error, TimeSpan.FromSeconds(30))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Starts on Events sub-tab
        Assert.Equal(DynamicSubTabId.Events, _state!.DynamicSubTab);

        // Right → Counters
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.DynamicSubTab == DynamicSubTabId.Counters, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(DynamicSubTabId.Counters, _state.DynamicSubTab);

        // Right → Output
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state.DynamicSubTab == DynamicSubTabId.Output, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(DynamicSubTabId.Output, _state.DynamicSubTab);

        // Right → Summary
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state.DynamicSubTab == DynamicSubTabId.Summary, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(DynamicSubTabId.Summary, _state.DynamicSubTab);

        // Right at max → stays on Summary (no wrap)
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(DynamicSubTabId.Summary, _state.DynamicSubTab);

        // Left → back to Output
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.LeftArrow)
            .WaitUntil(_ => _state.DynamicSubTab == DynamicSubTabId.Output, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(DynamicSubTabId.Output, _state.DynamicSubTab);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 60_000)]
    public async Task Tab8_CategoryFilterKeys_UpdateState()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        //Launch, wait for exit, stay on Events sub-tab
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D8)
            .WaitUntil(s => s.ContainsText("EventPipe") || s.ContainsText("Launch"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.Tracer?.ProcessState
                is TraceProcessState.Exited or TraceProcessState.Error, TimeSpan.FromSeconds(30))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Null(_state!.DynamicCategoryFilter);

        // g → GC filter
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.G)
            .WaitUntil(_ => _state!.DynamicCategoryFilter == TraceEventCategory.GC, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(TraceEventCategory.GC, _state.DynamicCategoryFilter);

        // j → JIT filter
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.J)
            .WaitUntil(_ => _state.DynamicCategoryFilter == TraceEventCategory.JIT, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(TraceEventCategory.JIT, _state.DynamicCategoryFilter);

        // e → Exception filter
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.E)
            .WaitUntil(_ => _state.DynamicCategoryFilter == TraceEventCategory.Exception, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(TraceEventCategory.Exception, _state.DynamicCategoryFilter);

        // l → Loader filter
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.L)
            .WaitUntil(_ => _state.DynamicCategoryFilter == TraceEventCategory.Loader, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(TraceEventCategory.Loader, _state.DynamicCategoryFilter);

        // t → Threading filter
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.T)
            .WaitUntil(_ => _state.DynamicCategoryFilter == TraceEventCategory.Threading, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(TraceEventCategory.Threading, _state.DynamicCategoryFilter);

        // h → HTTP filter
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.H)
            .WaitUntil(_ => _state.DynamicCategoryFilter == TraceEventCategory.Http, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(TraceEventCategory.Http, _state.DynamicCategoryFilter);

        // Esc → clears filter
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => _state.DynamicCategoryFilter is null, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Null(_state.DynamicCategoryFilter);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 60_000)]
    public async Task Tab8_CtrlK_StopsRunningProcess()
    {
        // MinimalApi is a web server that stays alive until killed,
        // so Ctrl+K is the only way to reach Exited within the timeout.
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.MinimalApiDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        //Navigate to Dynamic tab and launch
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D8)
            .WaitUntil(s => s.ContainsText("EventPipe") || s.ContainsText("Launch"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.Tracer?.ProcessState == TraceProcessState.Running,
                TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.NotNull(_state!.Tracer);
        Assert.Equal(TraceProcessState.Running, _state.Tracer!.ProcessState);

        // Ctrl+K to stop — the web server would run indefinitely without this
        await new Hex1bTerminalInputSequenceBuilder()
            .Ctrl().Key(Hex1bKey.K)
            .WaitUntil(_ => _state.Tracer!.ProcessState
                is TraceProcessState.Exited or TraceProcessState.Error,
                TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.True(_state.Tracer!.ProcessState
            is TraceProcessState.Exited or TraceProcessState.Error);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 60_000)]
    public async Task Tab8_Enter_RerunsAfterExit()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        //Launch and wait for process to finish (Exited or Error)
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D8)
            .WaitUntil(s => s.ContainsText("EventPipe") || s.ContainsText("Launch"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.Tracer?.ProcessState
                is TraceProcessState.Exited or TraceProcessState.Error, TimeSpan.FromSeconds(30))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        var firstTracer = _state!.Tracer;
        Assert.NotNull(firstTracer);

        // Press Enter to re-run
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state.Tracer != firstTracer, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // A new tracer was created
        Assert.NotNull(_state.Tracer);
        Assert.NotEqual(firstTracer, _state.Tracer);

        // Wait for the re-run to exit successfully
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(_ => _state.Tracer!.ProcessState == TraceProcessState.Exited,
                TimeSpan.FromSeconds(15))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(TraceProcessState.Exited, _state.Tracer!.ProcessState);
        Assert.Equal(0, _state.Tracer.ExitCode);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 60_000)]
    public async Task Tab8_SearchAfterProcessExit_NoGlobalBindingConflict()
    {
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        // Navigate to Dynamic tab and launch the process
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D8)
            .WaitUntil(s => s.ContainsText("EventPipe") || s.ContainsText("Launch"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Enter) // Launch process
            .WaitUntil(_ => _state!.Tracer?.ProcessState
                is TraceProcessState.Exited or TraceProcessState.Error, TimeSpan.FromSeconds(30))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Process has exited — activating search must not crash with
        // "Global binding conflict: Enter is already registered"
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.OemQuestion) // '/' — activate search
            .WaitUntil(_ => _state!.Search[TabId.Dynamic].IsActive, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        await Task.Delay(100, cts.Token);
        Assert.True(_state!.Search[TabId.Dynamic].IsActive);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task General_EnterOnReference_DrillsIntoAssembly()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            // Focus starts on the dependency table; DownArrow ensures a row is selected, Enter drills
            .Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.Enter)
            // After drill-down, the title bar should no longer show "HelloWorld.dll"
            .WaitUntil(s => !s.ContainsText("HelloWorld.dll"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab3_ArrowKeysWorkImmediately()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D3) // Tab 3 — IL Inspector
            .WaitUntil(s => s.ContainsText("▶") || s.ContainsText("▼"), TimeSpan.FromSeconds(10))
            // Arrow keys should work immediately without clicking first —
            // DownArrow moves table focus, which toggles expansion on namespace/type rows
            .Key(Hex1bKey.DownArrow)
            .WaitUntil(s => s.ContainsText(".ctor") || s.ContainsText("Main"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab3_DisassemblyPaneScrolls()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        // Navigate to IL tab
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D3)
            .WaitUntil(s => s.ContainsText("▶") || s.ContainsText("▼"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Select ToTitleCase programmatically (139 bytes of IL, overflows viewport)
        var toTitleCase = _state!.Analyzer.MethodDefs.First(m => m.Name == "ToTitleCase");
        var typeDef = _state.Analyzer.TypeDefs.First(t => t.FullName == toTitleCase.DeclaringType);
        var ns = !string.IsNullOrEmpty(typeDef.Namespace) ? typeDef.Namespace : "(global)";
        _state.IlTreeExpansionState[$"ns:{ns}"] = true;
        _state.IlTreeExpansionState[$"type:{toTitleCase.DeclaringType}"] = true;
        _state.IlSelectedMethod = toTitleCase;
        _state.IlFocusedTreeKey = $"method:{toTitleCase.Token}";
        _state.App.Invalidate();

        // Click in the editor to focus it, then PageDown scrolls natively
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("IL_0000"), TimeSpan.FromSeconds(10))
            .ClickAt(50, 15) // Click in editor pane (right of splitter)
            .PageDown()
            .PageDown()
            .PageDown()
            .WaitUntil(s => !s.ContainsText("IL_0000"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab4_ArrowKeysCycleSubTabs()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D4) // Tab 4 — Strings
            .WaitUntil(s => s.ContainsText("User Strings"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Verify starting state
        Assert.Equal(0, _state!.StringsSourceTab);

        // Right arrow → sub-tab 1
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state.StringsSourceTab == 1, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(1, _state.StringsSourceTab);

        // Right arrow → sub-tab 2
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state.StringsSourceTab == 2, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(2, _state.StringsSourceTab);

        // Left arrow → back to sub-tab 1
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.LeftArrow)
            .WaitUntil(_ => _state.StringsSourceTab == 1, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(1, _state.StringsSourceTab);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab4_ArrowKeysDuringSearchEditing_DoNotSwitchSubTab()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D4) // Tab 4 — Strings
            .WaitUntil(s => s.ContainsText("User Strings"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.OemQuestion) // '/' — activate search
            .WaitUntil(_ => _state!.Search[TabId.Strings].IsActive, TimeSpan.FromSeconds(10))
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
            .WaitUntil(_ => !_state.Search[TabId.Strings].IsActive, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state.StringsSourceTab == 1, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(1, _state.StringsSourceTab);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 60_000)]
    public async Task Tab8_Events_SKey_FiltersSocket_NotToggleSize()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        //Navigate to Dynamic tab, launch the process, wait for exit
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D8)
            .WaitUntil(s => s.ContainsText("EventPipe") || s.ContainsText("Launch"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.Tracer?.ProcessState
                is TraceProcessState.Exited or TraceProcessState.Error, TimeSpan.FromSeconds(30))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Record initial size toggle state
        var sizesBefore = _state!.HumanReadableSizes;

        // Press S on the Events sub-tab — should set Socket filter, not toggle sizes
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.S)
            .WaitUntil(_ => _state.DynamicCategoryFilter == TraceEventCategory.Socket, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(TraceEventCategory.Socket, _state.DynamicCategoryFilter);
        Assert.Equal(sizesBefore, _state.HumanReadableSizes);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task Tab3_ScrollPositionPreservedAcrossTabSwitch()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        // Navigate to IL tab
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D3)
            .WaitUntil(s => s.ContainsText("▶") || s.ContainsText("▼"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Select ToTitleCase programmatically (139 bytes of IL, overflows viewport)
        var toTitleCase = _state!.Analyzer.MethodDefs.First(m => m.Name == "ToTitleCase");
        var typeDef = _state.Analyzer.TypeDefs.First(t => t.FullName == toTitleCase.DeclaringType);
        var ns = !string.IsNullOrEmpty(typeDef.Namespace) ? typeDef.Namespace : "(global)";
        _state.IlTreeExpansionState[$"ns:{ns}"] = true;
        _state.IlTreeExpansionState[$"type:{toTitleCase.DeclaringType}"] = true;
        _state.IlSelectedMethod = toTitleCase;
        _state.IlFocusedTreeKey = $"method:{toTitleCase.Token}";
        _state.App.Invalidate();

        // Click in editor to focus it, then scroll down natively via PageDown
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("IL_0000"), TimeSpan.FromSeconds(10))
            .ClickAt(50, 15) // Click in editor pane
            .PageDown()
            .PageDown()
            .WaitUntil(s => !s.ContainsText("IL_0000"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        var savedMethod = _state!.IlSelectedMethod;

        // Switch to tab 1 (General)
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.D1)
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(savedMethod, _state.IlSelectedMethod);

        // Switch back to tab 3 — EditorNode preserved by Responsive, scroll intact
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.D3)
            .WaitUntil(s => s.ContainsText("IL_"), TimeSpan.FromSeconds(10))
            .WaitUntil(s => !s.ContainsText("IL_0000"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(savedMethod, _state.IlSelectedMethod);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 30_000)]
    public async Task QuitKey_ExitsApp()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Q) // q = quit
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // App should exit after q key
        var completed = await Task.WhenAny(runTask, Task.Delay(5000, cts.Token));
        Assert.Equal(runTask, completed);
    }

    [Fact(Timeout = 30_000)]
    public async Task CrossViewBack_SuppressedDuringSearchEditing()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.RichLibraryDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        //Navigate to IL Inspector tab
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D3) // Tab 3 — IL Inspector
            .WaitUntil(s => s.ContainsText("Select a method") || s.ContainsText("IL"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Programmatically set a cross-view back target (simulating a g/x navigation)
        _state!.CrossViewBackTarget = (TabId.PeMetadata, PeSubTabId.TypeDef);
        _hex1bApp!.Invalidate();

        // Wait for "Backspace: Back" hint to appear
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Backspace: Back"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Open search — type "test" then press Backspace
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.OemQuestion) // '/' — activate search
            .WaitUntil(_ => _state.Search[TabId.IlInspector].IsActive, TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.T).Key(Hex1bKey.E).Key(Hex1bKey.S).Key(Hex1bKey.T) // type "test"
            .Key(Hex1bKey.Backspace) // should delete 't', NOT navigate back
            .WaitUntil(_ => _state.Search[TabId.IlInspector].Query == "tes", TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Verify we stayed on IL Inspector — Backspace deleted a character, didn't navigate back
        Assert.Equal(TabId.IlInspector, _state.CurrentTab);
        Assert.Equal("tes", _state.Search[TabId.IlInspector].Query);
        Assert.NotNull(_state.CrossViewBackTarget); // Back target still present

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 60_000)]
    public async Task Tab8_Enter_OnJitEvent_NavigatesToIlInspector()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        //Navigate to Dynamic tab, launch trace, and wait for exit
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D8)
            .WaitUntil(s => s.ContainsText("EventPipe") || s.ContainsText("Launch"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.Tracer?.ProcessState
                is TraceProcessState.Exited or TraceProcessState.Error, TimeSpan.FromSeconds(30))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        var tracer = _state!.Tracer!;

        // HelloWorld defines Formatter.Format(int) and Formatter.Format(string).
        // Both produce JIT events with identical Detail ("Formatter.Format")
        // but distinct MetadataTokens. Deliberately target the SECOND overload
        // so that a name-only regression (FirstOrDefault by DeclaringType+Name)
        // would select the wrong method.
        var formatEvents = tracer.GetEvents()
            .Where(e => e.Category == TraceEventCategory.JIT
                     && e.Detail == "Formatter.Format")
            .ToList();

        Assert.True(formatEvents.Count >= 2,
            $"Expected >=2 Formatter.Format JIT events, got {formatEvents.Count}");

        var firstToken = formatEvents[0].MetadataToken;
        var targetEvent = formatEvents.First(e => e.MetadataToken != firstToken);
        Assert.True(targetEvent.MetadataToken > 0);

        var expectedMethod = _state.Analyzer.MethodDefs
            .FirstOrDefault(m => m.Token == targetEvent.MetadataToken);
        Assert.NotNull(expectedMethod);

        // Verify this IS an overload: name-based FirstOrDefault would return
        // a different method (the first match), proving token is required.
        Assert.True(DynamicAnalysisView.TryParseJitDetail(targetEvent.Detail,
            out var declType, out var methName));
        var byName = _state.Analyzer.MethodDefs
            .FirstOrDefault(m => m.DeclaringType == declType && m.Name == methName);
        Assert.NotNull(byName);

        Assert.NotEqual(expectedMethod.Token, byName.Token);

        // Use J key to set JIT filter (runs on the render thread, not a direct state mutation)
        var eventKey = $"{targetEvent.Timestamp.Ticks}:{targetEvent.EventName}:{targetEvent.Detail}:{targetEvent.MetadataToken}";
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.J)
            .WaitUntil(s => s.ContainsText("Filter: JIT"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Set focused key to the second overload's row, then press Enter
        _state.DynamicEventsFocusedKey = eventKey;
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state.CurrentTab == TabId.IlInspector, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(TabId.IlInspector, _state.CurrentTab);
        Assert.Equal(expectedMethod.Token, _state.IlSelectedMethod!.Token);
        Assert.NotNull(_state.CrossViewBackTarget);
        Assert.Equal(TabId.Dynamic, _state.CrossViewBackTarget.Value.Tab);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 60_000)]
    public async Task Tab8_JitNavigation_HintUpdatesAndEnterNavigatesWithoutRerun()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        // Navigate to Dynamic tab, launch trace, wait for exit
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D8)
            .WaitUntil(s => s.ContainsText("EventPipe") || s.ContainsText("Launch"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.Tracer?.ProcessState
                is TraceProcessState.Exited or TraceProcessState.Error, TimeSpan.FromSeconds(30))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        var tracer = _state!.Tracer!;

        // Status bar should show "Re-run" when no navigable JIT event is focused
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Re-run"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Filter to JIT events
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.J)
            .WaitUntil(s => s.ContainsText("Filter: JIT"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Find a navigable JIT event from the analyzed assembly
        var targetEvent = tracer.GetEvents()
            .First(e => e.Category == TraceEventCategory.JIT
                     && e.Detail == "Formatter.Format"
                     && e.MetadataToken > 0);
        var eventKey = $"{targetEvent.Timestamp.Ticks}:{targetEvent.EventName}:" +
                       $"{targetEvent.Detail}:{targetEvent.MetadataToken}";
        var expectedMethod = _state.Analyzer.MethodDefs
            .First(m => m.Token == targetEvent.MetadataToken);

        // Focus the navigable event — this triggers OnFocusChanged → Invalidate → re-render
        _state.DynamicEventsFocusedKey = eventKey;
        _state.App.Invalidate();

        // Status bar should now show "Go to IL" instead of "Re-run"
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Go to IL"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Press Enter — should navigate to IL Inspector, NOT re-run the trace
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state.CurrentTab == TabId.IlInspector, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.Equal(TabId.IlInspector, _state.CurrentTab);
        Assert.Equal(expectedMethod.Token, _state.IlSelectedMethod!.Token);
        Assert.NotNull(_state.CrossViewBackTarget);
        Assert.Equal(TabId.Dynamic, _state.CrossViewBackTarget.Value.Tab);

        // The tracer must NOT have been replaced — Enter navigated, not re-ran
        Assert.Same(tracer, _state.Tracer);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 60_000)]
    public async Task Tab8_SearchEditing_HintShowsRerunNotGoToIl()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        // Navigate to Dynamic tab, launch trace, wait for exit
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D8)
            .WaitUntil(s => s.ContainsText("EventPipe") || s.ContainsText("Launch"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.Tracer?.ProcessState
                is TraceProcessState.Exited or TraceProcessState.Error, TimeSpan.FromSeconds(30))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        var tracer = _state!.Tracer!;

        // Filter to JIT and focus a navigable event so CanNavigateJitEvent is true
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.J)
            .WaitUntil(s => s.ContainsText("Filter: JIT"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        var targetEvent = tracer.GetEvents()
            .First(e => e.Category == TraceEventCategory.JIT
                     && e.Detail == "Formatter.Format"
                     && e.MetadataToken > 0);
        var eventKey = $"{targetEvent.Timestamp.Ticks}:{targetEvent.EventName}:" +
                       $"{targetEvent.Detail}:{targetEvent.MetadataToken}";

        _state.DynamicEventsFocusedKey = eventKey;
        _state.App.Invalidate();

        // Confirm hint shows "Go to IL" before opening search
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Go to IL"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Open search — Enter now confirms search, not navigates
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.OemQuestion)
            .WaitUntil(_ => _state.Search[TabId.Dynamic].IsActive, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Hint must revert to "Re-run" while search is editing
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Re-run"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Tab must still be Dynamic — Enter did not navigate
        Assert.Equal(TabId.Dynamic, _state.CurrentTab);
        Assert.Same(tracer, _state.Tracer);

        cts.Cancel();
        await runTask;
    }

    [Fact(Timeout = 60_000)]
    public async Task Tab8_EnterDuringSearchEditing_ConfirmsSearchNotNavigates()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var (terminal, app) = CreateDotsiderApp(samples.HelloWorldDll);
        var runTask = app.RunAsync(cts.Token);
        await Task.Delay(100, cts.Token);

        // Navigate to Dynamic tab, launch trace, wait for exit
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D8)
            .WaitUntil(s => s.ContainsText("EventPipe") || s.ContainsText("Launch"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.Tracer?.ProcessState
                is TraceProcessState.Exited or TraceProcessState.Error, TimeSpan.FromSeconds(30))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        var tracer = _state!.Tracer!;

        // Filter to JIT and focus a navigable event
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.J)
            .WaitUntil(s => s.ContainsText("Filter: JIT"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        var targetEvent = tracer.GetEvents()
            .First(e => e.Category == TraceEventCategory.JIT
                     && e.Detail == "Formatter.Format"
                     && e.MetadataToken > 0);
        var eventKey = $"{targetEvent.Timestamp.Ticks}:{targetEvent.EventName}:" +
                       $"{targetEvent.Detail}:{targetEvent.MetadataToken}";
        _state.DynamicEventsFocusedKey = eventKey;
        _state.App.Invalidate();

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Go to IL"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        // Open search and type a query
        var search = _state.Search[TabId.Dynamic];
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.OemQuestion)
            .WaitUntil(_ => search.IsActive, TimeSpan.FromSeconds(10))
            .Type("Format")
            .WaitUntil(_ => search.Query == "Format", TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.True(search.IsActive);
        Assert.False(search.IsConfirmed);

        // Press Enter — should confirm search, NOT navigate to IL
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => search.IsConfirmed, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, cts.Token);

        Assert.True(search.IsConfirmed);
        Assert.Equal("Format", search.Query);
        Assert.Equal(TabId.Dynamic, _state.CurrentTab);
        Assert.Same(tracer, _state.Tracer);

        cts.Cancel();
        await runTask;
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
