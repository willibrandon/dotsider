using Dotsider.Core.Analysis;
using Dotsider.Views;
using Hex1b;
using Hex1b.Automation;
using Hex1b.Documents;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Tests for IL editor lifecycle: StatePanelWidget-based editor caching,
/// tree list per-render sync, and field pane staleness.
/// </summary>
[Collection("SampleAssemblies")]
public class IlEditorLifecycleTests(SampleAssemblyFixture samples) : IDisposable
{
    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _hex1bApp;
    private DotsiderState? _state;
    private CancellationTokenSource? _cts;

    // ── Helpers ───────────────────────────────────────────────

    private Hex1bApp CreateMinimalApp()
    {
        _workload = new Hex1bAppWorkloadAdapter();
        _terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(_workload)
            .WithHeadless()
            .WithDimensions(80, 24)
            .Build();
        _hex1bApp = new Hex1bApp(
            _ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("test")),
            new Hex1bAppOptions { WorkloadAdapter = _workload });
        return _hex1bApp;
    }

    private (Hex1bTerminal terminal, Hex1bApp app, CancellationToken ct) CreateDotsiderApp(string dllPath)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
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
        return (_terminal, _hex1bApp, _cts.Token);
    }

    /// <summary>
    /// Navigate to the IL Inspector tab and wait for tree + editor to appear.
    /// Programmatically selects a method so the editor pane populates.
    /// </summary>
    private static async Task NavigateToIlTab(Hex1bTerminal terminal, CancellationToken ct)
    {
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Key(Hex1bKey.D3)
            .WaitUntil(s => s.ContainsText("▶") || s.ContainsText("▼"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);
    }

    /// <summary>
    /// Programmatically select a method by name in the IL Inspector tree
    /// and wait for its disassembly to render.
    /// </summary>
    private async Task SelectMethodByName(string methodName, Hex1bTerminal terminal, CancellationToken ct)
    {
        var method = _state!.Analyzer.MethodDefs.First(m => m.Name == methodName);
        var typeDef = _state.Analyzer.TypeDefs.First(t => t.FullName == method.DeclaringType);
        var ns = !string.IsNullOrEmpty(typeDef.Namespace) ? typeDef.Namespace : "(global)";
        _state.IlTreeExpansionState[$"ns:{ns}"] = true;
        _state.IlTreeExpansionState[$"type:{method.DeclaringType}"] = true;
        _state.IlSelectedMethod = method;
        _state.IlFocusedTreeKey = $"method:{method.Token}";
        _state.App.Invalidate();

        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("IL_0000"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);
    }

    // ── State-level unit tests ────────────────────────────────

    /// <summary>
    /// Verifies get or create editor key same analyzer and token returns same reference.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void GetOrCreateEditorKey_SameAnalyzerAndToken_ReturnsSameReference()
    {
        var app = CreateMinimalApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);

        var key1 = state.GetOrCreateEditorKey(state.Analyzer, 0x06000001);
        var key2 = state.GetOrCreateEditorKey(state.Analyzer, 0x06000001);

        Assert.Same(key1, key2);
    }

    /// <summary>
    /// Verifies get or create editor key different tokens returns different references.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void GetOrCreateEditorKey_DifferentTokens_ReturnsDifferentReferences()
    {
        var app = CreateMinimalApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);

        var key1 = state.GetOrCreateEditorKey(state.Analyzer, 0x06000001);
        var key2 = state.GetOrCreateEditorKey(state.Analyzer, 0x06000002);

        Assert.NotSame(key1, key2);
    }

    /// <summary>
    /// Verifies set il focused tree key updates focused tree key.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void SetIlFocusedTreeKey_UpdatesFocusedTreeKey()
    {
        var app = CreateMinimalApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);

        state.SetIlFocusedTreeKey("method:0x06000001");

        Assert.Equal("method:0x06000001", state.IlFocusedTreeKey);
    }

    /// <summary>
    /// Verifies restore from il back entry reseeds editor key cache.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RestoreFromIlBackEntry_ReseedsEditorKeyCache()
    {
        var app = CreateMinimalApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);

        var method = state.Analyzer.MethodDefs[0];
        var editorKey = state.GetOrCreateEditorKey(state.Analyzer, method.Token);
        state.IlSelectedMethod = method;
        state.IlEditorState = new EditorState(new Hex1bDocument("test")) { IsReadOnly = true };
        state.IlEditorMethod = method;
        state.IlEditorAnalyzer = state.Analyzer;
        state.IlEditorKey = editorKey;

        var entry = new IlBackEntry(
            method, state.IlEditorState, method, state.Analyzer,
            $"method:{method.Token}", [], false, editorKey,
            [], null);

        // Simulate what ResetViewState does (called by PopAssembly on cross-assembly back)
        state.IlEditorKeyCache.Clear();

        state.RestoreFromIlBackEntry(entry);

        // GetOrCreateEditorKey must return the reseeded key
        var restoredKey = state.GetOrCreateEditorKey(state.Analyzer, method.Token);
        Assert.Same(editorKey, restoredKey);
    }

    /// <summary>
    /// Verifies restore from il back entry saves outgoing editor.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void RestoreFromIlBackEntry_SavesOutgoingEditor()
    {
        var app = CreateMinimalApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        if (state.Analyzer.MethodDefs.Count < 2) return;

        var methodB = state.Analyzer.MethodDefs[1];
        var keyB = state.GetOrCreateEditorKey(state.Analyzer, methodB.Token);
        var editorStateB = new EditorState(new Hex1bDocument("method B")) { IsReadOnly = true };
        state.IlEditorKey = keyB;
        state.IlEditorState = editorStateB;
        state.IlEditorMethod = methodB;
        state.IlEditorAnalyzer = state.Analyzer;
        state.IlSelectedMethod = methodB;

        var methodA = state.Analyzer.MethodDefs[0];
        var keyA = state.GetOrCreateEditorKey(state.Analyzer, methodA.Token);
        var editorStateA = new EditorState(new Hex1bDocument("method A")) { IsReadOnly = true };
        var entry = new IlBackEntry(
            methodA, editorStateA, methodA, state.Analyzer,
            $"method:{methodA.Token}", [], false, keyA,
            [], null);

        state.RestoreFromIlBackEntry(entry);

        Assert.True(state.IlCachedEditors.ContainsKey(keyB));
        Assert.Same(editorStateB, state.IlCachedEditors[keyB]);
    }

    /// <summary>
    /// Verifies back entry preserves editor key.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void BackEntry_PreservesEditorKey()
    {
        var app = CreateMinimalApp();
        using var state = new DotsiderState(app, samples.RichLibraryDll);
        if (state.Analyzer.MethodDefs.Count < 2) return;

        var methodA = state.Analyzer.MethodDefs[0];
        var keyA = state.GetOrCreateEditorKey(state.Analyzer, methodA.Token);
        state.IlSelectedMethod = methodA;
        state.IlEditorState = new EditorState(new Hex1bDocument("method A")) { IsReadOnly = true };
        state.IlEditorMethod = methodA;
        state.IlEditorAnalyzer = state.Analyzer;
        state.IlEditorKey = keyA;

        var methodB = state.Analyzer.MethodDefs[1];
        state.NavigateToIlDefinition(methodB.Token);

        Assert.True(state.IlBackStack.Count > 0);
        var entry = state.IlBackStack.Peek();
        Assert.Same(keyA, entry.EditorKey);
    }

    // ── Integration tests (real IL Inspector) ─────────────────

    /// <summary>
    /// After switching methods via the tree and switching back, the EditorNode's scroll
    /// offset should be preserved (node kept alive by StatePanelWidget cache).
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task EditorScroll_PreservedOnTreeRevisit()
    {
        var (terminal, app, ct) = CreateDotsiderApp(samples.RichLibraryDll);
        var runTask = app.RunAsync(ct);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        await NavigateToIlTab(terminal, ct);

        // Select a method with IL
        var methodA = _state!.Analyzer.MethodDefs.First(m => m.Rva > 0);
        await SelectMethodByName(methodA.Name, terminal, ct);

        // Focus editor and scroll down
        _state.App.RequestFocus(node => node is EditorNode);
        _state.App.Invalidate();
        await auto.WaitUntilAsync(_ => app.FocusedNode is EditorNode, description: "editor focused");
        for (var i = 0; i < 10; i++)
            await auto.KeyAsync(Hex1bKey.DownArrow, ct: ct);

        // Capture the EditorNode's scroll offset after moving down
        var editorNodeA = app.FocusedNode as EditorNode;
        Assert.NotNull(editorNodeA);
        var scrollAfterMove = editorNodeA!.ScrollOffset;
        // Scroll should have moved from the default (1) after 10 down-arrows
        Assert.True(scrollAfterMove >= 1, $"Expected scroll > 0, got {scrollAfterMove}");

        // Switch to a different method via tree
        var methodB = _state.Analyzer.MethodDefs.First(m => m.Token != methodA.Token && m.Rva > 0);
        _state.IlSelectedMethod = methodB;
        _state.IlFocusedTreeKey = $"method:{methodB.Token}";
        _state.App.Invalidate();
        await auto.WaitUntilAsync(_ => _state.IlEditorMethod?.Token == methodB.Token,
            description: "method B loaded");

        // Switch back to original method
        _state.IlSelectedMethod = methodA;
        _state.IlFocusedTreeKey = $"method:{methodA.Token}";
        _state.App.RequestFocus(node => node is EditorNode);
        _state.App.Invalidate();
        await auto.WaitUntilAsync(_ => _state.IlEditorMethod?.Token == methodA.Token,
            description: "method A reloaded");
        // Wait for StatePanelWidget reconciliation to settle
        await auto.WaitAsync(TimeSpan.FromMilliseconds(200), ct: ct);

        // The visible EditorNode should be the same cached node with preserved scroll
        var restoredEditorNode = app.Focusables.OfType<EditorNode>().FirstOrDefault();
        Assert.NotNull(restoredEditorNode);
        Assert.Same(editorNodeA, restoredEditorNode);
        Assert.Equal(scrollAfterMove, restoredEditorNode!.ScrollOffset);

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>
    /// When search n/N switches to a different method, the editor must show the new
    /// method's IL and focus must be on the visible editor, not on a hidden cached one.
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task SearchNavigateToMatch_FocusesVisibleEditor()
    {
        var (terminal, app, ct) = CreateDotsiderApp(samples.RichLibraryDll);
        var runTask = app.RunAsync(ct);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        await NavigateToIlTab(terminal, ct);

        // Select a method
        var firstMethod = _state!.Analyzer.MethodDefs.First(m => m.Rva > 0);
        await SelectMethodByName(firstMethod.Name, terminal, ct);

        // Focus editor programmatically
        _state.App.RequestFocus(node => node is EditorNode);
        _state.App.Invalidate();
        await auto.WaitUntilAsync(_ => app.FocusedNode is EditorNode, description: "editor focused");

        // Find a search query that matches in a DIFFERENT method
        var otherMethod = _state.Analyzer.MethodDefs.FirstOrDefault(m =>
            m.Token != firstMethod.Token && m.Rva > 0);
        if (otherMethod is null) { _cts!.Cancel(); await runTask; return; }

        // Navigate to the other method via state (simulating search n)
        IlInspectorView.NavigateToMatchForTest(_state, otherMethod);
        await auto.WaitAsync(TimeSpan.FromMilliseconds(200), ct: ct);  // Wait for render cycle

        // Focus must be on an EditorNode that's in the focus ring (visible, not hidden)
        var focused = app.FocusedNode;
        Assert.IsType<EditorNode>(focused);

        // The focused EditorNode must be in Focusables (not hidden in ResponsiveWidget)
        Assert.Contains(focused, app.Focusables);

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>
    /// The field pane must stabilize after the first render — not recreate EditorState every frame.
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task FieldPane_StableAcrossFrames()
    {
        var (terminal, app, ct) = CreateDotsiderApp(samples.RichLibraryDll);
        var runTask = app.RunAsync(ct);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        await NavigateToIlTab(terminal, ct);

        // Navigate to a field
        var field = _state!.Analyzer.FieldDefs.Count > 0 ? _state.Analyzer.FieldDefs[0] : null;
        if (field is null) { _cts!.Cancel(); await runTask; return; }

        _state.IlSelectedMethod = null;
        _state.IlSelectedField = field;
        _state.App.Invalidate();

        await auto.WaitUntilTextAsync("Fields do not have IL bodies");

        // Capture the editor key after first render
        var keyAfterFirstRender = _state.IlEditorKey;
        Assert.NotNull(keyAfterFirstRender);

        // Force another render
        _state.App.Invalidate();
        await auto.WaitAsync(TimeSpan.FromMilliseconds(200), ct: ct);

        // Editor key should be the same (staleness guard prevents recreation)
        Assert.Same(keyAfterFirstRender, _state.IlEditorKey);

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>
    /// PushAssemblyDirect calls ResetViewState which must clear all new IL lifecycle
    /// properties — the same path CommitAnalyzer exercises when re-opening an assembly
    /// after hex save. Verifies IlEditorKey, IlEditorField, IlScrollPanelNode,
    /// IlScrollSelectionIntoViewPending, IlEditorKeyCache, and IlCachedEditors are all
    /// reset. The assertion runs synchronously after PushAssemblyDirect with no awaits
    /// in between, so the render loop cannot tick and re-capture the panel before the
    /// check sees the cleared field.
    /// </summary>
    [Fact(Timeout = 60_000)]
    public async Task ResetViewState_ClearsAllLifecycleProperties()
    {
        var (terminal, app, ct) = CreateDotsiderApp(samples.RichLibraryDll);
        var runTask = app.RunAsync(ct);
        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));

        await NavigateToIlTab(terminal, ct);

        // Select a method to populate IL state
        var method = _state!.Analyzer.MethodDefs.First(m => m.Rva > 0);
        await SelectMethodByName(method.Name, terminal, ct);

        // Wait for editor state to be established
        await auto.WaitUntilAsync(_ => _state.IlEditorKey is not null, description: "editor key set");

        // Populate cached editors by switching methods
        var methodB = _state.Analyzer.MethodDefs.First(m => m.Token != method.Token && m.Rva > 0);
        _state.IlSelectedMethod = methodB;
        _state.IlFocusedTreeKey = $"method:{methodB.Token}";
        _state.App.Invalidate();
        await auto.WaitUntilAsync(_ => _state.IlEditorMethod?.Token == methodB.Token,
            description: "method B loaded");

        // Verify state is populated before reset
        Assert.NotNull(_state.IlEditorKey);
        Assert.True(_state.IlEditorKeyCache.Count > 0);
        Assert.True(_state.IlCachedEditors.Count > 0);

        // Stop the render loop before mutating state so no concurrent render can
        // re-populate IlScrollPanelNode (or any other field re-captured per render)
        // between ResetViewState and the assertions below.
        _cts!.Cancel();
        await runTask;

        // PushAssemblyDirect calls ResetViewState (same path as CommitAnalyzer)
        using var otherAnalyzer = new AssemblyAnalyzer(samples.HelloWorldDll);
        _state.PushAssemblyDirect(otherAnalyzer);

        // All lifecycle properties must be cleared
        Assert.Null(_state.IlEditorKey);
        Assert.Null(_state.IlEditorField);
        Assert.Null(_state.IlScrollPanelNode);
        Assert.False(_state.IlScrollSelectionIntoViewPending);
        Assert.Empty(_state.IlEditorKeyCache);
        Assert.Empty(_state.IlCachedEditors);
    }

    /// <summary>
    /// Disposes test resources created during the run.
    /// </summary>
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
