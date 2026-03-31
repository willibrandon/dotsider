using Dotsider.Core.Analysis.Models;
using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Integration tests for yank flash, readonly editor yank, and focus management
/// on the Dynamic tab (issue #103).
/// </summary>
[Collection("SampleAssemblies")]
public class DynamicYankIntegrationTests(SampleAssemblyFixture samples) : IDisposable
{
    private Hex1bAppWorkloadAdapter? _workload;
    private ClipboardCapturingWorkloadAdapter? _clipboardAdapter;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _hex1bApp;
    private DotsiderState? _state;
    private CancellationTokenSource? _cts;

    private (Hex1bTerminal terminal, Hex1bApp app, CancellationToken ct) Launch(string dllPath)
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
                WorkloadAdapter = _clipboardAdapter,
                EnableInputCoalescing = false,
                EnableMouse = true,
                Theme = DotsiderTheme.Create()
            });
        return (_terminal, _hex1bApp, _cts.Token);
    }

    private bool IsFocusedOnEditor()
    {
        try { return _state?.App.FocusedNode is EditorNode; }
        catch (NullReferenceException) { return false; }
    }

    private Hex1bTerminalInputSequenceBuilder LaunchTraceAndWaitForExit()
    {
        return new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D8)
            .WaitUntil(s => s.ContainsText("EventPipe") || s.ContainsText("Launch"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.Tracer?.ProcessState
                is TraceProcessState.Exited or TraceProcessState.Error, TimeSpan.FromSeconds(30))
            .WaitUntil(s => s.ContainsText("Events"), TimeSpan.FromSeconds(5));
    }

    private Hex1bTerminalInputSequenceBuilder NavigateToCounters()
    {
        return new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.DynamicSubTab == DynamicSubTabId.Counters, TimeSpan.FromSeconds(5))
            .WaitUntil(_ => _state!.DynamicCpuEditorState is not null, TimeSpan.FromSeconds(5))
            .WaitUntil(_ => IsFocusedOnEditor(), TimeSpan.FromSeconds(5));
    }

    /// <summary>Sends Tab keys until focus leaves all editors on the Counters subtab.</summary>
    private async Task TabOutOfCountersEditorsAsync(Hex1bTerminal terminal, CancellationToken ct)
    {
        // Each Tab moves focus to the next editor or to the subtab strip.
        // We send Tab and wait for the focused node to change, repeating
        // until focus lands on something that isn't an EditorNode.
        for (var i = 0; i < 6; i++)
        {
            Hex1bNode? before;
            try { before = _state?.App.FocusedNode; }
            catch (NullReferenceException) { return; }

            if (before is not EditorNode) return;

            await new Hex1bTerminalInputSequenceBuilder()
                .Key(Hex1bKey.Tab)
                .WaitUntil(_ =>
                {
                    try { return _state?.App.FocusedNode != before; }
                    catch (NullReferenceException) { return true; }
                }, TimeSpan.FromSeconds(2))
                .Build()
                .ApplyAsync(terminal, ct);
        }

        Assert.False(IsFocusedOnEditor(),
            $"Still on an editor after 6 Tab presses. FocusedNode: {_state?.App.FocusedNode?.GetType().Name}");
    }

    private Hex1bTerminalInputSequenceBuilder NavigateFromCountersToSummary()
    {
        // Note: caller must call TabOutOfCountersEditorsAsync first, then use this
        return new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.DynamicSubTab == DynamicSubTabId.Output, TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.DynamicSubTab == DynamicSubTabId.Summary, TimeSpan.FromSeconds(5))
            .WaitUntil(_ => _state!.DynamicSummaryEditorState is not null, TimeSpan.FromSeconds(5))
            .WaitUntil(_ => IsFocusedOnEditor(), TimeSpan.FromSeconds(5));
    }

    [Fact(Timeout = 30_000)]
    public async Task Dynamic_Events_YankOnFocusedRow_CopiesPayload_AndFlashes()
    {
        var (terminal, app, ct) = Launch(samples.HelloWorldDll);
        var runTask = app.RunAsync(ct);

        await LaunchTraceAndWaitForExit()
            .Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.UpArrow)
            .WaitUntil(_ => _state!.DynamicEventsFocusedKey is not null, TimeSpan.FromSeconds(5))
            .Type("y")
            .WaitUntil(s => s.ContainsText("Yanked"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.NotNull(_state!.YankNotification);
        Assert.True(_clipboardAdapter!.ClipboardWrites.TryDequeue(out _),
            "CopyToClipboard should have emitted an OSC 52 sequence");

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact(Timeout = 30_000)]
    public async Task Dynamic_Output_YankOnFocusedRow_CopiesPayload_AndFlashes()
    {
        var (terminal, app, ct) = Launch(samples.HelloWorldDll);
        var runTask = app.RunAsync(ct);

        await LaunchTraceAndWaitForExit().Build().ApplyAsync(terminal, ct);
        await NavigateToCounters().Build().ApplyAsync(terminal, ct);

        await TabOutOfCountersEditorsAsync(terminal, ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.DynamicSubTab == DynamicSubTabId.Output, TimeSpan.FromSeconds(5))
            .WaitUntil(_ => _state!.Tracer!.GetOutput().Count > 0, TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.UpArrow)
            .WaitUntil(_ => _state!.DynamicOutputFocusedKey is not null, TimeSpan.FromSeconds(5))
            .Type("y")
            .WaitUntil(s => s.ContainsText("Yanked"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.NotNull(_state!.YankNotification);
        Assert.True(_clipboardAdapter!.ClipboardWrites.TryDequeue(out _),
            "CopyToClipboard should have emitted an OSC 52 sequence");

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact(Timeout = 30_000)]
    public async Task Dynamic_Events_YankFlashDuringSearch()
    {
        var (terminal, app, ct) = Launch(samples.HelloWorldDll);
        var runTask = app.RunAsync(ct);

        await LaunchTraceAndWaitForExit()
            .Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.UpArrow)
            .WaitUntil(_ => _state!.DynamicEventsFocusedKey is not null, TimeSpan.FromSeconds(5))
            .Type("/")
            .WaitUntil(_ => _state!.Search[TabId.Dynamic].IsActive, TimeSpan.FromSeconds(5))
            .Type("JIT")
            .Key(Hex1bKey.Enter)
            .Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.UpArrow)
            .WaitUntil(_ => _state!.DynamicEventsFocusedKey is not null, TimeSpan.FromSeconds(5))
            .Type("y")
            .WaitUntil(s => s.ContainsText("Yanked"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.NotNull(_state!.YankNotification);
        Assert.True(_clipboardAdapter!.ClipboardWrites.TryDequeue(out _),
            "CopyToClipboard should have emitted an OSC 52 sequence");

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact(Timeout = 30_000)]
    public async Task Dynamic_Counters_SelectionYank_Works()
    {
        var (terminal, app, ct) = Launch(samples.HelloWorldDll);
        var runTask = app.RunAsync(ct);

        await LaunchTraceAndWaitForExit().Build().ApplyAsync(terminal, ct);

        await NavigateToCounters()
            .Type("yiw")
            .WaitUntil(s => s.ContainsText("Yanked"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.NotNull(_state!.YankNotification);
        Assert.True(_clipboardAdapter!.ClipboardWrites.TryDequeue(out _),
            "CopyToClipboard should have emitted an OSC 52 sequence");

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact(Timeout = 30_000)]
    public async Task Dynamic_Summary_SelectionYank_Works()
    {
        var (terminal, app, ct) = Launch(samples.HelloWorldDll);
        var runTask = app.RunAsync(ct);

        await LaunchTraceAndWaitForExit().Build().ApplyAsync(terminal, ct);
        await NavigateToCounters().Build().ApplyAsync(terminal, ct);
        await TabOutOfCountersEditorsAsync(terminal, ct);

        await NavigateFromCountersToSummary()
            .Type("yiw")
            .WaitUntil(s => s.ContainsText("Yanked"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.NotNull(_state!.YankNotification);
        Assert.True(_clipboardAdapter!.ClipboardWrites.TryDequeue(out _),
            "CopyToClipboard should have emitted an OSC 52 sequence");

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact(Timeout = 30_000)]
    public async Task Dynamic_LeftRightDoNotSwitchSubTabsWhenEditorFocused()
    {
        var (terminal, app, ct) = Launch(samples.HelloWorldDll);
        var runTask = app.RunAsync(ct);

        await LaunchTraceAndWaitForExit().Build().ApplyAsync(terminal, ct);
        await NavigateToCounters().Build().ApplyAsync(terminal, ct);

        var subtabBefore = _state!.DynamicSubTab;
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.LeftArrow)
            .Key(Hex1bKey.RightArrow)
            .Build()
            .ApplyAsync(terminal, ct);

        await Task.Delay(100, ct);
        Assert.Equal(subtabBefore, _state.DynamicSubTab);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact(Timeout = 30_000)]
    public async Task Dynamic_TabFromEditor_FocusesSubtabStrip_StaysOnSubTab()
    {
        var (terminal, app, ct) = Launch(samples.HelloWorldDll);
        var runTask = app.RunAsync(ct);

        await LaunchTraceAndWaitForExit().Build().ApplyAsync(terminal, ct);
        await NavigateToCounters().Build().ApplyAsync(terminal, ct);
        await TabOutOfCountersEditorsAsync(terminal, ct);

        Assert.Equal(TabId.Dynamic, _state!.CurrentTab);
        Assert.Equal(DynamicSubTabId.Counters, _state.DynamicSubTab);

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.DynamicSubTab != DynamicSubTabId.Counters, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.Equal(TabId.Dynamic, _state.CurrentTab);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact(Timeout = 30_000)]
    public async Task Dynamic_Rerun_ClearsDynamicEditorCaches()
    {
        var (terminal, app, ct) = Launch(samples.HelloWorldDll);
        var runTask = app.RunAsync(ct);

        await LaunchTraceAndWaitForExit().Build().ApplyAsync(terminal, ct);
        await NavigateToCounters().Build().ApplyAsync(terminal, ct);
        await TabOutOfCountersEditorsAsync(terminal, ct);
        await NavigateFromCountersToSummary().Build().ApplyAsync(terminal, ct);

        Assert.NotNull(_state!.DynamicCpuEditorState);
        Assert.NotNull(_state.DynamicSummaryEditorState);

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ => !IsFocusedOnEditor(), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.LeftArrow)
            .Key(Hex1bKey.LeftArrow)
            .Key(Hex1bKey.LeftArrow)
            .WaitUntil(_ => _state.DynamicSubTab == DynamicSubTabId.Events, TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ =>
                _state.DynamicCpuEditorState is null
                && _state.DynamicSummaryEditorState is null, TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.Null(_state.DynamicCpuEditorState);
        Assert.Null(_state.DynamicCpuEditorText);
        Assert.Null(_state.DynamicSummaryEditorState);
        Assert.Null(_state.DynamicSummaryEditorText);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact(Timeout = 60_000)]
    public async Task Dynamic_Counters_LiveUpdate_PreservesSelectionWhileFocused()
    {
        var (terminal, app, ct) = Launch(samples.MinimalApiDll);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D8)
            .WaitUntil(s => s.ContainsText("EventPipe") || s.ContainsText("Launch"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.Tracer?.ProcessState == TraceProcessState.Running, TimeSpan.FromSeconds(30))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.DynamicSubTab == DynamicSubTabId.Counters, TimeSpan.FromSeconds(5))
            .WaitUntil(_ => _state!.DynamicCpuEditorState is not null, TimeSpan.FromSeconds(5))
            .WaitUntil(_ => IsFocusedOnEditor(), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        var editorStateBefore = _state!.DynamicCpuEditorState;
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("iw")
            .Build()
            .ApplyAsync(terminal, ct);

        await Task.Delay(1500, ct);
        Assert.Same(editorStateBefore, _state.DynamicCpuEditorState);

        _state.Tracer?.Stop();
        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact(Timeout = 60_000)]
    public async Task Dynamic_Summary_LiveUpdate_PreservesSelectionWhileFocused()
    {
        var (terminal, app, ct) = Launch(samples.MinimalApiDll);
        var runTask = app.RunAsync(ct);

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D8)
            .WaitUntil(s => s.ContainsText("EventPipe") || s.ContainsText("Launch"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.Tracer?.ProcessState == TraceProcessState.Running, TimeSpan.FromSeconds(30))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.DynamicSubTab == DynamicSubTabId.Counters, TimeSpan.FromSeconds(5))
            .WaitUntil(_ => IsFocusedOnEditor(), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        await TabOutOfCountersEditorsAsync(terminal, ct);

        await NavigateFromCountersToSummary()
            .Build()
            .ApplyAsync(terminal, ct);

        var editorStateBefore = _state!.DynamicSummaryEditorState;
        await new Hex1bTerminalInputSequenceBuilder()
            .Type("iw")
            .Build()
            .ApplyAsync(terminal, ct);

        await Task.Delay(1500, ct);
        Assert.Same(editorStateBefore, _state.DynamicSummaryEditorState);

        _state.Tracer?.Stop();
        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    [Fact(Timeout = 60_000)]
    public async Task Dynamic_Summary_PostExitRefresh_UpdatesWhileFocused()
    {
        var (terminal, app, ct) = Launch(samples.MinimalApiDll);
        var runTask = app.RunAsync(ct);

        // Launch trace, wait for running, switch to Summary (via Counters)
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D8)
            .WaitUntil(s => s.ContainsText("EventPipe") || s.ContainsText("Launch"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.Enter)
            .WaitUntil(_ => _state!.Tracer?.ProcessState == TraceProcessState.Running, TimeSpan.FromSeconds(30))
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.DynamicSubTab == DynamicSubTabId.Counters, TimeSpan.FromSeconds(5))
            .WaitUntil(_ => IsFocusedOnEditor(), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        await TabOutOfCountersEditorsAsync(terminal, ct);

        await NavigateFromCountersToSummary()
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.True(IsFocusedOnEditor());

        // Capture the frozen EditorState reference while the process is running.
        // The freeze mechanism keeps this exact instance alive while focused+running.
        var frozenState = _state!.DynamicSummaryEditorState;

        // Let the process run longer so Duration accumulates past the frozen snapshot
        await Task.Delay(3000, ct);

        // Stop the tracer — once exited, the freeze lifts and the editor must update
        _state.Tracer!.Stop();

        // Wait for exit, then wait for the EditorState to be recreated (proving the
        // freeze was lifted and UpdateEditorIfNeeded saw the changed text)
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(_ => _state.Tracer!.ProcessState
                is TraceProcessState.Exited or TraceProcessState.Error, TimeSpan.FromSeconds(10))
            .WaitUntil(_ => _state.DynamicSummaryEditorState != frozenState, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.NotSame(frozenState, _state.DynamicSummaryEditorState);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    public void Dispose()
    {
        _state?.Tracer?.Stop();
        _state?.Dispose();
        _hex1bApp?.Dispose();
        _terminal?.Dispose();
        _clipboardAdapter?.Dispose();
        _workload?.Dispose();
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
