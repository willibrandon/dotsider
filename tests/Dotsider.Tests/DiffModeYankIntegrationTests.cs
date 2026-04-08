using Dotsider.Views;
using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Dotsider.Tests;

[Collection("SampleAssemblies")]
public class DiffModeYankIntegrationTests(SampleAssemblyFixture samples) : IDisposable
{
    private Hex1bAppWorkloadAdapter? _workload;
    private ClipboardCapturingWorkloadAdapter? _clipboardAdapter;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _hex1bApp;
    private DiffState? _state;
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
                WorkloadAdapter = _clipboardAdapter,
                EnableInputCoalescing = false
            });
        return (_terminal, _hex1bApp, _cts.Token);
    }

    // --- Summary tab ---

    [Fact(Timeout = 30_000)]
    public async Task Summary_TabCyclesThroughEditors()
    {
        var (terminal, app, ct) = Launch();
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Change Summary") || s.ContainsText("RichLibrary"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Tab → Left Info
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ =>
            {
                try { return _state!.App.FocusedNode is EditorNode { State: var es }
                    && es == _state.LeftInfoEditorState; }
                catch (NullReferenceException) { return false; }
            }, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Tab → Right Info
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ =>
            {
                try { return _state!.App.FocusedNode is EditorNode { State: var es }
                    && es == _state.RightInfoEditorState; }
                catch (NullReferenceException) { return false; }
            }, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Tab → Change Stats
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ =>
            {
                try { return _state!.App.FocusedNode is EditorNode { State: var es }
                    && es == _state.ChangeStatsEditorState; }
                catch (NullReferenceException) { return false; }
            }, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact(Timeout = 30_000)]
    public async Task Summary_LeftRightDoNotSwitchTabsWhenEditorFocused()
    {
        var (terminal, app, ct) = Launch();
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Change Summary"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Tab) // Focus left info editor
            .WaitUntil(_ =>
            {
                try { return _state!.App.FocusedNode is EditorNode; }
                catch (NullReferenceException) { return false; }
            }, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        var tabBefore = _state!.CurrentTab;

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .Build()
            .ApplyAsync(terminal, ct);

        await Task.Delay(100, ct);
        Assert.Equal(tabBefore, _state.CurrentTab);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Types/Methods/Refs tabs ---

    [Fact(Timeout = 30_000)]
    public async Task Types_YankOnFocusedRow_ShowsNotification()
    {
        var (terminal, app, ct) = Launch();
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary"), TimeSpan.FromSeconds(10))
            .Type("2") // Types tab
            .WaitUntil(s => s.ContainsText("Type") && s.ContainsText("Base Type"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.DownArrow) // Seed focus on first row
            .Build()
            .ApplyAsync(terminal, ct);

        await Task.Delay(200, ct);

        // If DiffFocusedKey is still null, try j to navigate in table
        if (_state!.DiffFocusedKey is null)
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .Key(Hex1bKey.DownArrow)
                .Build()
                .ApplyAsync(terminal, ct);
            await Task.Delay(200, ct);
        }

        Assert.NotNull(_state.DiffFocusedKey);

        // Compute expected payload before yank
        var expectedPayload = YankHelper.GetYankText(_state);
        Assert.NotNull(expectedPayload);
        Assert.Contains("\t", expectedPayload); // Tab-separated

        // Yank
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("y")
            .WaitUntil(s => s.ContainsText("Yanked:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.NotNull(_state!.YankNotification);

        // Verify the actual OSC 52 clipboard payload emitted by ctx.CopyToClipboard
        Assert.True(_clipboardAdapter!.ClipboardWrites.TryDequeue(out var actualClipboard),
            "CopyToClipboard should have emitted an OSC 52 sequence");
        Assert.Equal(expectedPayload, actualClipboard);

        // Notification auto-clears
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(_ => _state.YankNotification is null, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact(Timeout = 30_000)]
    public async Task Types_SearchWithNavigation_CyclesMatches()
    {
        var (terminal, app, ct) = Launch();
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D2) // Types tab
            .WaitUntil(s => s.ContainsText("Type"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Seed focus — navigate down to get a focused row
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.DownArrow)
            .Build()
            .ApplyAsync(terminal, ct);
        await Task.Delay(200, ct);

        // Search for something that should match multiple types
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.OemQuestion) // /
            .Type("Model")
            .Key(Hex1bKey.Enter) // Confirm search
            .WaitUntil(_ =>
            {
                var s = _state!.Search[_state.CurrentTab];
                return s.IsActive && s.IsConfirmed;
            }, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        var firstFocused = _state!.DiffFocusedKey;

        // n → next match
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("n")
            .Build()
            .ApplyAsync(terminal, ct);

        await TestHelpers.WaitUntilAsync(
            () => _state!.DiffFocusedKey is not null && !Equals(_state.DiffFocusedKey, firstFocused),
            TimeSpan.FromSeconds(10));

        var secondFocused = _state.DiffFocusedKey;
        Assert.NotEqual(firstFocused, secondFocused);

        // N → previous match (moves to a different row)
        await new Hex1bTerminalInputSequenceBuilder()
            .Shift().Key(Hex1bKey.N)
            .Build()
            .ApplyAsync(terminal, ct);

        await TestHelpers.WaitUntilAsync(
            () => _state!.DiffFocusedKey is not null && !Equals(_state.DiffFocusedKey, secondFocused),
            TimeSpan.FromSeconds(10));

        Assert.NotEqual(secondFocused, _state.DiffFocusedKey);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact(Timeout = 30_000)]
    public async Task TabArrows_WorkWhenEditorNotFocused()
    {
        var (terminal, app, ct) = Launch();
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.Equal(0, _state!.CurrentTab); // Summary

        // Right arrow switches to Types tab (tab index 1)
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state.CurrentTab == 1, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.Equal(1, _state.CurrentTab);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Summary editor selection + yank ---

    [Fact(Timeout = 30_000)]
    public async Task Summary_SelectionYank_ShowsNotification()
    {
        var (terminal, app, ct) = Launch();
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Change Summary"), TimeSpan.FromSeconds(10))
            // Tab to left info editor
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ =>
            {
                try { return _state!.App.FocusedNode is EditorNode { State: var es }
                    && es == _state.LeftInfoEditorState; }
                catch (NullReferenceException) { return false; }
            }, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Select text
        await new Hex1bTerminalInputSequenceBuilder()
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.LeftInfoEditorState!.Cursor.HasSelection, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Yank
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("y")
            .WaitUntil(s => s.ContainsText("Yanked:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.NotNull(_state!.YankNotification);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact(Timeout = 30_000)]
    public async Task Summary_RightInfoYank_ShowsNotification()
    {
        var (terminal, app, ct) = Launch();
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Change Summary"), TimeSpan.FromSeconds(10))
            // Tab to left info, then tab to right info
            .Key(Hex1bKey.Tab)
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ =>
            {
                try { return _state!.App.FocusedNode is EditorNode { State: var es } && es == _state.RightInfoEditorState; }
                catch (NullReferenceException) { return false; }
            }, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Select and yank
        await new Hex1bTerminalInputSequenceBuilder()
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.RightInfoEditorState!.Cursor.HasSelection, TimeSpan.FromSeconds(10))
            .Type("y")
            .WaitUntil(s => s.ContainsText("Yanked:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.NotNull(_state!.YankNotification);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact(Timeout = 30_000)]
    public async Task Summary_ChangeStatsYank_ShowsNotification()
    {
        var (terminal, app, ct) = Launch();
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Change Summary"), TimeSpan.FromSeconds(10))
            // Tab to left info first
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ =>
            {
                try { return _state!.App.FocusedNode is EditorNode { State: var es } && es == _state.LeftInfoEditorState; }
                catch (NullReferenceException) { return false; }
            }, TimeSpan.FromSeconds(10))
            // Tab to right info
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ =>
            {
                try { return _state!.App.FocusedNode is EditorNode { State: var es } && es == _state.RightInfoEditorState; }
                catch (NullReferenceException) { return false; }
            }, TimeSpan.FromSeconds(10))
            // Tab to change stats
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ =>
            {
                try { return _state!.App.FocusedNode is EditorNode { State: var es } && es == _state.ChangeStatsEditorState; }
                catch (NullReferenceException) { return false; }
            }, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Select and yank
        await new Hex1bTerminalInputSequenceBuilder()
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.ChangeStatsEditorState!.Cursor.HasSelection, TimeSpan.FromSeconds(10))
            .Type("y")
            .WaitUntil(s => s.ContainsText("Yanked:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.NotNull(_state!.YankNotification);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Methods tab yank ---

    [Fact(Timeout = 30_000)]
    public async Task Methods_YankOnFocusedRow_ShowsNotification()
    {
        var (terminal, app, ct) = Launch();
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D3) // Methods tab
            .WaitUntil(s => s.ContainsText("Method") && s.ContainsText("Declaring Type"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.DownArrow) // Seed focus
            .Build()
            .ApplyAsync(terminal, ct);
        await Task.Delay(200, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .Type("y")
            .WaitUntil(s => s.ContainsText("Yanked:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.NotNull(_state!.YankNotification);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Refs tab yank ---

    [Fact(Timeout = 30_000)]
    public async Task Refs_YankOnFocusedRow_ShowsNotification()
    {
        var (terminal, app, ct) = Launch();
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D4) // Refs tab
            .WaitUntil(s => s.ContainsText("Assembly") && s.ContainsText("Left Version"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.DownArrow)
            .Build()
            .ApplyAsync(terminal, ct);
        await Task.Delay(200, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .Type("y")
            .WaitUntil(s => s.ContainsText("Yanked:"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.NotNull(_state!.YankNotification);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Methods n/N search navigation ---

    [Fact(Timeout = 30_000)]
    public async Task Methods_SearchNavigation_CyclesMatches()
    {
        var (terminal, app, ct) = Launch();
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D3) // Methods tab
            .WaitUntil(s => s.ContainsText("Method"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.DownArrow)
            .Build()
            .ApplyAsync(terminal, ct);
        await Task.Delay(200, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.OemQuestion)
            .Type("get")
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ =>
            {
                var s = _state!.Search[_state.CurrentTab];
                return s.IsActive && s.IsConfirmed;
            }, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        var first = _state!.DiffFocusedKey;

        await new Hex1bTerminalInputSequenceBuilder()
            .Type("n")
            .Build()
            .ApplyAsync(terminal, ct);

        await TestHelpers.WaitUntilAsync(
            () => _state!.DiffFocusedKey is not null && !Equals(_state.DiffFocusedKey, first),
            TimeSpan.FromSeconds(10));

        var second = _state.DiffFocusedKey;
        Assert.NotEqual(first, second);

        // N → previous match (moves to a different row)
        await new Hex1bTerminalInputSequenceBuilder()
            .Shift().Key(Hex1bKey.N)
            .Build()
            .ApplyAsync(terminal, ct);

        await TestHelpers.WaitUntilAsync(
            () => _state!.DiffFocusedKey is not null && !Equals(_state.DiffFocusedKey, second),
            TimeSpan.FromSeconds(10));

        Assert.NotEqual(second, _state.DiffFocusedKey);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Refs n/N search navigation ---

    [Fact(Timeout = 30_000)]
    public async Task Refs_SearchNavigation_CyclesMatches()
    {
        var (terminal, app, ct) = Launch();
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D4) // Refs tab
            .WaitUntil(s => s.ContainsText("Assembly"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.DownArrow)
            .Build()
            .ApplyAsync(terminal, ct);
        await Task.Delay(200, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.OemQuestion)
            .Type("System")
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ =>
            {
                var s = _state!.Search[_state.CurrentTab];
                return s.IsActive && s.IsConfirmed;
            }, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        var first = _state!.DiffFocusedKey;

        await new Hex1bTerminalInputSequenceBuilder()
            .Type("n")
            .Build()
            .ApplyAsync(terminal, ct);

        await TestHelpers.WaitUntilAsync(
            () => _state!.DiffFocusedKey is not null && !Equals(_state.DiffFocusedKey, first),
            TimeSpan.FromSeconds(10));

        var second = _state.DiffFocusedKey;
        Assert.NotEqual(first, second);

        // N → previous match (moves to a different row)
        await new Hex1bTerminalInputSequenceBuilder()
            .Shift().Key(Hex1bKey.N)
            .Build()
            .ApplyAsync(terminal, ct);

        await TestHelpers.WaitUntilAsync(
            () => _state!.DiffFocusedKey is not null && !Equals(_state.DiffFocusedKey, second),
            TimeSpan.FromSeconds(10));

        Assert.NotEqual(second, _state.DiffFocusedKey);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Yank flash set and clear ---

    [Fact(Timeout = 30_000)]
    public async Task Types_YankFlash_SetsAndClears()
    {
        var (terminal, app, ct) = Launch();
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D2) // Types tab
            .WaitUntil(s => s.ContainsText("Type"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.DownArrow)
            .Build()
            .ApplyAsync(terminal, ct);
        await Task.Delay(200, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .Type("y")
            .WaitUntil(_ => _state!.YankNotification is not null, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Flash should clear after 150ms
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(_ => !_state!.YankFlashRow, TimeSpan.FromSeconds(3))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.False(_state!.YankFlashRow);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact(Timeout = 30_000)]
    public async Task Summary_YY_YanksCurrentLine()
    {
        var (terminal, app, ct) = Launch();
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Change Summary"), TimeSpan.FromSeconds(10))
            // Tab to focus the left info editor
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ =>
            {
                try { return _state!.App.FocusedNode is EditorNode; }
                catch (NullReferenceException) { return false; }
            }, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // yy to yank the current line
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("y")
            .Type("y")
            .WaitUntil(s => s.ContainsText("Yanked"), TimeSpan.FromSeconds(10))
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
