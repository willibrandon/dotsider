using Dotsider.Views;
using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Behavior tests for the IL Inspector tree's <see cref="ScrollPanelNode"/>-hosted
/// scrollbar and non-wrapping selection. Issue #167.
/// </summary>
[TestClass]
public class IlInspectorScrollbarTests : IDisposable
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private Hex1bAppWorkloadAdapter? _workload;
    private Hex1bTerminal? _terminal;
    private Hex1bApp? _hex1bApp;
    private DotsiderState? _state;
    private CancellationTokenSource? _cts;
    private Task? _runTask;

    /// <summary>
    /// Creates a dotsider test app with mouse input enabled so wheel/drag/track tests can
    /// drive the <see cref="ScrollPanelNode"/>'s default mouse bindings. Mirrors
    /// <c>StandardModeViewTests.CreateDotsiderAppWithMouse</c>.
    /// </summary>
    /// <param name="dllPath">The sample assembly to open.</param>
    /// <param name="enableInputCoalescing">Whether to coalesce input events; opt in for the
    /// drag/key race coverage and the live-selection coalesced tests.</param>
    /// <returns>The terminal, app, and a cancellation token tied to the test scope.</returns>
    private (Hex1bTerminal terminal, Hex1bApp app, CancellationToken ct) CreateMouseApp(
        string dllPath, bool enableInputCoalescing = false)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
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
                EnableInputCoalescing = enableInputCoalescing,
                EnableMouse = true,
            });
        return (_terminal, _hex1bApp, _cts.Token);
    }

    private Task RunAppAsync(Hex1bApp app, CancellationToken ct)
    {
        _runTask = app.RunAsync(ct);
        return _runTask;
    }

    private bool TryWaitForAppExit()
    {
        if (_runTask is null) return true;
        try { return _runTask.Wait(TimeSpan.FromSeconds(5)); }
        catch (AggregateException ex) when (ex.InnerExceptions.All(static e => e is OperationCanceledException)) { return true; }
        catch (OperationCanceledException) { return true; }
    }

    /// <summary>
    /// Programmatically expands every namespace and every type in the tree so the
    /// flattened-row count is large enough to exceed the viewport on the default 120×30
    /// terminal. This is the precedent-style way to force scroll overflow without
    /// resorting to non-fixture assemblies.
    /// </summary>
    /// <param name="state">The shared application state.</param>
    private static void ExpandAllTypes(DotsiderState state)
    {
        foreach (var ns in state.Analyzer.TypeDefs
            .Select(t => string.IsNullOrEmpty(t.Namespace) ? "(global)" : t.Namespace).Distinct())
            state.IlTreeExpansionState[$"ns:{ns}"] = true;
        foreach (var t in state.Analyzer.TypeDefs)
            state.IlTreeExpansionState[$"type:{t.FullName}"] = true;
    }

    private static async Task SwitchToIlAsync(Hex1bTerminal terminal, CancellationToken ct)
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
    /// Waits for the General tab to finish its first render (so <c>_state</c> exists),
    /// runs the supplied state mutation, then switches to the IL Inspector tab. State
    /// mutations applied here land in the closure of the IL tab's first render —
    /// without this ordering, the panel's binding closure captures default-expansion
    /// rows and any subsequent expansion is invisible to keyboard handlers until the
    /// next render fires.
    /// </summary>
    private async Task SetupIlTabAsync(Hex1bTerminal terminal, Action<DotsiderState>? mutate, CancellationToken ct)
    {
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.InAlternateScreen, TimeSpan.FromSeconds(10))
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);
        if (mutate is not null && _state is not null) mutate(_state);
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.D3)
            .WaitUntil(s => s.ContainsText("▶") || s.ContainsText("▼"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);
    }

    /// <summary>
    /// Copies the focus ring to an array, treating the transient
    /// <see cref="InvalidOperationException"/> a concurrent render-thread rebuild can raise
    /// mid-enumeration as an empty result. Hex1b rebuilds the ring (a plain list) after every
    /// frame, so a read from the test poll thread must tolerate that race and retry rather than
    /// fault the test.
    /// </summary>
    private static Hex1bNode[] SnapshotFocusables(Hex1bApp app)
    {
        try { return [.. app.Focusables]; }
        catch (InvalidOperationException) { return []; }
    }

    private static ScrollPanelNode? FindPanel(Hex1bApp app)
        => SnapshotFocusables(app).OfType<ScrollPanelNode>().FirstOrDefault();

    /// <summary>
    /// Whether the tree overflows the panel viewport. The panel's own IsScrollable is
    /// always false under the windowed tree (its child is viewport-sized); scrollability
    /// is a property of the row count against the viewport.
    /// </summary>
    private bool TreeScrollable(ScrollPanelNode sp)
        => sp.ViewportSize > 0
           && Views.IlInspectorView.BuildTreeRows(_state!).Count > sp.ViewportSize;

    /// <summary>The tree's maximum scroll offset for the current rows and viewport.</summary>
    private int TreeMaxOffset(ScrollPanelNode sp)
        => Math.Max(0, Views.IlInspectorView.BuildTreeRows(_state!).Count - sp.ViewportSize);

    /// <summary>The tree's state-owned scroll offset (first visible row index).</summary>
    private int TreeOffset => _state!.IlTreeScrollOffset;

    private static async Task<ScrollPanelNode> WaitForPanelAsync(
        Hex1bTerminalAutomator auto, Hex1bApp app)
    {
        // Single predicate that re-evaluates the panel reference on every poll, so
        // a brief stretch where the focus ring rebuilds (e.g. tab switch reconciles
        // the panel out and back in) cannot leave a captured `sp` null between two
        // sequential WaitUntilAsync calls. The reference is only published to the
        // caller once all three conditions hold simultaneously on the same poll.
        ScrollPanelNode? captured = null;
        await auto.WaitUntilAsync(_ =>
        {
            // Read the panel and the focused node from one snapshot so a rebuild between the two
            // reads cannot yield an inconsistent pair.
            var focusables = SnapshotFocusables(app);
            var current = focusables.OfType<ScrollPanelNode>().FirstOrDefault();
            if (current is null || current.ViewportSize <= 0) return false;
            if (focusables.FirstOrDefault(n => n.IsFocused) is not ScrollPanelNode focused
                || !ReferenceEquals(focused, current)) return false;
            captured = current;
            return true;
        }, description: "ScrollPanelNode focused, arranged, and stable");
        return captured!;
    }

    private static int FirstThumbY(Hex1bTerminalSnapshot snapshot, int sbCol, int yStart, int yEnd)
    {
        for (var y = yStart; y < yEnd; y++)
        {
            if (snapshot.GetCell(sbCol, y).Character == "▉")
                return y;
        }
        return -1;
    }

    /// <summary>
    /// Waits until at least one scrollbar thumb cell is painted, then returns its Y.
    /// </summary>
    private static async Task<int> WaitForThumbAsync(
        Hex1bTerminalAutomator auto,
        Hex1bTerminal terminal,
        ScrollPanelNode sp)
    {
        var sbCol = sp.Bounds.X + sp.Bounds.Width - 1;
        await auto.WaitUntilAsync(s =>
            FirstThumbY(s, sbCol, sp.Bounds.Y, sp.Bounds.Y + sp.Bounds.Height) >= 0,
            description: "scrollbar thumb to paint");
        return FirstThumbY(terminal.CreateSnapshot(), sbCol, sp.Bounds.Y, sp.Bounds.Y + sp.Bounds.Height);
    }

    /// <summary>
    /// Assigns <see cref="DotsiderState.IlFocusedTreeKey"/> directly (bypassing the
    /// pending-scroll arming) and waits until the binding closure on the panel has
    /// actually picked up the new rows. The closure refreshes only on the next
    /// IL Inspector render; we invalidate, then poll <see cref="ScrollPanelNode.ContentSize"/>
    /// against the expected visible-window height — the windowed tree's child measures
    /// min(viewport, rows) tall — so agreement proves a render with the post-mutation
    /// rows has been arranged into the panel and the binding closure is current.
    /// </summary>
    private static async Task SetSelectionDirectAsync(
        Hex1bTerminalAutomator auto,
        Hex1bApp app,
        DotsiderState state,
        List<IlTreeRow> rows,
        int targetIndex)
    {
        state.IlFocusedTreeKey = rows[targetIndex].Key;
        if (rows[targetIndex] is { Kind: IlTreeRowKind.Method, Method: not null } m)
            state.IlSelectedMethod = m.Method;
        state.App.Invalidate();
        await auto.WaitUntilAsync(_ =>
        {
            var sp = FindPanel(app);
            return sp is { ViewportSize: > 0 }
                && sp.ContentSize == Math.Min(sp.ViewportSize, rows.Count);
        }, description: "panel ContentSize agrees with the visible window");

        app.RequestFocus(node => node is ScrollPanelNode);
        app.Invalidate();
        await auto.WaitUntilAsync(_ => SnapshotFocusables(app).FirstOrDefault(n => n.IsFocused) is ScrollPanelNode,
            description: "ScrollPanelNode focused after direct selection");
    }

    /// <summary>
    /// First arrival paints the scrollbar without requiring an extra keystroke.
    /// Pins the bootstrap-invalidate fix.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab3_TreeScrollbar_RendersOnFirstArrival_WithoutExtraInput()
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.RichLibraryDll);
        var runTask = RunAppAsync(app, ct);
        await SetupIlTabAsync(terminal, ExpandAllTypes, ct);
        _state!.App.Invalidate();

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        var sp = await WaitForPanelAsync(auto, app);

        await auto.WaitUntilAsync(s =>
        {
            var sbCol = sp.Bounds.X + sp.Bounds.Width - 1;
            for (var y = sp.Bounds.Y; y < sp.Bounds.Y + sp.Bounds.Height; y++)
                if (s.GetCell(sbCol, y).Character == "▉") return true;
            return false;
        }, description: "thumb cell to paint without further input");

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>When the tree exceeds the viewport, the scrollbar paints a thumb cell in the panel's rightmost column.</summary>

    [TestMethod]

    [Timeout(30_000, CooperativeCancellation = true)]

    public async Task Tab3_TreeScrollbar_RendersAtRightEdge_WhenContentExceedsViewport()
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.RichLibraryDll);
        var runTask = RunAppAsync(app, ct);
        await SetupIlTabAsync(terminal, ExpandAllTypes, ct);
        _state!.App.Invalidate();

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        var sp = await WaitForPanelAsync(auto, app);
        await auto.WaitUntilAsync(_ => TreeScrollable(sp), description: "tree becomes scrollable");

        var thumbY = await WaitForThumbAsync(auto, terminal, sp);
        Assert.IsGreaterThanOrEqualTo(sp.Bounds.Y, thumbY, "thumb cell rendered inside panel bounds");

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>When all rows fit the viewport, no thumb cell is painted.</summary>

    [TestMethod]

    [Timeout(30_000, CooperativeCancellation = true)]

    public async Task Tab3_TreeScrollbar_HiddenWhenContentFits()
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.HelloWorldDll);
        var runTask = RunAppAsync(app, ct);
        await SwitchToIlAsync(terminal, ct);

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        var sp = await WaitForPanelAsync(auto, app);
        await auto.WaitUntilAsync(_ => !TreeScrollable(sp), description: "content fits viewport");

        var snapshot = terminal.CreateSnapshot();
        var sbCol = sp.Bounds.X + sp.Bounds.Width - 1;
        var hasThumb = FirstThumbY(snapshot, sbCol, sp.Bounds.Y, sp.Bounds.Y + sp.Bounds.Height) >= 0;
        Assert.IsFalse(hasThumb, "No thumb expected when content fits");

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>DownArrow at the last row is a no-op (clamp, not wrap).</summary>

    [TestMethod]

    [Timeout(30_000, CooperativeCancellation = true)]

    public async Task Tab3_DownArrow_AtBottom_DoesNotWrap()
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.RichLibraryDll);
        var runTask = RunAppAsync(app, ct);
        await SetupIlTabAsync(terminal, ExpandAllTypes, ct);

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        await WaitForPanelAsync(auto, app);

        var rows = Views.IlInspectorView.BuildTreeRows(_state!);
        await SetSelectionDirectAsync(auto, app, _state!, rows, rows.Count - 1);
        var lastKey = rows[^1].Key;

        await auto.KeyAsync(Hex1bKey.DownArrow, ct: ct);
        await Task.Delay(50, ct);

        Assert.AreEqual(lastKey, _state!.IlFocusedTreeKey as string);

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>UpArrow at the first row is a no-op (clamp, not wrap).</summary>

    [TestMethod]

    [Timeout(30_000, CooperativeCancellation = true)]

    public async Task Tab3_UpArrow_AtTop_DoesNotWrap()
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.RichLibraryDll);
        var runTask = RunAppAsync(app, ct);
        await SwitchToIlAsync(terminal, ct);

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        await WaitForPanelAsync(auto, app);

        var rows = Views.IlInspectorView.BuildTreeRows(_state!);
        await SetSelectionDirectAsync(auto, app, _state!, rows, 0);
        var firstKey = rows[0].Key;

        await auto.KeyAsync(Hex1bKey.UpArrow, ct: ct);
        await Task.Delay(50, ct);

        Assert.AreEqual(firstKey, _state!.IlFocusedTreeKey as string);

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>DownArrow advances selection past the viewport bottom; the panel offset follows.</summary>

    [TestMethod]

    [Timeout(30_000, CooperativeCancellation = true)]

    public async Task Tab3_DownArrow_MovesSelection_AndScrollsViewportToFollow()
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.RichLibraryDll);
        var runTask = RunAppAsync(app, ct);
        await SetupIlTabAsync(terminal, ExpandAllTypes, ct);

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        var sp = await WaitForPanelAsync(auto, app);
        await auto.WaitUntilAsync(_ => TreeScrollable(sp),
            description: "viewport sized and scrollable");

        var rows = Views.IlInspectorView.BuildTreeRows(_state!);
        await SetSelectionDirectAsync(auto, app, _state!, rows, 0);

        // Press DownArrow until selection passes the visible viewport. Each press
        // is paired with a wait so the panel's keyboard binding must observe each key.
        var target = Math.Min(rows.Count - 1, sp.ViewportSize + 2);
        var targetKey = rows[target].Key;
        for (var i = 0; i < target + 5 && _state!.IlFocusedTreeKey as string != targetKey; i++)
        {
            var prev = _state!.IlFocusedTreeKey as string;
            await auto.KeyAsync(Hex1bKey.DownArrow, ct: ct);
            await auto.WaitUntilAsync(_ => _state!.IlFocusedTreeKey as string != prev,
                timeout: TimeSpan.FromSeconds(2),
                description: $"DownArrow #{i + 1} advances from {prev}");
        }

        Assert.IsGreaterThan(0, TreeOffset, $"Offset should advance after walking past viewport. ViewportSize={sp.ViewportSize}, target={target}, Offset={TreeOffset}");

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>PageDown advances selection by Math.Max(1, ViewportSize - 1).</summary>

    [TestMethod]

    [Timeout(30_000, CooperativeCancellation = true)]

    public async Task Tab3_PageDown_AdvancesByPanelPageSize()
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.RichLibraryDll);
        var runTask = RunAppAsync(app, ct);
        await SetupIlTabAsync(terminal, ExpandAllTypes, ct);

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        var sp = await WaitForPanelAsync(auto, app);
        await auto.WaitUntilAsync(_ => sp.ViewportSize > 0, description: "viewport sized");

        var rows = Views.IlInspectorView.BuildTreeRows(_state!);
        await SetSelectionDirectAsync(auto, app, _state!, rows, 0);

        var step = Math.Max(1, sp.ViewportSize - 1);
        var expectedKey = rows[Math.Min(rows.Count - 1, step)].Key;
        await auto.KeyAsync(Hex1bKey.PageDown, ct: ct);
        await auto.WaitUntilAsync(_ => _state!.IlFocusedTreeKey as string == expectedKey,
            description: "PageDown advances by page size");

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>PageUp retreats selection by Math.Max(1, ViewportSize - 1).</summary>

    [TestMethod]

    [Timeout(30_000, CooperativeCancellation = true)]

    public async Task Tab3_PageUp_RetreatsByPanelPageSize()
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.RichLibraryDll);
        var runTask = RunAppAsync(app, ct);
        await SetupIlTabAsync(terminal, ExpandAllTypes, ct);

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        var sp = await WaitForPanelAsync(auto, app);
        await auto.WaitUntilAsync(_ => sp.ViewportSize > 0, description: "viewport sized");

        var rows = Views.IlInspectorView.BuildTreeRows(_state!);
        await auto.KeyAsync(Hex1bKey.End, ct: ct);
        await auto.WaitUntilAsync(_ => _state!.IlFocusedTreeKey as string == rows[^1].Key,
            description: "last row selected via End");

        var step = Math.Max(1, sp.ViewportSize - 1);
        var expectedIdx = Math.Max(0, rows.Count - 1 - step);
        var expectedKey = rows[expectedIdx].Key;
        await auto.KeyAsync(Hex1bKey.PageUp, ct: ct);
        await auto.WaitUntilAsync(_ => _state!.IlFocusedTreeKey as string == expectedKey,
            description: "PageUp retreats by page size");

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>Home jumps selection to row 0 and resets the panel offset to zero.</summary>

    [TestMethod]

    [Timeout(30_000, CooperativeCancellation = true)]

    public async Task Tab3_Home_JumpsToFirstRow_AndOffsetReturnsToZero()
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.RichLibraryDll);
        var runTask = RunAppAsync(app, ct);
        await SetupIlTabAsync(terminal, ExpandAllTypes, ct);

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        var sp = await WaitForPanelAsync(auto, app);

        var rows = Views.IlInspectorView.BuildTreeRows(_state!);
        await auto.KeyAsync(Hex1bKey.End, ct: ct);
        await auto.WaitUntilAsync(_ => _state!.IlFocusedTreeKey as string == rows[^1].Key,
            description: "last row selected");
        await auto.KeyAsync(Hex1bKey.Home, ct: ct);
        await auto.WaitUntilAsync(_ => _state!.IlFocusedTreeKey as string == rows[0].Key,
            description: "Home jumps to row 0");
        await auto.WaitUntilAsync(_ => TreeOffset == 0, description: "Offset returns to 0");

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>End jumps selection to the last row and pushes the panel offset to MaxOffset.</summary>

    [TestMethod]

    [Timeout(30_000, CooperativeCancellation = true)]

    public async Task Tab3_End_JumpsToLastRow_AndOffsetReachesMax()
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.RichLibraryDll);
        var runTask = RunAppAsync(app, ct);
        await SetupIlTabAsync(terminal, ExpandAllTypes, ct);

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        var sp = await WaitForPanelAsync(auto, app);

        var rows = Views.IlInspectorView.BuildTreeRows(_state!);
        await auto.KeyAsync(Hex1bKey.End, ct: ct);
        await auto.WaitUntilAsync(_ => _state!.IlFocusedTreeKey as string == rows[^1].Key,
            description: "End selects last row");
        await auto.WaitUntilAsync(_ => TreeOffset == TreeMaxOffset(sp),
            description: "Offset hits MaxOffset");

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>
    /// Mouse wheel over the tree body advances the panel offset by exactly 3 (the
    /// hex1b default) without changing selection. Selection-coupled designs would
    /// fail this — the test pins the ScrollPanel-as-focusable architecture.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab3_MouseWheelDown_OverTreeBody_AdvancesOffsetBy3_WithoutChangingSelection()
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.RichLibraryDll);
        var runTask = RunAppAsync(app, ct);
        await SetupIlTabAsync(terminal, ExpandAllTypes, ct);

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        var sp = await WaitForPanelAsync(auto, app);
        await auto.WaitUntilAsync(_ => TreeScrollable(sp), description: "tree scrollable");

        var rows = Views.IlInspectorView.BuildTreeRows(_state!);
        await SetSelectionDirectAsync(auto, app, _state!, rows, 0);

        var initialOffset = TreeOffset;
        var initialKey = _state!.IlFocusedTreeKey as string;
        var bodyX = sp.Bounds.X + 5;
        var bodyY = sp.Bounds.Y + 5;
        await new Hex1bTerminalInputSequenceBuilder()
            .MouseMoveTo(bodyX, bodyY)
            .ScrollDown()
            .Build()
            .ApplyAsync(terminal, ct);
        await auto.WaitUntilAsync(_ => TreeOffset == initialOffset + 3,
            description: "Offset advanced by exactly 3");

        Assert.AreEqual(initialKey, _state!.IlFocusedTreeKey as string);

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>Wheel-up over the tree at offset zero is a no-op for both offset and selection.</summary>

    [TestMethod]

    [Timeout(30_000, CooperativeCancellation = true)]

    public async Task Tab3_MouseWheelUp_AtTop_OffsetStaysAtZero()
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.RichLibraryDll);
        var runTask = RunAppAsync(app, ct);
        await SetupIlTabAsync(terminal, ExpandAllTypes, ct);

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        var sp = await WaitForPanelAsync(auto, app);
        await auto.WaitUntilAsync(_ => TreeScrollable(sp), description: "tree scrollable");

        var bodyX = sp.Bounds.X + 5;
        var bodyY = sp.Bounds.Y + 5;
        var initialKey = _state!.IlFocusedTreeKey as string;
        await new Hex1bTerminalInputSequenceBuilder()
            .MouseMoveTo(bodyX, bodyY)
            .ScrollUp()
            .ScrollUp()
            .Build()
            .ApplyAsync(terminal, ct);
        await Task.Delay(50, ct);

        Assert.AreEqual(0, TreeOffset);
        Assert.AreEqual(initialKey, _state!.IlFocusedTreeKey as string);

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>Wheel-down over the tree at MaxOffset clamps; selection is unchanged.</summary>

    [TestMethod]

    [Timeout(30_000, CooperativeCancellation = true)]

    public async Task Tab3_MouseWheelDown_AtBottom_ClampsAtMaxOffset()
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.RichLibraryDll);
        var runTask = RunAppAsync(app, ct);
        await SetupIlTabAsync(terminal, ExpandAllTypes, ct);

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        var sp = await WaitForPanelAsync(auto, app);
        await auto.WaitUntilAsync(_ => TreeScrollable(sp), description: "tree scrollable");

        _state!.IlTreeScrollOffset = TreeMaxOffset(sp);
        _state.App.Invalidate();
        var initialKey = _state.IlFocusedTreeKey as string;
        var bodyX = sp.Bounds.X + 5;
        var bodyY = sp.Bounds.Y + 5;
        await new Hex1bTerminalInputSequenceBuilder()
            .MouseMoveTo(bodyX, bodyY)
            .ScrollDown()
            .ScrollDown()
            .Build()
            .ApplyAsync(terminal, ct);
        await Task.Delay(50, ct);

        Assert.AreEqual(TreeMaxOffset(sp), TreeOffset);
        Assert.AreEqual(initialKey, _state!.IlFocusedTreeKey as string);

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>Click on the scrollbar track below the thumb pages the viewport by one page; selection is unchanged.</summary>

    [TestMethod]

    [Timeout(30_000, CooperativeCancellation = true)]

    public async Task Tab3_ScrollbarTrackClick_PagesViewport_WithoutChangingSelection()
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.RichLibraryDll);
        var runTask = RunAppAsync(app, ct);
        await SetupIlTabAsync(terminal, ExpandAllTypes, ct);

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        var sp = await WaitForPanelAsync(auto, app);
        await auto.WaitUntilAsync(_ => TreeScrollable(sp), description: "tree scrollable");

        // Find a thumb cell, then click below it on the track.
        var sbCol = sp.Bounds.X + sp.Bounds.Width - 1;
        var thumbY = await WaitForThumbAsync(auto, terminal, sp);

        var initialOffset = TreeOffset;
        var initialKey = _state!.IlFocusedTreeKey as string;
        var trackY = sp.Bounds.Y + sp.Bounds.Height - 2; // below the thumb
        var expectedStep = Math.Max(1, sp.ViewportSize - 1);

        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(sbCol, trackY)
            .Build()
            .ApplyAsync(terminal, ct);
        await auto.WaitUntilAsync(_ => TreeOffset == Math.Min(TreeMaxOffset(sp), initialOffset + expectedStep),
            description: "track click pages viewport");

        Assert.AreEqual(initialKey, _state!.IlFocusedTreeKey as string);

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>Drag on the thumb advances the panel offset; selection is unchanged.</summary>

    [TestMethod]

    [Timeout(30_000, CooperativeCancellation = true)]

    public async Task Tab3_ScrollbarThumbDrag_UpdatesOffset_WithoutChangingSelection()
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.RichLibraryDll);
        var runTask = RunAppAsync(app, ct);
        await SetupIlTabAsync(terminal, ExpandAllTypes, ct);

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        var sp = await WaitForPanelAsync(auto, app);
        await auto.WaitUntilAsync(_ => TreeScrollable(sp), description: "tree scrollable");

        var sbCol = sp.Bounds.X + sp.Bounds.Width - 1;
        var thumbY = await WaitForThumbAsync(auto, terminal, sp);

        var initialKey = _state!.IlFocusedTreeKey as string;
        var dragDistance = 3;
        await new Hex1bTerminalInputSequenceBuilder()
            .Drag(sbCol, thumbY, sbCol, thumbY + dragDistance)
            .Build()
            .ApplyAsync(terminal, ct);
        await auto.WaitUntilAsync(_ => TreeOffset > 0, description: "drag advanced offset");

        Assert.AreEqual(initialKey, _state!.IlFocusedTreeKey as string);

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>The thumb cell moves further down the gutter when offset moves from 0 to MaxOffset.</summary>

    [TestMethod]

    [Timeout(30_000, CooperativeCancellation = true)]

    public async Task Tab3_ScrollbarThumbReflectsScrollOffset()
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.RichLibraryDll);
        var runTask = RunAppAsync(app, ct);
        await SetupIlTabAsync(terminal, ExpandAllTypes, ct);

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        var sp = await WaitForPanelAsync(auto, app);
        await auto.WaitUntilAsync(_ => TreeScrollable(sp), description: "tree scrollable");

        var sbCol = sp.Bounds.X + sp.Bounds.Width - 1;
        var topThumb = await WaitForThumbAsync(auto, terminal, sp);

        _state!.IlTreeScrollOffset = TreeMaxOffset(sp);
        _state!.App.Invalidate();
        await auto.WaitUntilAsync(s =>
        {
            var y = FirstThumbY(s, sbCol, sp.Bounds.Y, sp.Bounds.Y + sp.Bounds.Height);
            return y > topThumb;
        }, description: "thumb moved further down after Offset = MaxOffset");

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>
    /// After a thumb drag, the next DownArrow advances selection — proving the
    /// ScrollPanelNode kept keyboard focus through the drag (no extra FocusWhere needed
    /// because the panel is the only focusable for the tree).
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab3_AfterScrollbarDrag_DownArrow_AdvancesSelection()
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.RichLibraryDll, enableInputCoalescing: true);
        var runTask = RunAppAsync(app, ct);
        await SetupIlTabAsync(terminal, ExpandAllTypes, ct);

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        var sp = await WaitForPanelAsync(auto, app);
        await auto.WaitUntilAsync(_ => TreeScrollable(sp), description: "tree scrollable");

        var rows = Views.IlInspectorView.BuildTreeRows(_state!);
        await SetSelectionDirectAsync(auto, app, _state!, rows, 0);

        var sbCol = sp.Bounds.X + sp.Bounds.Width - 1;
        var thumbY = await WaitForThumbAsync(auto, terminal, sp);

        var beforeKey = _state!.IlFocusedTreeKey as string;
        await new Hex1bTerminalInputSequenceBuilder()
            .Drag(sbCol, thumbY, sbCol, thumbY + 2)
            .Key(Hex1bKey.DownArrow)
            .WaitUntil(_ => _state!.IlFocusedTreeKey as string != beforeKey, TimeSpan.FromSeconds(5))
            .Build()
            .ApplyAsync(terminal, ct);

        Assert.AreNotEqual(beforeKey, _state!.IlFocusedTreeKey as string);

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>
    /// No-op thumb click followed by DownArrow still advances selection — proves the
    /// panel-as-focusable design has no spare focusable that could absorb the key.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Tab3_NoOpScrollbarClick_DownArrow_AdvancesSelection()
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.RichLibraryDll);
        var runTask = RunAppAsync(app, ct);
        await SetupIlTabAsync(terminal, ExpandAllTypes, ct);

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        var sp = await WaitForPanelAsync(auto, app);
        await auto.WaitUntilAsync(_ => TreeScrollable(sp), description: "tree scrollable");

        var rows = Views.IlInspectorView.BuildTreeRows(_state!);
        await SetSelectionDirectAsync(auto, app, _state!, rows, 0);

        var sbCol = sp.Bounds.X + sp.Bounds.Width - 1;
        var thumbY = await WaitForThumbAsync(auto, terminal, sp);
        Assert.IsTrue(sp.Bounds.X <= sbCol && sbCol < sp.Bounds.X + sp.Bounds.Width, "thumb column must be inside panel bounds");

        var beforeKey = _state!.IlFocusedTreeKey as string;
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(sbCol, thumbY)
            .Build()
            .ApplyAsync(terminal, ct);
        await auto.KeyAsync(Hex1bKey.DownArrow, ct: ct);
        await auto.WaitUntilAsync(_ => _state!.IlFocusedTreeKey as string != beforeKey,
            description: "DownArrow advances selection after no-op click");

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>The IL Inspector tab exposes exactly one ScrollPanelNode in the focus ring.</summary>

    [TestMethod]

    [Timeout(30_000, CooperativeCancellation = true)]

    public async Task Tab3_TreeContainsExactlyOneScrollPanelNode()
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.RichLibraryDll);
        var runTask = RunAppAsync(app, ct);
        await SwitchToIlAsync(terminal, ct);

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        await WaitForPanelAsync(auto, app);

        // Snapshot the ring without racing the render thread's per-frame rebuild (an empty
        // snapshot means the rebuild is in flight — poll again), then assert the panel is unique.
        var count = 0;
        await auto.WaitUntilAsync(_ =>
        {
            var focusables = SnapshotFocusables(app);
            if (focusables.Length == 0) return false;
            count = focusables.OfType<ScrollPanelNode>().Count();
            return true;
        }, description: "focus ring observed without a concurrent rebuild");
        Assert.AreEqual(1, count);

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>
    /// Resolving the effective index against an empty row list returns -1, and
    /// every navigation handler in <see cref="IlTreeList"/> early-returns under
    /// that condition. Pure unit assertion — backs up the rendered theory variants
    /// below by pinning the behavior at the helper level.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void Tab3_NoMatchSearch_NavigationHelpers_NoOpOnZeroRows()
    {
        IReadOnlyList<IlTreeRow> empty = [];
        Assert.AreEqual(-1, IlTreeList.ResolveEffectiveIndex(empty, key: null));
        Assert.AreEqual(-1, IlTreeList.ResolveEffectiveIndex(empty, key: "method:0x06000001"));
        Assert.AreEqual(-1, IlTreeList.FindRowIndex(empty, "method:0x06000001"));
    }

    /// <summary>
    /// Theory data: every navigation key the panel binds. Pressing any of these on
    /// a zero-row tree must be a complete no-op — no exception, no key change, no
    /// offset change.
    /// </summary>
    public static IEnumerable<object[]> NoMatchKeyVariants()
    {
        yield return [Hex1bKey.UpArrow];
        yield return [Hex1bKey.DownArrow];
        yield return [Hex1bKey.Home];
        yield return [Hex1bKey.End];
        yield return [Hex1bKey.PageUp];
        yield return [Hex1bKey.PageDown];
        yield return [Hex1bKey.Enter];
        yield return [Hex1bKey.Spacebar];
        yield return [Hex1bKey.LeftArrow];
        yield return [Hex1bKey.RightArrow];
    }

    /// <summary>
    /// On a no-match search (zero rows), each navigation key is a no-op against the
    /// rendered panel — selection key and offset stay where they were. We wait for
    /// <see cref="ScrollPanelNode.ContentSize"/> to reach 0 before pressing keys so
    /// the panel's binding closure is guaranteed to hold the empty rows.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DynamicData(nameof(NoMatchKeyVariants))]
    public async Task Tab3_NoMatchSearch_NavigationKey_IsNoOp(Hex1bKey key)
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.RichLibraryDll);
        var runTask = RunAppAsync(app, ct);
        await SwitchToIlAsync(terminal, ct);

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        var sp = await WaitForPanelAsync(auto, app);

        // Establish a baseline selection so we can detect drift.
        var rows0 = Views.IlInspectorView.BuildTreeRows(_state!);
        await SetSelectionDirectAsync(auto, app, _state!, rows0, 0);

        // Apply a confirmed no-match search; wait for the panel's layout to actually
        // drop to zero rows before sending input. ContentSize == 0 proves the binding
        // closure on the panel now closes over the empty list.
        var search = _state!.Search[Dotsider.TabId.IlInspector];
        search.ActivateOrCycle();
        search.UpdateQuery("zzzz_no_match_zzzz");
        search.Confirm();
        _state.App.Invalidate();
        // Wait for both the BuildTreeRows-level and the panel-layout-level signals
        // that the empty-row state has propagated. ContentSize dropping below the
        // original count proves a render with the post-filter VStack reached the panel
        // and replaced the prior frame's binding closure with one capturing zero rows.
        await auto.WaitUntilAsync(_ => Views.IlInspectorView.BuildTreeRows(_state).Count == 0,
            description: "search filters BuildTreeRows to zero rows");
        // Search bar shows the confirmed query as static text — its appearance on
        // screen is the deterministic signal that the IL view re-rendered with the
        // search active and the panel's binding closure now closes over zero rows.
        // Re-nudge per poll: Hex1b drains an Invalidate that races an in-flight
        // frame, so a single test-thread Invalidate can be dropped without a render.
        await auto.WaitUntilAsync(s =>
        {
            if (s.ContainsText("zzzz_no_match_zzzz")) return true;
            _state.App.Invalidate();
            return false;
        }, description: "confirmed search query renders");

        var beforeKey = _state.IlFocusedTreeKey as string;
        var beforeOffset = TreeOffset;

        await auto.KeyAsync(key, ct: ct);
        await Task.Delay(50, ct);

        Assert.AreEqual(beforeKey, _state.IlFocusedTreeKey as string);
        Assert.AreEqual(beforeOffset, TreeOffset);

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>
    /// Mouse wheel on a zero-row tree is a no-op for both offset and selection.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    [DataRow(true)]
    [DataRow(false)]
    public async Task Tab3_NoMatchSearch_MouseWheel_IsNoOp(bool wheelDown)
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.RichLibraryDll);
        var runTask = RunAppAsync(app, ct);
        await SwitchToIlAsync(terminal, ct);

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        var sp = await WaitForPanelAsync(auto, app);

        var rows0 = Views.IlInspectorView.BuildTreeRows(_state!);
        await SetSelectionDirectAsync(auto, app, _state!, rows0, 0);

        var search = _state!.Search[Dotsider.TabId.IlInspector];
        search.ActivateOrCycle();
        search.UpdateQuery("zzzz_no_match_zzzz");
        search.Confirm();
        _state.App.Invalidate();
        await auto.WaitUntilAsync(_ => Views.IlInspectorView.BuildTreeRows(_state).Count == 0,
            description: "search filters BuildTreeRows to zero rows");
        // Search bar shows the confirmed query as static text — its appearance on
        // screen is the deterministic signal that the IL view re-rendered with the
        // search active and the panel's binding closure now closes over zero rows.
        // Re-nudge per poll: Hex1b drains an Invalidate that races an in-flight
        // frame, so a single test-thread Invalidate can be dropped without a render.
        await auto.WaitUntilAsync(s =>
        {
            if (s.ContainsText("zzzz_no_match_zzzz")) return true;
            _state.App.Invalidate();
            return false;
        }, description: "confirmed search query renders");

        var beforeKey = _state.IlFocusedTreeKey as string;
        var beforeOffset = TreeOffset;

        var bodyX = sp.Bounds.X + 5;
        var bodyY = sp.Bounds.Y + 1;
        var seq = new Hex1bTerminalInputSequenceBuilder().MouseMoveTo(bodyX, bodyY);
        seq = wheelDown ? seq.ScrollDown() : seq.ScrollUp();
        await seq.Build().ApplyAsync(terminal, ct);
        await Task.Delay(50, ct);

        Assert.AreEqual(beforeKey, _state.IlFocusedTreeKey as string);
        Assert.AreEqual(beforeOffset, TreeOffset);

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>When the row count drops below the previous offset, the panel clamps Offset to MaxOffset.</summary>

    [TestMethod]

    [Timeout(30_000, CooperativeCancellation = true)]

    public async Task Tab3_RowCountShrinks_ClampsScrollOffset()
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.RichLibraryDll);
        var runTask = RunAppAsync(app, ct);
        await SetupIlTabAsync(terminal, ExpandAllTypes, ct);

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        var sp = await WaitForPanelAsync(auto, app);
        await auto.WaitUntilAsync(_ => TreeScrollable(sp), description: "tree scrollable");

        await auto.KeyAsync(Hex1bKey.End, ct: ct);
        await auto.WaitUntilAsync(_ => TreeOffset == TreeMaxOffset(sp), description: "scrolled to end");

        // Collapse all types — content height shrinks dramatically.
        foreach (var t in _state!.Analyzer.TypeDefs)
            _state!.IlTreeExpansionState[$"type:{t.FullName}"] = false;
        _state!.App.Invalidate();

        await auto.WaitUntilAsync(_ => TreeOffset <= TreeMaxOffset(sp),
            description: "Offset clamped after collapse");
        Assert.IsLessThanOrEqualTo(TreeMaxOffset(sp), TreeOffset, $"Offset={TreeOffset} MaxOffset={TreeMaxOffset(sp)}");

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>A coalesced DownArrow + LeftArrow batch operates on the post-Down selection, not the build-time snapshot.</summary>

    [TestMethod]

    [Timeout(30_000, CooperativeCancellation = true)]

    public async Task Tab3_DownArrow_LeftArrow_Coalesced_OperatesOnLiveSelection()
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.RichLibraryDll, enableInputCoalescing: true);
        var runTask = RunAppAsync(app, ct);
        await SwitchToIlAsync(terminal, ct);

        // Expand TWO namespaces so DownArrow walks from one to the next, both initially expanded.
        foreach (var ns in _state!.Analyzer.TypeDefs
            .Select(t => string.IsNullOrEmpty(t.Namespace) ? "(global)" : t.Namespace).Distinct())
            _state!.IlTreeExpansionState[$"ns:{ns}"] = true;

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        await WaitForPanelAsync(auto, app);

        var rows = Views.IlInspectorView.BuildTreeRows(_state!);
        var firstNs = rows.FirstOrDefault(r => r.Kind == IlTreeRowKind.Namespace);
        var secondNs = firstNs is null
            ? null
            : rows.Skip(rows.IndexOf(firstNs) + 1)
                .FirstOrDefault(r => r.Kind == IlTreeRowKind.Namespace && r.Depth == 0);
        if (firstNs is null || secondNs is null)
        {
            // Sample assembly only has one namespace — fall back to a type row companion.
            _cts!.Cancel();
            await runTask;
            return;
        }

        _state!.IlFocusedTreeKey = firstNs.Key;
        _state!.App.Invalidate();
        await auto.WaitUntilAsync(_ => _state!.IlFocusedTreeKey as string == firstNs.Key,
            description: "first namespace selected");
        Assert.IsTrue(Views.IlInspectorView.GetExpansionState(_state, firstNs.ExpansionKey, defaultExpanded: true),
            "first namespace expanded");
        Assert.IsTrue(Views.IlInspectorView.GetExpansionState(_state, secondNs.ExpansionKey, defaultExpanded: true),
            "second namespace expanded");

        // Coalesced DownArrow + LeftArrow: Down moves to next row (first child of firstNs);
        // LeftArrow on a child row collapses parent OR walks to parent. Either way, the
        // operation must read post-Down state, never the build-time firstNs.
        await new Hex1bTerminalInputSequenceBuilder()
            .Key(Hex1bKey.DownArrow)
            .Key(Hex1bKey.LeftArrow)
            .Build()
            .ApplyAsync(terminal, ct);
        await Task.Delay(100, ct);

        // First namespace must still be expanded (not collapsed by a stale-state Left).
        Assert.IsTrue(Views.IlInspectorView.GetExpansionState(_state, firstNs.ExpansionKey, defaultExpanded: true),
            "LeftArrow must not collapse the build-time row when selection has moved");

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>A click on a row selects and activates that row.</summary>

    [TestMethod]

    [Timeout(30_000, CooperativeCancellation = true)]

    public async Task Tab3_Click_OnRow_SelectsAndActivates()
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.RichLibraryDll);
        var runTask = RunAppAsync(app, ct);
        await SetupIlTabAsync(terminal, ExpandAllTypes, ct);

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        var sp = await WaitForPanelAsync(auto, app);

        // Pick a method row index that is visible at the current Offset.
        var rows = Views.IlInspectorView.BuildTreeRows(_state!);
        var visibleMethod = -1;
        for (var i = TreeOffset; i < Math.Min(rows.Count, TreeOffset + sp.ViewportSize); i++)
        {
            if (rows[i].Kind == IlTreeRowKind.Method) { visibleMethod = i; break; }
        }
        if (visibleMethod < 0)
        {
            _cts!.Cancel(); await runTask; return;
        }

        var clickX = sp.Bounds.X + 5;
        var clickY = sp.Bounds.Y + (visibleMethod - TreeOffset);
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(clickX, clickY)
            .Build()
            .ApplyAsync(terminal, ct);
        await auto.WaitUntilAsync(_ => _state!.IlFocusedTreeKey as string == rows[visibleMethod].Key,
            description: "click selected the method row");
        Assert.IsNotNull(_state!.IlSelectedMethod);
        Assert.AreEqual(rows[visibleMethod].Method!.Token, _state.IlSelectedMethod.Token);

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>A click on the scrollbar gutter when scrollable does not change the row selection.</summary>

    [TestMethod]

    [Timeout(30_000, CooperativeCancellation = true)]

    public async Task Tab3_Click_OnScrollbarColumn_DoesNotSelectRow()
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.RichLibraryDll);
        var runTask = RunAppAsync(app, ct);
        await SetupIlTabAsync(terminal, ExpandAllTypes, ct);

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        var sp = await WaitForPanelAsync(auto, app);
        await auto.WaitUntilAsync(_ => TreeScrollable(sp), description: "tree scrollable");

        var rows = Views.IlInspectorView.BuildTreeRows(_state!);
        await SetSelectionDirectAsync(auto, app, _state!, rows, 0);

        var sbCol = sp.Bounds.X + sp.Bounds.Width - 1;
        var sbY = sp.Bounds.Y + sp.Bounds.Height - 2; // not the thumb — track click pages, not selects
        var thumbY = await WaitForThumbAsync(auto, terminal, sp);
        // Make sure the click target is NOT the thumb — pick a track location.
        if (sbY == thumbY) sbY = thumbY - 1;

        var beforeKey = _state!.IlFocusedTreeKey as string;
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(sbCol, sbY)
            .Build()
            .ApplyAsync(terminal, ct);
        await Task.Delay(80, ct);

        Assert.AreEqual(beforeKey, _state!.IlFocusedTreeKey as string);

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>ComplexApp.dll renders the scrollbar correctly when content exceeds the viewport.</summary>

    [TestMethod]

    [Timeout(30_000, CooperativeCancellation = true)]

    public async Task Tab3_ComplexAppDll_ScrollbarRenders()
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.ComplexAppDll);
        var runTask = RunAppAsync(app, ct);
        await SetupIlTabAsync(terminal, ExpandAllTypes, ct);

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        var sp = await WaitForPanelAsync(auto, app);

        // Allow a frame for layout, then check thumb only when actually scrollable.
        await Task.Delay(50, ct);
        if (TreeScrollable(sp))
        {
            var sbCol = sp.Bounds.X + sp.Bounds.Width - 1;
            await auto.WaitUntilAsync(s =>
                FirstThumbY(s, sbCol, sp.Bounds.Y, sp.Bounds.Y + sp.Bounds.Height) >= 0,
                description: "thumb paints for ComplexAppDll");
        }

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>NavigateToIlMethod targeting a deep row scrolls that row into view via the pending-scroll flag.</summary>

    [TestMethod]

    [Timeout(30_000, CooperativeCancellation = true)]

    public async Task Tab3_NavigateToIlMethod_DeepRow_ScrollsIntoView()
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.RichLibraryDll);
        var runTask = RunAppAsync(app, ct);

        // Stay on the General tab so the IL view does not render until the jump.
        await new Hex1bTerminalInputSequenceBuilder()
            .WaitUntil(s => s.ContainsText("Assembly Name"), TimeSpan.FromSeconds(10))
            .Build()
            .ApplyAsync(terminal, ct);

        // Pre-expand so all method rows are reachable by index.
        ExpandAllTypes(_state!);

        // Pick a method whose row index lies past the viewport.
        var rows = Views.IlInspectorView.BuildTreeRows(_state!);
        var deepIdx = -1;
        for (var i = rows.Count - 1; i >= 0; i--)
        {
            if (rows[i].Kind == IlTreeRowKind.Method) { deepIdx = i; break; }
        }
        Assert.IsGreaterThan(0, deepIdx, "test fixture must contain a method row");
        var deepMethod = rows[deepIdx].Method!;

        _state!.NavigateToIlMethod(deepMethod);

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        var sp = await WaitForPanelAsync(auto, app);
        await auto.WaitUntilAsync(_ =>
        {
            var freshRows = Views.IlInspectorView.BuildTreeRows(_state);
            var idx = IlTreeList.FindRowIndex(freshRows, $"method:{deepMethod.Token}");
            return idx >= 0
                && sp.ViewportSize > 0
                && !_state.IlScrollSelectionIntoViewPending
                && TreeOffset <= idx
                && idx < TreeOffset + sp.ViewportSize;
        }, description: "deep target visible after first-arrival pending scroll");

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>Wheel can push the selection offscreen; a subsequent repaint does not snap the viewport back to the selection.</summary>

    [TestMethod]

    [Timeout(30_000, CooperativeCancellation = true)]

    public async Task Tab3_MouseWheel_ScrollsSelectionOffscreen_RepaintDoesNotSnapBack()
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.RichLibraryDll);
        var runTask = RunAppAsync(app, ct);
        await SetupIlTabAsync(terminal, ExpandAllTypes, ct);

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        var sp = await WaitForPanelAsync(auto, app);
        await auto.WaitUntilAsync(_ => TreeScrollable(sp), description: "tree scrollable");

        var rows = Views.IlInspectorView.BuildTreeRows(_state!);
        await SetSelectionDirectAsync(auto, app, _state!, rows, 0);

        // Wheel down enough times to push row 0 offscreen. Each tick advances Offset
        // by 3 (hex1b's ScrollPanelNode default). Send all five ticks in a single
        // sequence and wait until every tick has been applied — only then is it safe
        // to capture the post-wheel offset, because in-flight events finishing during
        // a later Task.Delay would otherwise bump it further and break the
        // "did not snap back" equality below.
        var bodyX = sp.Bounds.X + 5;
        var bodyY = sp.Bounds.Y + 5;
        const int ticks = 5;
        const int expectedAdvance = 3 * ticks;
        await new Hex1bTerminalInputSequenceBuilder()
            .MouseMoveTo(bodyX, bodyY)
            .ScrollDown(ticks)
            .Build()
            .ApplyAsync(terminal, ct);
        await auto.WaitUntilAsync(_ => TreeOffset >= Math.Min(expectedAdvance, TreeMaxOffset(sp)),
            description: "all wheel ticks applied to Offset");
        var offsetAfterWheel = TreeOffset;

        // Force a repaint and wait one render cycle. The repaint must NOT trigger any
        // EnsureSelectionVisible-style snap-back; the wheel-only path leaves the
        // pending-scroll flag clear, so Offset stays where the wheel left it.
        _state!.App.Invalidate();
        await auto.WaitUntilAsync(_ => true, description: "render frame elapses");

        Assert.AreEqual(rows[0].Key, _state!.IlFocusedTreeKey as string);
        Assert.AreEqual(offsetAfterWheel, TreeOffset);

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>NavigateBack landing on the IL tab focuses the ScrollPanelNode (via RequestContentFocus).</summary>

    [TestMethod]

    [Timeout(30_000, CooperativeCancellation = true)]

    public async Task Tab3_NavigateBack_FromHexToIl_FocusesScrollPanelNode()
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.RichLibraryDll);
        var runTask = RunAppAsync(app, ct);
        await SwitchToIlAsync(terminal, ct);

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        await WaitForPanelAsync(auto, app);

        // Navigate to Hex (Tab 5) via the cross-view stack.
        _state!.CrossViewBackStack.Push((_state!.CurrentTab, _state!.PeSubTab));
        _state!.NavigateToTab(Dotsider.TabId.HexDump);
        _state!.App.RequestFocus(node => node is Hex1b.EditorNode);
        _state!.App.Invalidate();
        await auto.WaitUntilAsync(_ => _state!.App.FocusedNode is Hex1b.EditorNode,
            description: "Hex tab focuses editor");

        _state!.NavigateBack();
        await auto.WaitUntilAsync(_ => _state!.App.FocusedNode is ScrollPanelNode,
            description: "NavigateBack focuses ScrollPanelNode on IL");

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>On first arrival with no focused key, row 0 is the effective selection.</summary>

    [TestMethod]

    [Timeout(30_000, CooperativeCancellation = true)]

    public async Task Tab3_FirstArrival_NoFocusedKey_HighlightsRow0()
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.RichLibraryDll);
        var runTask = RunAppAsync(app, ct);
        await SwitchToIlAsync(terminal, ct);

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        var sp = await WaitForPanelAsync(auto, app);

        // Force the null-key path: clear the key, redraw.
        _state!.IlFocusedTreeKey = null;
        _state!.App.Invalidate();
        await Task.Delay(50, ct);

        // Effective index must fall back to row 0 — confirm by pressing DownArrow,
        // which from null should land on row 1, not row 0 → that proves the fallback.
        var rows = Views.IlInspectorView.BuildTreeRows(_state);
        await auto.KeyAsync(Hex1bKey.DownArrow, ct: ct);
        await auto.WaitUntilAsync(_ => _state!.IlFocusedTreeKey as string == rows[1].Key,
            description: "DownArrow from null key lands on row 1");

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>DownArrow from a null focused key lands on row 1 (effective 0 + 1).</summary>

    [TestMethod]

    [Timeout(30_000, CooperativeCancellation = true)]

    public async Task Tab3_DownArrow_FromNullKey_LandsOnRow1()
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.RichLibraryDll);
        var runTask = RunAppAsync(app, ct);
        await SwitchToIlAsync(terminal, ct);

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        await WaitForPanelAsync(auto, app);

        _state!.IlFocusedTreeKey = null;
        _state!.App.Invalidate();
        await Task.Delay(50, ct);

        var rows = Views.IlInspectorView.BuildTreeRows(_state);
        await auto.KeyAsync(Hex1bKey.DownArrow, ct: ct);
        await auto.WaitUntilAsync(_ => _state!.IlFocusedTreeKey as string == rows[1].Key,
            description: "fallback to 0 then +1 → row 1");

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>A SetIlFocusedTreeKey(null) clears the pending-scroll flag without leaking to subsequent renders.</summary>

    [TestMethod]

    [Timeout(30_000, CooperativeCancellation = true)]

    public async Task Tab3_PendingFlag_DoesNotLeakAfterSetIlFocusedTreeKeyNull()
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.RichLibraryDll);
        var runTask = RunAppAsync(app, ct);
        await SetupIlTabAsync(terminal, ExpandAllTypes, ct);

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        var sp = await WaitForPanelAsync(auto, app);
        await auto.WaitUntilAsync(_ => TreeScrollable(sp), description: "tree scrollable");

        // External set then null — flag must clear without scrolling anywhere weird.
        var rows = Views.IlInspectorView.BuildTreeRows(_state!);
        _state!.SetIlFocusedTreeKey(rows[^1].Key);
        await auto.WaitUntilAsync(_ => !_state.IlScrollSelectionIntoViewPending,
            description: "pending flag cleared after deep external set");
        _state!.SetIlFocusedTreeKey(null);
        await auto.WaitUntilAsync(_ => !_state.IlScrollSelectionIntoViewPending,
            description: "pending flag cleared after null set");

        // Now: direct assignment (no flag) + wheel to push offscreen, repaint must NOT snap back.
        await SetSelectionDirectAsync(auto, app, _state, rows, 5);

        var bodyX = sp.Bounds.X + 5;
        var bodyY = sp.Bounds.Y + 5;
        for (var i = 0; i < 5; i++)
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .MouseMoveTo(bodyX, bodyY)
                .ScrollDown()
                .Build()
                .ApplyAsync(terminal, ct);
        }
        var offsetAfterWheel = TreeOffset;
        Assert.IsGreaterThan(0, offsetAfterWheel, "wheel advanced offset");

        _state!.App.Invalidate();
        await Task.Delay(80, ct);
        Assert.AreEqual(offsetAfterWheel, TreeOffset);

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>When the panel is not scrollable, a click in the rightmost column selects the row beneath it.</summary>

    [TestMethod]

    [Timeout(30_000, CooperativeCancellation = true)]

    public async Task Tab3_Click_RightmostColumn_WhenNotScrollable_SelectsRow()
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.HelloWorldDll);
        var runTask = RunAppAsync(app, ct);
        await SwitchToIlAsync(terminal, ct);

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        var sp = await WaitForPanelAsync(auto, app);
        await auto.WaitUntilAsync(_ => !TreeScrollable(sp),
            description: "HelloWorld content fits viewport");

        // Pick a method row that is visible.
        var rows = Views.IlInspectorView.BuildTreeRows(_state!);
        var methodIdx = rows.FindIndex(r => r.Kind == IlTreeRowKind.Method);
        if (methodIdx < 0)
        {
            // Expand the first type — HelloWorld should have a Program type.
            var firstType = _state!.Analyzer.TypeDefs.First(t =>
                _state!.Analyzer.MethodDefs.Any(m => m.DeclaringType == t.FullName));
            _state!.IlTreeExpansionState[$"type:{firstType.FullName}"] = true;
            _state!.App.Invalidate();
            await auto.WaitUntilAsync(_ =>
                Views.IlInspectorView.BuildTreeRows(_state)
                    .Any(r => r.Kind == IlTreeRowKind.Method),
                description: "method row materializes");
            rows = Views.IlInspectorView.BuildTreeRows(_state);
            methodIdx = rows.FindIndex(r => r.Kind == IlTreeRowKind.Method);
            // Wait for the panel's closure to pick up the expanded rows.
            await auto.WaitUntilAsync(_ => sp.ContentSize == Math.Min(sp.ViewportSize, rows.Count),
                description: "panel ContentSize agrees with expanded rows");
        }
        Assert.IsGreaterThanOrEqualTo(0, methodIdx);

        var rightCol = sp.Bounds.X + sp.Bounds.Width - 1;
        var clickY = sp.Bounds.Y + (methodIdx - TreeOffset);
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(rightCol, clickY)
            .Build()
            .ApplyAsync(terminal, ct);
        await auto.WaitUntilAsync(_ => _state!.IlFocusedTreeKey as string == rows[methodIdx].Key,
            description: "rightmost-column click selects when not scrollable");

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>An external jump that expands the tree on an already-open IL tab scrolls the deep target into view after the layout grows.</summary>

    [TestMethod]

    [Timeout(30_000, CooperativeCancellation = true)]

    public async Task Tab3_AlreadyOpen_ExternalJumpExpandsTree_TargetRowScrollsIntoView()
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.RichLibraryDll);
        var runTask = RunAppAsync(app, ct);
        await SwitchToIlAsync(terminal, ct);

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        var sp = await WaitForPanelAsync(auto, app);

        // Find a deep method that requires expansion to materialize.
        var deepMethod = _state!.Analyzer.MethodDefs.OrderByDescending(m => m.Token).First();
        var typeDef = _state!.Analyzer.TypeDefs.First(t => t.FullName == deepMethod.DeclaringType);
        var ns = !string.IsNullOrEmpty(typeDef.Namespace) ? typeDef.Namespace : "(global)";

        // External jump via the same one-shot path: expand tree + SetIlFocusedTreeKey
        // in one operation. The pending-scroll consumer must wait until ContentSize
        // matches the freshly expanded rows before clamping.
        _state!.IlTreeExpansionState[$"ns:{ns}"] = true;
        _state!.IlTreeExpansionState[$"type:{typeDef.FullName}"] = true;
        _state!.SetIlFocusedTreeKey($"method:{deepMethod.Token}");
        _state!.App.Invalidate();

        await auto.WaitUntilAsync(_ =>
        {
            var rows = Views.IlInspectorView.BuildTreeRows(_state);
            var idx = IlTreeList.FindRowIndex(rows, $"method:{deepMethod.Token}");
            return idx >= 0
                && sp.ViewportSize > 0
                && !_state.IlScrollSelectionIntoViewPending
                && TreeOffset <= idx
                && idx < TreeOffset + sp.ViewportSize;
        }, description: "deep target visible after expand + pending scroll");

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>Home re-anchors the viewport when row 0 is selected but offscreen (boundary scroll-recovery).</summary>

    [TestMethod]

    [Timeout(30_000, CooperativeCancellation = true)]

    public async Task Tab3_Home_WhenSelectedRow0IsOffscreen_ScrollsSelectionIntoView()
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.RichLibraryDll);
        var runTask = RunAppAsync(app, ct);
        await SetupIlTabAsync(terminal, ExpandAllTypes, ct);

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        var sp = await WaitForPanelAsync(auto, app);
        await auto.WaitUntilAsync(_ => TreeScrollable(sp), description: "tree scrollable");

        var rows = Views.IlInspectorView.BuildTreeRows(_state!);
        await SetSelectionDirectAsync(auto, app, _state!, rows, 0);

        // Wheel-scroll row 0 offscreen, key stays on row 0.
        var bodyX = sp.Bounds.X + 5;
        var bodyY = sp.Bounds.Y + 5;
        for (var i = 0; i < 5; i++)
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .MouseMoveTo(bodyX, bodyY)
                .ScrollDown()
                .Build()
                .ApplyAsync(terminal, ct);
        }
        await auto.WaitUntilAsync(_ => TreeOffset > 0, description: "offset > 0");

        await auto.KeyAsync(Hex1bKey.Home, ct: ct);
        await auto.WaitUntilAsync(_ => TreeOffset == 0,
            description: "Home re-anchors viewport on row 0");

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>UpArrow at row 0 re-anchors the viewport when row 0 is offscreen, even though the selection index does not move.</summary>

    [TestMethod]

    [Timeout(30_000, CooperativeCancellation = true)]

    public async Task Tab3_UpArrow_AtFirstRow_WhenOffscreen_ScrollsSelectionIntoView()
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.RichLibraryDll);
        var runTask = RunAppAsync(app, ct);
        await SetupIlTabAsync(terminal, ExpandAllTypes, ct);

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        var sp = await WaitForPanelAsync(auto, app);
        await auto.WaitUntilAsync(_ => TreeScrollable(sp), description: "tree scrollable");

        var rows = Views.IlInspectorView.BuildTreeRows(_state!);
        await SetSelectionDirectAsync(auto, app, _state!, rows, 0);

        var bodyX = sp.Bounds.X + 5;
        var bodyY = sp.Bounds.Y + 5;
        for (var i = 0; i < 5; i++)
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .MouseMoveTo(bodyX, bodyY)
                .ScrollDown()
                .Build()
                .ApplyAsync(terminal, ct);
        }
        await auto.WaitUntilAsync(_ => TreeOffset > 0, description: "offset > 0");

        await auto.KeyAsync(Hex1bKey.UpArrow, ct: ct);
        await auto.WaitUntilAsync(_ => TreeOffset == 0,
            description: "UpArrow at row 0 re-anchors viewport");
        Assert.AreEqual(rows[0].Key, _state!.IlFocusedTreeKey as string);

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>End re-anchors the viewport when the last row is selected but offscreen.</summary>

    [TestMethod]

    [Timeout(30_000, CooperativeCancellation = true)]

    public async Task Tab3_End_WhenLastRowAlreadySelectedButOffscreen_ScrollsSelectionIntoView()
    {
        var (terminal, app, ct) = CreateMouseApp(Samples.RichLibraryDll);
        var runTask = RunAppAsync(app, ct);
        await SetupIlTabAsync(terminal, ExpandAllTypes, ct);

        var auto = new Hex1bTerminalAutomator(terminal, defaultTimeout: TimeSpan.FromSeconds(10));
        var sp = await WaitForPanelAsync(auto, app);
        await auto.WaitUntilAsync(_ => TreeScrollable(sp), description: "tree scrollable");

        var rows = Views.IlInspectorView.BuildTreeRows(_state!);
        await auto.KeyAsync(Hex1bKey.End, ct: ct);
        await auto.WaitUntilAsync(_ => _state!.IlFocusedTreeKey as string == rows[^1].Key,
            description: "last row selected");

        // Wheel up enough that the last row falls offscreen but key still points there.
        var bodyX = sp.Bounds.X + 5;
        var bodyY = sp.Bounds.Y + 5;
        for (var i = 0; i < 10; i++)
        {
            await new Hex1bTerminalInputSequenceBuilder()
                .MouseMoveTo(bodyX, bodyY)
                .ScrollUp()
                .Build()
                .ApplyAsync(terminal, ct);
        }
        await auto.WaitUntilAsync(_ => TreeOffset < TreeMaxOffset(sp),
            description: "wheel pushed last row offscreen");

        await auto.KeyAsync(Hex1bKey.End, ct: ct);
        await auto.WaitUntilAsync(_ => TreeOffset == TreeMaxOffset(sp),
            description: "End re-anchors viewport on last row");

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>Disposes the per-test app, terminal, state, and cancellation token.</summary>

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
