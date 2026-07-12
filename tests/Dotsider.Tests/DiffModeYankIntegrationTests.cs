using Dotsider.Core.Analysis.Models;
using Dotsider.Views;
using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Diff Mode Yank Integration.
/// </summary>
[TestClass]
public class DiffModeYankIntegrationTests : IDisposable
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private Hex1bAppWorkloadAdapter? _workload;
    private ClipboardCapturingWorkloadAdapter? _clipboardAdapter;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _hex1bApp;
    private DiffState? _state;
    private CancellationTokenSource? _cts;
    private Task? _runTask;

    private (Hex1bTerminal terminal, Hex1bApp app, CancellationToken ct) Launch()
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
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
                _state ??= new DiffState(_hex1bApp!, Samples.RichLibraryDll, Samples.RichLibraryV2Dll);
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

    private Task RunAppAsync(Hex1bApp app, CancellationToken ct)
    {
        _runTask = app.RunAsync(ct);
        return _runTask;
    }

    private async Task<string> FocusFirstDiffRowAsync<T>(
        Hex1bTerminal terminal,
        IReadOnlyList<DiffEntry<T>> entries,
        Func<DiffEntry<T>, string> keySelector,
        CancellationToken ct)
    {
        Assert.IsNotEmpty(entries);
        var expectedKey = keySelector(entries[0]);

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Home)
            .WaitUntil(
                _ => Equals(_state!.DiffFocusedKey, expectedKey)
                    && _state.App.FocusedNode is TableNode<DiffEntry<T>> table
                    && Equals(table.FocusedKey, expectedKey),
                TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        return expectedKey;
    }

    private bool TryWaitForAppExit()
    {
        if (_runTask is null) return true;
        try { return _runTask.Wait(TimeSpan.FromSeconds(5)); }
        catch (AggregateException ex) when (ex.InnerExceptions.All(static e => e is OperationCanceledException)) { return true; }
        catch (OperationCanceledException) { return true; }
    }

    // --- Summary tab ---

    /// <summary>
    /// Verifies summary tab cycles through editors.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Summary_TabCyclesThroughEditors()
    {
        var (terminal, app, ct) = Launch();
        var runTask = RunAppAsync(app, ct);

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
                try
                {
                    return _state!.App.FocusedNode is EditorNode { State: var es }
                    && es == _state.LeftInfoEditorState;
                }
                catch (NullReferenceException) { return false; }
            }, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Tab → Right Info
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ =>
            {
                try
                {
                    return _state!.App.FocusedNode is EditorNode { State: var es }
                    && es == _state.RightInfoEditorState;
                }
                catch (NullReferenceException) { return false; }
            }, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Tab → Change Stats
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ =>
            {
                try
                {
                    return _state!.App.FocusedNode is EditorNode { State: var es }
                    && es == _state.ChangeStatsEditorState;
                }
                catch (NullReferenceException) { return false; }
            }, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies summary left right do not switch tabs when editor focused.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Summary_LeftRightDoNotSwitchTabsWhenEditorFocused()
    {
        var (terminal, app, ct) = Launch();
        var runTask = RunAppAsync(app, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Change Summary"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Tab) // Focus left info editor
            .WaitUntil(_ =>
            {
                try
                {
                    return _state!.App.FocusedNode is EditorNode { State: var editorState }
                        && ReferenceEquals(editorState, _state.LeftInfoEditorState);
                }
                catch (NullReferenceException) { return false; }
            }, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        var tabBefore = _state!.CurrentTab;
        var editor = _state.LeftInfoEditorState!;
        var cursorBefore = editor.Cursor.Position.Value;

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(
                _ => editor.Cursor.Position.Value != cursorBefore,
                TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.AreEqual(tabBefore, _state.CurrentTab);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Types/Methods/Refs tabs ---

    /// <summary>
    /// Verifies types yank on focused row shows notification.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Types_YankOnFocusedRow_ShowsNotification()
    {
        var (terminal, app, ct) = Launch();
        var runTask = RunAppAsync(app, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary"), TimeSpan.FromSeconds(10))
            .Type("2") // Types tab
            .WaitUntil(s => s.ContainsText("Type") && s.ContainsText("Base Type"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        var state = _state!;
        var expectedKey = await FocusFirstDiffRowAsync(
            terminal,
            state.DiffResult.TypeDiffs,
            GetTypeDiffKey,
            ct);
        var expectedPayload = YankHelper.GetYankText(state);
        Assert.IsNotNull(expectedPayload);
        Assert.Contains("\t", expectedPayload); // Tab-separated

        // Yank
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("y")
            .WaitUntil(
                _snapshot => _clipboardAdapter!.ClipboardWrites.TryPeek(out _)
                    && state.YankNotification is not null,
                TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Verify the actual OSC 52 clipboard payload emitted by ctx.CopyToClipboard
        Assert.IsTrue(_clipboardAdapter!.ClipboardWrites.TryDequeue(out var actualClipboard),
            "CopyToClipboard should have emitted an OSC 52 sequence");
        Assert.AreEqual(expectedKey, state.DiffFocusedKey);
        Assert.AreEqual(expectedPayload, actualClipboard);

        // Notification auto-clears
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(_ => state.YankNotification is null, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies types search with navigation cycles matches.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Types_SearchWithNavigation_CyclesMatches()
    {
        var (terminal, app, ct) = Launch();
        var runTask = RunAppAsync(app, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D2) // Types tab
            .WaitUntil(s => s.ContainsText("Type"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        _ = await FocusFirstDiffRowAsync(
            terminal,
            _state!.DiffResult.TypeDiffs,
            GetTypeDiffKey,
            ct);

        // Search for something that should match multiple types
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.OemQuestion) // /
            .Type("Model")
            .Key(Hex1bKey.Enter) // Confirm search
            .WaitUntil(_ =>
            {
                var s = _state!.Search[_state.CurrentTab];
                return s.IsActive && s.IsConfirmed;
            }, TimeSpan.FromSeconds(5))
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
            TimeSpan.FromSeconds(5));

        var secondFocused = _state.DiffFocusedKey;
        Assert.AreNotEqual(firstFocused, secondFocused);

        // N → previous match (moves to a different row)
        await new Hex1bTerminalInputSequenceBuilder()
            .Shift().Key(Hex1bKey.N)
            .Build()
            .ApplyAsync(terminal, ct);

        await TestHelpers.WaitUntilAsync(
            () => _state!.DiffFocusedKey is not null && !Equals(_state.DiffFocusedKey, secondFocused),
            TimeSpan.FromSeconds(5));

        Assert.AreNotEqual(secondFocused, _state.DiffFocusedKey);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies tab arrows work when editor not focused.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task TabArrows_WorkWhenEditorNotFocused()
    {
        var (terminal, app, ct) = Launch();
        var runTask = RunAppAsync(app, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.AreEqual(0, _state!.CurrentTab); // Summary

        // Right arrow switches to Types tab (tab index 1)
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state.CurrentTab == 1, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.AreEqual(1, _state.CurrentTab);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Summary editor selection + yank ---

    /// <summary>
    /// Verifies summary selection yank shows notification.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Summary_SelectionYank_ShowsNotification()
    {
        var (terminal, app, ct) = Launch();
        var runTask = RunAppAsync(app, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Change Summary"), TimeSpan.FromSeconds(10))
            // Tab to left info editor
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ =>
            {
                try
                {
                    return _state!.App.FocusedNode is EditorNode { State: var es }
                    && es == _state.LeftInfoEditorState;
                }
                catch (NullReferenceException) { return false; }
            }, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Select text
        await new Hex1bTerminalInputSequenceBuilder()
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.LeftInfoEditorState!.Cursor.HasSelection, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Yank
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("y")
            .WaitUntil(_ => _state!.YankNotification is not null, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies summary right info yank shows notification.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Summary_RightInfoYank_ShowsNotification()
    {
        var (terminal, app, ct) = Launch();
        var runTask = RunAppAsync(app, ct);

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
            }, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Select and yank
        await new Hex1bTerminalInputSequenceBuilder()
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.RightInfoEditorState!.Cursor.HasSelection, TimeSpan.FromSeconds(5))
            .Type("y")
            .WaitUntil(_ => _state!.YankNotification is not null, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies summary change stats yank shows notification.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Summary_ChangeStatsYank_ShowsNotification()
    {
        var (terminal, app, ct) = Launch();
        var runTask = RunAppAsync(app, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Change Summary"), TimeSpan.FromSeconds(10))
            // Tab to left info first
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ =>
            {
                try { return _state!.App.FocusedNode is EditorNode { State: var es } && es == _state.LeftInfoEditorState; }
                catch (NullReferenceException) { return false; }
            }, TimeSpan.FromSeconds(5))
            // Tab to right info
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ =>
            {
                try { return _state!.App.FocusedNode is EditorNode { State: var es } && es == _state.RightInfoEditorState; }
                catch (NullReferenceException) { return false; }
            }, TimeSpan.FromSeconds(5))
            // Tab to change stats
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ =>
            {
                try { return _state!.App.FocusedNode is EditorNode { State: var es } && es == _state.ChangeStatsEditorState; }
                catch (NullReferenceException) { return false; }
            }, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Select and yank
        await new Hex1bTerminalInputSequenceBuilder()
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .Shift().Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.ChangeStatsEditorState!.Cursor.HasSelection, TimeSpan.FromSeconds(5))
            .Type("y")
            .WaitUntil(_ => _state!.YankNotification is not null, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Methods tab yank ---

    /// <summary>
    /// Verifies methods yank on focused row shows notification.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Methods_YankOnFocusedRow_ShowsNotification()
    {
        var (terminal, app, ct) = Launch();
        var runTask = RunAppAsync(app, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D3) // Methods tab
            .WaitUntil(s => s.ContainsText("Method") && s.ContainsText("Declaring Type"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        var state = _state!;
        var expectedKey = await FocusFirstDiffRowAsync(
            terminal,
            state.DiffResult.MethodDiffs,
            GetMethodDiffKey,
            ct);
        var expectedPayload = YankHelper.GetYankText(state);
        Assert.IsNotNull(expectedPayload);

        await new Hex1bTerminalInputSequenceBuilder()
            .Type("y")
            .WaitUntil(
                _snapshot => _clipboardAdapter!.ClipboardWrites.TryPeek(out _)
                    && state.YankNotification is not null,
                TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.IsTrue(_clipboardAdapter!.ClipboardWrites.TryDequeue(out var actualClipboard),
            "CopyToClipboard should have emitted an OSC 52 sequence");
        Assert.AreEqual(expectedKey, state.DiffFocusedKey);
        Assert.AreEqual(expectedPayload, actualClipboard);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Refs tab yank ---

    /// <summary>
    /// Verifies refs yank on focused row shows notification.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Refs_YankOnFocusedRow_ShowsNotification()
    {
        var (terminal, app, ct) = Launch();
        var runTask = RunAppAsync(app, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D4) // Refs tab
            .WaitUntil(s => s.ContainsText("Assembly") && s.ContainsText("Left Version"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        var state = _state!;
        var expectedKey = await FocusFirstDiffRowAsync(
            terminal,
            state.DiffResult.AssemblyRefDiffs,
            GetAssemblyRefDiffKey,
            ct);
        var expectedPayload = YankHelper.GetYankText(state);
        Assert.IsNotNull(expectedPayload);

        await new Hex1bTerminalInputSequenceBuilder()
            .Type("y")
            .WaitUntil(
                _snapshot => _clipboardAdapter!.ClipboardWrites.TryPeek(out _)
                    && state.YankNotification is not null,
                TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.IsTrue(_clipboardAdapter!.ClipboardWrites.TryDequeue(out var actualClipboard),
            "CopyToClipboard should have emitted an OSC 52 sequence");
        Assert.AreEqual(expectedKey, state.DiffFocusedKey);
        Assert.AreEqual(expectedPayload, actualClipboard);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Methods n/N search navigation ---

    /// <summary>
    /// Verifies methods search navigation cycles matches.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Methods_SearchNavigation_CyclesMatches()
    {
        var (terminal, app, ct) = Launch();
        var runTask = RunAppAsync(app, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D3) // Methods tab
            .WaitUntil(s => s.ContainsText("Method"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        _ = await FocusFirstDiffRowAsync(
            terminal,
            _state!.DiffResult.MethodDiffs,
            GetMethodDiffKey,
            ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.OemQuestion)
            .Type("get")
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ =>
            {
                var s = _state!.Search[_state.CurrentTab];
                return s.IsActive && s.IsConfirmed;
            }, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("n/N: navigate"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        var first = _state!.DiffFocusedKey;

        await new Hex1bTerminalInputSequenceBuilder()
            .Type("n")
            .WaitUntil(_ => !Equals(_state!.DiffFocusedKey, first), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        var second = _state.DiffFocusedKey;
        Assert.AreNotEqual(first, second);

        // N → previous match (moves to a different row)
        await new Hex1bTerminalInputSequenceBuilder()
            .Shift().Key(Hex1bKey.N)
            .WaitUntil(_ => !Equals(_state!.DiffFocusedKey, second), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.AreNotEqual(second, _state.DiffFocusedKey);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Refs n/N search navigation ---

    /// <summary>
    /// Verifies refs search navigation cycles matches.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Refs_SearchNavigation_CyclesMatches()
    {
        var (terminal, app, ct) = Launch();
        var runTask = RunAppAsync(app, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D4) // Refs tab
            .WaitUntil(s => s.ContainsText("Assembly"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        _ = await FocusFirstDiffRowAsync(
            terminal,
            _state!.DiffResult.AssemblyRefDiffs,
            GetAssemblyRefDiffKey,
            ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.OemQuestion)
            .Type("System")
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ =>
            {
                var s = _state!.Search[_state.CurrentTab];
                return s.IsActive && s.IsConfirmed;
            }, TimeSpan.FromSeconds(5))
            .WaitUntil(s => s.ContainsText("n/N: navigate"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        var first = _state!.DiffFocusedKey;

        await new Hex1bTerminalInputSequenceBuilder()
            .Type("n")
            .WaitUntil(_ => !Equals(_state!.DiffFocusedKey, first), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        var second = _state.DiffFocusedKey;
        Assert.AreNotEqual(first, second);

        // N → previous match (moves to a different row)
        await new Hex1bTerminalInputSequenceBuilder()
            .Shift().Key(Hex1bKey.N)
            .WaitUntil(_ => !Equals(_state!.DiffFocusedKey, second), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.AreNotEqual(second, _state.DiffFocusedKey);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    // --- Yank flash set and clear ---

    /// <summary>
    /// Verifies types yank flash sets and clears.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Types_YankFlash_SetsAndClears()
    {
        var (terminal, app, ct) = Launch();
        var runTask = RunAppAsync(app, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("RichLibrary"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D2) // Types tab
            .WaitUntil(s => s.ContainsText("Type"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        _ = await FocusFirstDiffRowAsync(
            terminal,
            _state!.DiffResult.TypeDiffs,
            GetTypeDiffKey,
            ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .Type("y")
            .WaitUntil(_ => _state!.YankNotification is not null, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        // Flash should clear after 150ms
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(_ => !_state!.YankFlashRow, TimeSpan.FromSeconds(3))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.IsFalse(_state!.YankFlashRow);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies summary yy yanks current line.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Summary_YY_YanksCurrentLine()
    {
        var (terminal, app, ct) = Launch();
        var runTask = RunAppAsync(app, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Change Summary"), TimeSpan.FromSeconds(10))
            // Tab to focus the left info editor
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

    private static string GetAssemblyRefDiffKey(DiffEntry<AssemblyRefInfo> entry) =>
        entry.Kind + ":" + (entry.Left?.Name ?? entry.Right?.Name ?? "");

    private static string GetMethodDiffKey(DiffEntry<MethodDefInfo> entry) =>
        entry.Kind + ":"
        + (entry.Left?.DeclaringType ?? entry.Right?.DeclaringType ?? "")
        + "::"
        + (entry.Left?.Name ?? entry.Right?.Name ?? "")
        + (entry.Left?.Signature ?? entry.Right?.Signature ?? "");

    private static string GetTypeDiffKey(DiffEntry<TypeDefInfo> entry) =>
        entry.Kind + ":" + (entry.Left?.FullName ?? entry.Right?.FullName ?? "");

    /// <summary>
    /// Disposes test resources created during the run.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _cts?.Cancel();
        if (!TryWaitForAppExit())
        {
            _hex1bApp?.Dispose();
            _terminal?.Dispose();
            _ = TryWaitForAppExit();
        }
        _state?.Dispose();
        _hex1bApp?.Dispose();
        _terminal?.Dispose();
        _workload?.Dispose();
        _cts?.Dispose();
    }
}
