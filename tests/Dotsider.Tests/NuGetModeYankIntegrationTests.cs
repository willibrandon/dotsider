using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Nu Get Mode Yank Integration.
/// </summary>
[TestClass]
public class NuGetModeYankIntegrationTests : IDisposable
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private Hex1bAppWorkloadAdapter? _workload;
    private ClipboardCapturingWorkloadAdapter? _clipboardAdapter;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _hex1bApp;
    private NuGetState? _state;
    private CancellationTokenSource? _cts;

    private (Hex1bTerminal terminal, Hex1bApp app, CancellationToken ct) Launch()
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
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
                _state ??= new NuGetState(_hex1bApp!, Samples.RichLibraryNupkg);
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

    /// <summary>
    /// Verifies browser initial focus on first dll row.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Browser_InitialFocusOnFirstDllRow()
    {
        var (terminal, app, ct) = Launch();
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.IsNotNull(_state!.FileTreeFocusedKey);
        Assert.IsFalse(_state.App.FocusedNode is EditorNode,
            "Initial focus should be on table, not editor");

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies browser tab toggles focus.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
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

    /// <summary>
    /// Verifies browser yank on dll row shows notification and flash.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
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
        Assert.IsNotNull(_state!.FileTreeFocusedKey);
        var expectedPath = _state.FileTreeFocusedKey as string;
        Assert.IsNotNull(expectedPath);

        await new Hex1bTerminalInputSequenceBuilder()
            .Type("y")
            .WaitUntil(s => s.ContainsText("Yanked:"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.IsNotNull(_state.YankNotification);
        Assert.Contains("lib/", _state.YankNotification); // DLL path contains lib/ directory

        // Verify the actual OSC 52 clipboard payload emitted by ctx.CopyToClipboard
        Assert.IsTrue(_clipboardAdapter!.ClipboardWrites.TryDequeue(out var actualClipboard),
            "CopyToClipboard should have emitted an OSC 52 sequence");
        Assert.AreEqual(expectedPath, actualClipboard);

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

    /// <summary>
    /// Verifies drill into saves focused key esc restores.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
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
        Assert.IsNotNull(savedKey);

        // Enter → drill into DLL
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => !_state.IsBrowsingPackage, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.AreEqual(savedKey, _state.SavedFileTreeFocusedKey);

        // Esc → back to package
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => _state.IsBrowsingPackage, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.AreEqual(savedKey, _state.FileTreeFocusedKey);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Child input routing ---

    /// <summary>
    /// Verifies child search digits do not switch tabs.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
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
        Assert.AreEqual(tabBefore, dllState.CurrentTab);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Hex Escape chain ---

    /// <summary>
    /// Verifies hex dump esc from normal mode returns to package.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
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

        Assert.IsFalse(dllState.HexEditorState.IsReadOnly);

        // Esc 1: exit insert mode (NOT back to package)
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => dllState.HexMode == HexEditMode.Normal, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.IsTrue(dllState.HexEditorState.IsReadOnly);
        Assert.IsFalse(_state.IsBrowsingPackage, "Should still be in DLL inspector");

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

    /// <summary>
    /// Verifies yank timer race leave dll before flash clears no exception.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
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
        Assert.IsTrue(_state!.IsBrowsingPackage);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Package Info editor yank ---

    /// <summary>
    /// Verifies package info selection yank works.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
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

        Assert.IsNotNull(_state!.YankNotification);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Package Info double-click selection + yank ---

    /// <summary>
    /// Verifies package info double click word selection yank works.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
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

        Assert.IsGreaterThan(0, matches.Count);
        var (row, col) = matches[0];

        // Focus through the real package-browser binding. It executes on Hex1b's input
        // loop, so the pending focus request is consumed by the mandatory next render.
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(5));
        var state = _state!;
        await auto.KeyAsync(Hex1bKey.Tab, ct);
        await auto.WaitUntilAsync(_ =>
            state.App.FocusedNode is EditorNode { State: var es }
                && ReferenceEquals(es, state.PackageInfoEditorState),
            description: "package info editor focused");

        var editorState = state.PackageInfoEditorState!;
        var expectedOffset = editorState.Document.GetText().IndexOf("Dotsider", StringComparison.Ordinal);
        Assert.IsGreaterThanOrEqualTo(0, expectedOffset);

        Hex1bMouseCompatibility.BeginClickSequence(app);
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(col, row)
            .Build()
            .ApplyAsync(terminal, ct);
        await auto.WaitUntilAsync(_ => editorState.Cursor.Position.Value == expectedOffset,
            description: "first click landed on the displayed Dotsider word");

        Hex1bMouseCompatibility.ContinueClickSequence(app);
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(col, row)
            .Build()
            .ApplyAsync(terminal, ct);

        // Wait until the editor reports the word selection produced by the second click.
        await auto.WaitUntilAsync(
            _ => _state!.PackageInfoEditorState?.Cursor.HasSelection == true,
            description: "editor word selection after double-click");

        // Yank
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("y")
            .WaitUntil(s => s.ContainsText("Yanked:"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.IsNotNull(_state!.YankNotification);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- DLL inspector editor yank ---

    /// <summary>
    /// Verifies dll inspector editor yank works.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
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

    /// <summary>
    /// Verifies hex jump dialog digits go into input esc closes.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
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

        Assert.IsTrue(dllState.HexJumpDialogOpen);
        Assert.IsFalse(_state.IsBrowsingPackage, "Should still be in DLL inspector");

        // Esc closes dialog
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => !dllState.HexJumpDialogOpen, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.IsFalse(_state.IsBrowsingPackage, "Should still be in DLL inspector after closing dialog");

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Child input suppression: q/y ---

    /// <summary>
    /// Verifies child search q does not quit.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
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
            .WaitUntil(_ => (dllState.Search[dllState.CurrentTab].Query ?? "").Contains('q'),
                TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // App should still be running (not quit)
        Assert.Contains("q", dllState.Search[dllState.CurrentTab].Query ?? "");

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Full hex Esc chain: insert → search dismiss → back to package ---

    /// <summary>
    /// Verifies hex esc chain insert then search then back.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
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

        Assert.IsFalse(dllState.HexEditorState.IsReadOnly);

        // Esc 1: exit insert mode
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Escape)
            .WaitUntil(_ => dllState.HexMode == HexEditMode.Normal, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.IsTrue(dllState.HexEditorState.IsReadOnly);
        Assert.IsFalse(_state.IsBrowsingPackage);

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

        Assert.IsEmpty(dllState.HexMatchOffsets);
        Assert.IsFalse(_state.IsBrowsingPackage);

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

    /// <summary>
    /// Verifies child search y does not yank.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
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

        // Press y — should go into search box, not yank. Wait deterministically for
        // the typed character to land in the search query so a slow-to-process input
        // event cannot leave the assertion racing the search-bar TextBox update.
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("y")
            .WaitUntil(_ => (dllState.Search[dllState.CurrentTab].Query ?? "").Contains('y'),
                TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.Contains("y", dllState.Search[dllState.CurrentTab].Query ?? "");
        Assert.IsNull(_state.YankNotification);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- DLL inspector row yank flash ---

    /// <summary>
    /// Verifies dll inspector row yank flash sets and clears.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
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

    /// <summary>
    /// Verifies package info yy yanks current line.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
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

        Assert.IsTrue(_clipboardAdapter!.ClipboardWrites.TryDequeue(out var yankedText),
            "CopyToClipboard should have emitted an OSC 52 sequence");

        Assert.IsGreaterThan(0, yankedText.Length);
        Assert.DoesNotContain("\n", yankedText);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Disposes test resources created during the run.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _cts?.Cancel();
        _state?.Dispose();
        _hex1bApp?.Dispose();
        _terminal?.Dispose();
        _workload?.Dispose();
        _cts?.Dispose();
    }
}
