using Dotsider.Core.Analysis.Models;
using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Integration tests for yank flash, readonly editor yank, and focus management
/// on the Dynamic tab (issue #103).
/// </summary>
[TestClass]
public class DynamicYankIntegrationTests : IDisposable
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private Hex1bAppWorkloadAdapter? _workload;
    private ClipboardCapturingWorkloadAdapter? _clipboardAdapter;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _hex1bApp;
    private DotsiderState? _state;
    private CancellationTokenSource? _cts;

    private (Hex1bTerminal terminal, Hex1bApp app, CancellationToken ct) Launch(string dllPath)
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

    private bool IsFocusedOnEditor(EditorState? expectedState)
    {
        try
        {
            return _state?.App.FocusedNode is EditorNode { State: var es }
                && es == expectedState;
        }
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
            .WaitUntil(_ => IsFocusedOnEditor(_state!.DynamicCpuEditorState), TimeSpan.FromSeconds(5));
    }

    /// <summary>Moves focus out of the Counters editors to the subtab strip.</summary>
    private async Task TabOutOfCountersEditorsAsync(Hex1bTerminal terminal, CancellationToken ct)
    {
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(_ => IsFocusedOnEditor(_state!.DynamicCpuEditorState), TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ => IsFocusedOnEditor(_state!.DynamicMemoryEditorState), TimeSpan.FromSeconds(5))
            .WaitUntil(_ => _state!.DynamicSubTab == DynamicSubTabId.Counters, TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ => IsFocusedOnEditor(_state!.DynamicGcEditorState), TimeSpan.FromSeconds(5))
            .WaitUntil(_ => _state!.DynamicSubTab == DynamicSubTabId.Counters, TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ => IsFocusedOnEditor(_state!.DynamicThreadingEditorState), TimeSpan.FromSeconds(5))
            .WaitUntil(_ => _state!.DynamicSubTab == DynamicSubTabId.Counters, TimeSpan.FromSeconds(5))
            .Key(Hex1bKey.Tab)
            .WaitUntil(_ => !IsFocusedOnEditor(), TimeSpan.FromSeconds(5))
            .WaitUntil(_ => _state!.DynamicSubTab == DynamicSubTabId.Counters, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);
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
            .WaitUntil(_ => IsFocusedOnEditor(_state!.DynamicSummaryEditorState), TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Verifies dynamic events yank on focused row copies payload and flashes.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Dynamic_Events_YankOnFocusedRow_CopiesPayload_AndFlashes()
    {
        var (terminal, app, ct) = Launch(Samples.HelloWorldDll);
        var runTask = app.RunAsync(ct);

        await LaunchTraceAndWaitForExit()
            .Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.UpArrow)
            .WaitUntil(_ => _state!.DynamicEventsFocusedKey is not null, TimeSpan.FromSeconds(5))
            .Type("y")
            .WaitUntil(s => s.ContainsText("Yanked"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.IsNotNull(_state!.YankNotification);
        Assert.IsTrue(_clipboardAdapter!.ClipboardWrites.TryDequeue(out _),
            "CopyToClipboard should have emitted an OSC 52 sequence");

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies dynamic output yank on focused row copies payload and flashes.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Dynamic_Output_YankOnFocusedRow_CopiesPayload_AndFlashes()
    {
        var (terminal, app, ct) = Launch(Samples.HelloWorldDll);
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

        Assert.IsNotNull(_state!.YankNotification);
        Assert.IsTrue(_clipboardAdapter!.ClipboardWrites.TryDequeue(out _),
            "CopyToClipboard should have emitted an OSC 52 sequence");

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies dynamic events yank flash during search.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Dynamic_Events_YankFlashDuringSearch()
    {
        var (terminal, app, ct) = Launch(Samples.HelloWorldDll);
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

        Assert.IsNotNull(_state!.YankNotification);
        Assert.IsTrue(_clipboardAdapter!.ClipboardWrites.TryDequeue(out _),
            "CopyToClipboard should have emitted an OSC 52 sequence");

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies dynamic counters selection yank works.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Dynamic_Counters_SelectionYank_Works()
    {
        var (terminal, app, ct) = Launch(Samples.HelloWorldDll);
        var runTask = app.RunAsync(ct);

        await LaunchTraceAndWaitForExit().Build().ApplyAsync(terminal, ct);

        await NavigateToCounters()
            .Type("yiw")
            .WaitUntil(s => s.ContainsText("Yanked"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.IsNotNull(_state!.YankNotification);
        Assert.IsTrue(_clipboardAdapter!.ClipboardWrites.TryDequeue(out _),
            "CopyToClipboard should have emitted an OSC 52 sequence");

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies dynamic summary selection yank works.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Dynamic_Summary_SelectionYank_Works()
    {
        var (terminal, app, ct) = Launch(Samples.HelloWorldDll);
        var runTask = app.RunAsync(ct);

        await LaunchTraceAndWaitForExit().Build().ApplyAsync(terminal, ct);
        await NavigateToCounters().Build().ApplyAsync(terminal, ct);
        await TabOutOfCountersEditorsAsync(terminal, ct);

        await NavigateFromCountersToSummary()
            .Type("yiw")
            .WaitUntil(s => s.ContainsText("Yanked"), TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.IsNotNull(_state!.YankNotification);
        Assert.IsTrue(_clipboardAdapter!.ClipboardWrites.TryDequeue(out _),
            "CopyToClipboard should have emitted an OSC 52 sequence");

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies dynamic left right navigate sub tabs from editor focus.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Dynamic_LeftRightNavigateSubTabsFromEditorFocus()
    {
        var (terminal, app, ct) = Launch(Samples.HelloWorldDll);
        var runTask = app.RunAsync(ct);

        await LaunchTraceAndWaitForExit().Build().ApplyAsync(terminal, ct);
        await NavigateToCounters().Build().ApplyAsync(terminal, ct);

        Assert.AreEqual(DynamicSubTabId.Counters, _state!.DynamicSubTab);

        // Left from Counters → Events
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.LeftArrow)
            .WaitUntil(_ => _state!.DynamicSubTab == DynamicSubTabId.Events, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.AreEqual(DynamicSubTabId.Events, _state.DynamicSubTab);

        // Right from Events → Counters
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.DynamicSubTab == DynamicSubTabId.Counters, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.AreEqual(DynamicSubTabId.Counters, _state.DynamicSubTab);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies dynamic tab from editor focuses subtab strip stays on sub tab.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Dynamic_TabFromEditor_FocusesSubtabStrip_StaysOnSubTab()
    {
        var (terminal, app, ct) = Launch(Samples.HelloWorldDll);
        var runTask = app.RunAsync(ct);

        await LaunchTraceAndWaitForExit().Build().ApplyAsync(terminal, ct);
        await NavigateToCounters().Build().ApplyAsync(terminal, ct);
        await TabOutOfCountersEditorsAsync(terminal, ct);

        Assert.AreEqual(TabId.Dynamic, _state!.CurrentTab);
        Assert.AreEqual(DynamicSubTabId.Counters, _state.DynamicSubTab);

        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.RightArrow)
            .WaitUntil(_ => _state!.DynamicSubTab != DynamicSubTabId.Counters, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.AreEqual(TabId.Dynamic, _state.CurrentTab);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies dynamic rerun clears dynamic editor caches.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Dynamic_Rerun_ClearsDynamicEditorCaches()
    {
        var (terminal, app, ct) = Launch(Samples.HelloWorldDll);
        var runTask = app.RunAsync(ct);

        await LaunchTraceAndWaitForExit().Build().ApplyAsync(terminal, ct);
        await NavigateToCounters().Build().ApplyAsync(terminal, ct);
        await TabOutOfCountersEditorsAsync(terminal, ct);
        await NavigateFromCountersToSummary().Build().ApplyAsync(terminal, ct);

        Assert.IsNotNull(_state!.DynamicCpuEditorState);
        Assert.IsNotNull(_state.DynamicSummaryEditorState);

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

        Assert.IsNull(_state.DynamicCpuEditorState);
        Assert.IsNull(_state.DynamicCpuEditorText);
        Assert.IsNull(_state.DynamicSummaryEditorState);
        Assert.IsNull(_state.DynamicSummaryEditorText);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies dynamic counters live update preserves selection while focused.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Dynamic_Counters_LiveUpdate_PreservesSelectionWhileFocused()
    {
        var (terminal, app, ct) = Launch(Samples.MinimalApiDll);
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
        Assert.AreSame(editorStateBefore, _state.DynamicCpuEditorState);

        _state.Tracer?.Stop();
        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies dynamic summary live update preserves selection while focused.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Dynamic_Summary_LiveUpdate_PreservesSelectionWhileFocused()
    {
        var (terminal, app, ct) = Launch(Samples.MinimalApiDll);
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
        Assert.AreSame(editorStateBefore, _state.DynamicSummaryEditorState);

        _state.Tracer?.Stop();
        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Verifies dynamic summary post exit refresh updates while focused.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Dynamic_Summary_PostExitRefresh_UpdatesWhileFocused()
    {
        var (terminal, app, ct) = Launch(Samples.MinimalApiDll);
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

        Assert.IsTrue(IsFocusedOnEditor());

        // Capture the frozen EditorState reference while the process is running.
        // The freeze mechanism keeps this exact instance alive while focused+running.
        var frozenState = _state!.DynamicSummaryEditorState;

        // Wait for the tracer to accumulate some data so Duration differs from the frozen snapshot
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(_ => true, TimeSpan.FromSeconds(3))
            .Build()
            .ApplyAsync(terminal, ct);

        // Stop the tracer via Ctrl+K (through the UI, which naturally triggers a
        // render) rather than calling Stop() directly. Direct Stop() can race with
        // the render loop's snapshot polling.
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(15));
        await auto.Ctrl().KeyAsync(Hex1bKey.K, ct);
        await auto.WaitUntilTextAsync("Exited");
        await auto.WaitUntilAsync(_ => _state.DynamicSummaryEditorState != frozenState,
            description: "editor state to update after freeze lifts");

        Assert.AreNotSame(frozenState, _state.DynamicSummaryEditorState);

        _cts!.Cancel();
        try { await runTask; } catch (OperationCanceledException) { }
    }

    /// <summary>
    /// Disposes test resources created during the run.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _state?.Tracer?.Stop();
        _state?.Dispose();
        _hex1bApp?.Dispose();
        _terminal?.Dispose();
        _clipboardAdapter?.Dispose();
        _workload?.Dispose();
        _cts?.Dispose();
    }
}
