using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Dotsider.Tests;

[Collection("SampleAssemblies")]
public class NuGetModeYankIntegrationTests(SampleAssemblyFixture samples) : IDisposable
{
    private Hex1bAppWorkloadAdapter? _workload;
    private ClipboardCapturingWorkloadAdapter? _clipboardAdapter;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _hex1bApp;
    private NuGetState? _state;
    private CancellationTokenSource? _cts;

    private (Hex1bTerminal terminal, Hex1bApp app, CancellationToken ct) Launch()
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _workload = new Hex1bAppWorkloadAdapter();
        _clipboardAdapter = new ClipboardCapturingWorkloadAdapter(_workload);
        _terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(_workload)
            .WithHeadless()
            .WithDimensions(120, 30)
            .WithMouse()
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
                WorkloadAdapter = _clipboardAdapter,
                EnableInputCoalescing = false,
                EnableMouse = true
            });
        return (_terminal, _hex1bApp, _cts.Token);
    }

    // --- Package browser ---

    [Fact(Timeout = 30_000)]
    public async Task Browser_InitialFocusOnFirstDllRow()
    {
        var (terminal, app, ct) = Launch();
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.NotNull(_state!.FileTreeFocusedKey);
        Assert.False(_state.App.FocusedNode is EditorNode,
            "Initial focus should be on table, not editor");

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact(Timeout = 30_000)]
    public async Task Browser_TabTogglesFocus()
    {
        var (terminal, app, ct) = Launch();
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Tab → Package Info editor
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ =>
            {
                try { return _state!.App.FocusedNode is EditorNode; }
                catch (NullReferenceException) { return false; }
            }, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Tab → back to table
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ =>
            {
                try { return _state!.App.FocusedNode is not EditorNode; }
                catch (NullReferenceException) { return false; }
            }, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact(Timeout = 30_000)]
    public async Task Browser_YankOnDllRow_ShowsNotificationAndFlash()
    {
        var (terminal, app, ct) = Launch();
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary.dll"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Yank the focused DLL row — payload should be the file path
        Assert.NotNull(_state!.FileTreeFocusedKey);
        var expectedPath = _state.FileTreeFocusedKey as string;
        Assert.NotNull(expectedPath);

        await new Hex1bTerminalInputSequenceBuilder()
            .Type("y")
            .WaitUntil(s => s.ContainsText("Yanked:"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.NotNull(_state.YankNotification);
        Assert.Contains("lib/", _state.YankNotification); // DLL path contains lib/ directory

        // Verify the actual OSC 52 clipboard payload emitted by ctx.CopyToClipboard
        Assert.True(_clipboardAdapter!.ClipboardWrites.TryDequeue(out var actualClipboard),
            "CopyToClipboard should have emitted an OSC 52 sequence");
        Assert.Equal(expectedPath, actualClipboard);

        // Flash should have fired and cleared
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(_ => !_state.YankFlashRow, TimeSpan.FromSeconds(3))
            .Build()
            .ApplyAsync(terminal, ct);

        // Notification auto-clears
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(_ => _state.YankNotification is null, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- DLL inspector ---

    [Fact(Timeout = 30_000)]
    public async Task DrillInto_SavesFocusedKey_EscRestores()
    {
        var (terminal, app, ct) = Launch();
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary.dll"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        var savedKey = _state!.FileTreeFocusedKey;
        Assert.NotNull(savedKey);

        // Enter → drill into DLL
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => !_state.IsBrowsingPackage, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.Equal(savedKey, _state.SavedFileTreeFocusedKey);

        // Esc → back to package
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => _state.IsBrowsingPackage, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.Equal(savedKey, _state.FileTreeFocusedKey);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Child input routing ---

    [Fact(Timeout = 30_000)]
    public async Task ChildSearch_DigitsDoNotSwitchTabs()
    {
        var (terminal, app, ct) = Launch();
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary.dll"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Enter) // Drill into DLL
            .WaitUntil(_ => !_state!.IsBrowsingPackage, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        var dllState = _state!.SelectedDllState!;

        // Open search in the DLL inspector
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.OemQuestion) // /
            .WaitUntil(_ => dllState.Search[dllState.CurrentTab].IsActive, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        var tabBefore = dllState.CurrentTab;

        // Type digits — should go into search, not switch tabs
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("123")
            .Build()
            .ApplyAsync(terminal, ct);

        await Task.Delay(100, ct);
        Assert.Equal(tabBefore, dllState.CurrentTab);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Hex Escape chain ---

    [Fact(Timeout = 30_000)]
    public async Task HexDump_EscFromNormalMode_ReturnsToPackage()
    {
        var (terminal, app, ct) = Launch();
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary.dll"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Enter) // Drill into DLL
            .WaitUntil(_ => !_state!.IsBrowsingPackage, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        var dllState = _state!.SelectedDllState!;

        // Go to Hex Dump tab
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.D5) // Tab 5 = Hex Dump
            .WaitUntil(_ => dllState.CurrentTab == TabId.HexDump, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Data Interpretation"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Wait for bindings to re-register after tab switch
        await Task.Delay(200, ct);

        // Enter insert mode
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.I)
            .WaitUntil(_ => dllState.HexMode == HexEditMode.Insert, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.False(dllState.HexEditorState.IsReadOnly);

        // Esc 1: exit insert mode (NOT back to package)
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => dllState.HexMode == HexEditMode.Normal, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.True(dllState.HexEditorState.IsReadOnly);
        Assert.False(_state.IsBrowsingPackage, "Should still be in DLL inspector");

        // Esc 2: back to package
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => _state.IsBrowsingPackage, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Yank timer race ---

    [Fact(Timeout = 30_000)]
    public async Task YankTimerRace_LeaveDllBeforeFlashClears_NoException()
    {
        var (terminal, app, ct) = Launch();
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary.dll"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Enter) // Drill into DLL
            .WaitUntil(_ => !_state!.IsBrowsingPackage, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Yank a row in the DLL inspector
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("y")
            .Build()
            .ApplyAsync(terminal, ct);

        // Immediately go back to package (within 150ms flash window)
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => _state!.IsBrowsingPackage, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Wait longer than the flash timer to ensure no exception
        await Task.Delay(300, ct);

        // App should still be running fine
        Assert.True(_state!.IsBrowsingPackage);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Package Info editor yank ---

    [Fact(Timeout = 30_000)]
    public async Task PackageInfo_SelectionYank_Works()
    {
        var (terminal, app, ct) = Launch();
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary"), TimeSpan.FromSeconds(10))
            // Tab to Package Info editor
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ =>
            {
                try { return _state!.App.FocusedNode is EditorNode; }
                catch (NullReferenceException) { return false; }
            }, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Select text
        await new Hex1bTerminalInputSequenceBuilder()
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.PackageInfoEditorState!.Cursor.HasSelection, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .Type("y")
            .WaitUntil(s => s.ContainsText("Yanked:"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.NotNull(_state!.YankNotification);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Package Info double-click selection + yank ---

    [Fact(Timeout = 60_000)]
    public async Task PackageInfo_DoubleClickWordSelectionYank_Works()
    {
        var (terminal, app, ct) = Launch();
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Find "Dotsider" on screen (inside Package Info editor — Authors line)
        List<(int Line, int Column)> matches = [];
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s =>
            {
                var found = s.FindText("Dotsider");
                if (found.Count == 0) return false;
                matches = [.. found.Select(m => (m.Line, m.Column))];
                return true;
            }, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.True(matches.Count > 0);
        var (row, col) = matches[0];

        // Click to focus editor, then double-click to select word.
        // Each Automator step completes through the input pipeline before the next.
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(5));
        await auto.ClickAtAsync(col, row, ct: ct);
        await auto.DoubleClickAtAsync(col, row, ct: ct);

        // Wait for selection via screen state (not internal state polling)
        await auto.WaitUntilAsync(
            _ => _state!.PackageInfoEditorState?.Cursor.HasSelection == true,
            description: "editor word selection after double-click");

        // Yank
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("y")
            .WaitUntil(s => s.ContainsText("Yanked:"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.NotNull(_state!.YankNotification);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- DLL inspector editor yank ---

    [Fact(Timeout = 30_000)]
    public async Task DllInspector_EditorYank_Works()
    {
        var (terminal, app, ct) = Launch();
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary.dll"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Enter) // Drill into DLL
            .WaitUntil(_ => !_state!.IsBrowsingPackage, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            // Tab to Assembly Info editor
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ =>
            {
                try { return _state!.App.FocusedNode is EditorNode; }
                catch (NullReferenceException) { return false; }
            }, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Select and yank
        await new Hex1bTerminalInputSequenceBuilder()
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .Type("y")
            .WaitUntil(s => s.ContainsText("Yanked:"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Hex jump dialog ---

    [Fact(Timeout = 30_000)]
    public async Task HexJumpDialog_DigitsGoIntoInput_EscCloses()
    {
        var (terminal, app, ct) = Launch();
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary.dll"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Enter) // Drill into DLL
            .WaitUntil(_ => !_state!.IsBrowsingPackage, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        var dllState = _state!.SelectedDllState!;

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.D5) // Hex Dump
            .WaitUntil(_ => dllState.CurrentTab == TabId.HexDump, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Data Interpretation"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        await Task.Delay(200, ct);

        // Open jump dialog
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("g")
            .WaitUntil(_ => dllState.HexJumpDialogOpen, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.True(dllState.HexJumpDialogOpen);
        Assert.False(_state.IsBrowsingPackage, "Should still be in DLL inspector");

        // Esc closes dialog
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => !dllState.HexJumpDialogOpen, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.False(_state.IsBrowsingPackage, "Should still be in DLL inspector after closing dialog");

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Child input suppression: q/y ---

    [Fact(Timeout = 30_000)]
    public async Task ChildSearch_QDoesNotQuit()
    {
        var (terminal, app, ct) = Launch();
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary.dll"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Enter) // Drill into DLL
            .WaitUntil(_ => !_state!.IsBrowsingPackage, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        var dllState = _state!.SelectedDllState!;

        // Open search
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.OemQuestion)
            .WaitUntil(_ => dllState.Search[dllState.CurrentTab].IsActive, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Press q — should go into search box, not quit
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("q")
            .Build()
            .ApplyAsync(terminal, ct);

        await Task.Delay(200, ct);

        // App should still be running (not quit)
        Assert.Contains("q", dllState.Search[dllState.CurrentTab].Query ?? "");

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Full hex Esc chain: insert → search dismiss → back to package ---

    [Fact(Timeout = 30_000)]
    public async Task HexEscChain_InsertThenSearchThenBack()
    {
        var (terminal, app, ct) = Launch();
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary.dll"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => !_state!.IsBrowsingPackage, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        var dllState = _state!.SelectedDllState!;

        // Go to Hex Dump
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.D5)
            .WaitUntil(_ => dllState.CurrentTab == TabId.HexDump, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("Data Interpretation"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);
        await Task.Delay(200, ct);

        // Enter insert mode
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.I)
            .WaitUntil(_ => dllState.HexMode == HexEditMode.Insert, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.False(dllState.HexEditorState.IsReadOnly);

        // Esc 1: exit insert mode
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => dllState.HexMode == HexEditMode.Normal, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.True(dllState.HexEditorState.IsReadOnly);
        Assert.False(_state.IsBrowsingPackage);

        // Start a search
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.OemQuestion)
            .WaitUntil(_ => dllState.Search[TabId.HexDump].IsActive, TimeSpan.FromSeconds(5))
            .Type("MZ")
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => dllState.Search[TabId.HexDump].IsConfirmed, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Esc 2: dismiss confirmed search with hex cleanup
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => !dllState.Search[TabId.HexDump].IsActive, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.Empty(dllState.HexMatchOffsets);
        Assert.False(_state.IsBrowsingPackage);

        // Esc 3: back to package
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => _state.IsBrowsingPackage, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Child input suppression: y ---

    [Fact(Timeout = 30_000)]
    public async Task ChildSearch_YDoesNotYank()
    {
        var (terminal, app, ct) = Launch();
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary.dll"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => !_state!.IsBrowsingPackage, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        var dllState = _state!.SelectedDllState!;

        // Open search
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.OemQuestion)
            .WaitUntil(_ => dllState.Search[dllState.CurrentTab].IsActive, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Press y — should go into search box, not yank
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("y")
            .Build()
            .ApplyAsync(terminal, ct);

        await Task.Delay(200, ct);

        Assert.Contains("y", dllState.Search[dllState.CurrentTab].Query ?? "");
        Assert.Null(_state.YankNotification);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- DLL inspector row yank flash ---

    [Fact(Timeout = 30_000)]
    public async Task DllInspector_RowYank_FlashSetsAndClears()
    {
        var (terminal, app, ct) = Launch();
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary.dll"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Enter) // Drill into DLL
            .WaitUntil(_ => !_state!.IsBrowsingPackage, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        var dllState = _state!.SelectedDllState!;

        // Yank
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("y")
            .Build()
            .ApplyAsync(terminal, ct);

        // Flash should clear
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(_ => !dllState.YankFlashRow, TimeSpan.FromSeconds(3))
            .Build()
            .ApplyAsync(terminal, ct);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact(Timeout = 30_000)]
    public async Task PackageInfo_YY_YanksCurrentLine()
    {
        var (terminal, app, ct) = Launch();
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary"), TimeSpan.FromSeconds(10))
            // Tab to focus the Package Info editor
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ =>
            {
                try { return _state!.App.FocusedNode is EditorNode; }
                catch (NullReferenceException) { return false; }
            }, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // yy to yank the current line
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("y")
            .Type("y")
            .WaitUntil(s => s.ContainsText("Yanked"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.True(_clipboardAdapter!.ClipboardWrites.TryDequeue(out var yankedText),
            "CopyToClipboard should have emitted an OSC 52 sequence");

        Assert.True(yankedText.Length > 0);
        Assert.DoesNotContain("\n", yankedText);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _state?.Dispose();
        _hex1bApp?.Dispose();
        _terminal?.Dispose();
        _workload?.Dispose();
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
