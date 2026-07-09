using Dotsider.Views;
using Hex1b;
using Hex1b.Automation;
using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Behavior tests for the IL tree's windowed virtualization (issue #188): with far more
/// rows than Hex1b's 10,000-row render-surface clamp, the tree must still render all the
/// way to the bottom, keep the scrollbar accurate, and preserve the #167 scroll model —
/// wheel and gutter scrolling move the viewport without touching the selection, keyboard
/// navigation is non-wrapping and pulls the selection into view. Drives
/// <see cref="IlTreeList.Build"/> and <see cref="IlInspectorView.SyncTreeScroll"/> (the
/// real capture/pending logic) over a synthetic 12,000-row tree.
/// </summary>
[TestClass]
public sealed class IlTreeVirtualizationTests : IDisposable
{
    private static SampleAssemblyFixture Samples => SampleAssemblyHost.Instance;

    private const int RowCount = 12_000;

    private Hex1bApp? _app;
    private Hex1bTerminal? _terminal;
    private Hex1bAppWorkloadAdapter? _workload;
    private DotsiderState? _state;
    private CancellationTokenSource? _cts;
    private List<IlTreeRow>? _rows;

    /// <summary>
    /// Rows occupied by the harness header above the tree. Mutable mid-test so the
    /// viewport-change test can grow the tree pane without changing the widget shape
    /// (a shape change would reconcile a new panel node).
    /// </summary>
    private int _headerRows;

    /// <summary>
    /// Starts a bare app whose only content is the tree list over 12,000 synthetic rows,
    /// mirroring the root build's generation bookkeeping and the IL view's per-render
    /// scroll sync via <see cref="IlInspectorView.SyncTreeScroll"/> so the logic under
    /// test is the real one.
    /// </summary>
    private (Hex1bTerminalAutomator auto, Task runTask, CancellationToken ct) StartTreeApp()
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None);
        _workload = new Hex1bAppWorkloadAdapter();
        _terminal = Hex1bTerminal.CreateBuilder()
            .WithWorkload(_workload)
            .WithHeadless()
            .WithDimensions(80, 30)
            .Build();

        _rows = [.. Enumerable.Range(0, RowCount)
            .Select(i => new IlTreeRow($"row:{i}", 0, IlTreeRowKind.Method, $"row-{i}", null, false, false, ""))];
        var formatted = _rows.Select(r => r.Label).ToList();

        _app = new Hex1bApp(_ =>
        {
            _state ??= new DotsiderState(_app!, Samples.HelloWorldDll) { CurrentTab = TabId.IlInspector };
            // Mirror DotsiderApp.Build: only the root build advances the generation.
            unchecked { _state.BuildGeneration++; }
            _state.ExtraFrameArmed = false;
            IlInspectorView.SyncTreeScroll(_state, _rows!);
            var tree = IlTreeList.Build(
                _rows!, formatted, _state,
                selectionChanged: i => _state!.IlFocusedTreeKey = _rows![i].Key,
                itemActivated: null, expandRow: null, collapseRow: null);
            return Task.FromResult<Hex1bWidget>(new VStackWidget(
            [
                new TextBlockWidget(string.Empty).FixedHeight(_headerRows),
                tree,
            ]).Fill());
        }, new Hex1bAppOptions { WorkloadAdapter = _workload, EnableMouse = true, EnableInputCoalescing = false });

        var runTask = _app.RunAsync(_cts.Token);
        return (new Hex1bTerminalAutomator(_terminal, defaultTimeout: TimeSpan.FromSeconds(10)), runTask, _cts.Token);
    }

    /// <summary>
    /// Waits for the panel to be captured, arranged, and focused so keyboard and offset
    /// math have a live viewport to work against.
    /// </summary>
    private async Task<ScrollPanelNode> WaitForPanelAsync(Hex1bTerminalAutomator auto)
    {
        await auto.WaitUntilAsync(s => s.InAlternateScreen, description: "alternate screen");
        ScrollPanelNode? sp = null;
        await auto.WaitUntilAsync(_ =>
            (sp = _state?.IlScrollPanelNode) is { ViewportSize: > 0 },
            description: "panel captured and arranged");
        _app!.RequestFocus(node => node is ScrollPanelNode);
        await auto.WaitUntilAsync(_ => _app.FocusedNode is ScrollPanelNode,
            description: "panel focused");
        return sp!;
    }

    private void InvalidateAfterDirectStateMutation()
    {
        _state!.App.Invalidate();
        _state.RequestExtraFrame();
    }

    /// <summary>The gutter column: the panel's rightmost column when the tree overflows.</summary>
    private static int GutterCol(ScrollPanelNode sp) => sp.Bounds.X + sp.Bounds.Width - 1;

    /// <summary>Finds the first thumb cell in the gutter column, or -1.</summary>
    private static int FirstThumbY(Hex1bTerminalSnapshot snapshot, ScrollPanelNode sp)
    {
        var col = GutterCol(sp);
        for (var y = sp.Bounds.Y; y < sp.Bounds.Y + sp.Bounds.Height; y++)
        {
            if (snapshot.GetCell(col, y).Character == "▉")
                return y;
        }
        return -1;
    }

    /// <summary>
    /// Past the 10k render-surface clamp, scrolling to the very bottom still paints the
    /// last rows — the full viewport, no blank gap. Pins the issue #188 regression.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Rows12k_ScrolledToBottom_RendersLastRows_NoGap()
    {
        var (auto, runTask, _) = StartTreeApp();
        var sp = await WaitForPanelAsync(auto);

        _state!.IlTreeScrollOffset = int.MaxValue; // clamps to MaxOffset in the next build
        InvalidateAfterDirectStateMutation();

        var expectedTop = RowCount - sp.ViewportSize;
        await auto.WaitUntilAsync(_ => _state!.IlTreeScrollOffset == expectedTop,
            description: "scroll offset clamps to bottom");
        await auto.WaitUntilTextAsync("row-11999");
        Assert.AreEqual(expectedTop, _state.IlTreeScrollOffset);
        // The viewport is full from its first row — a blank gap would drop this label.
        await auto.WaitUntilTextAsync($"row-{expectedTop}");

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>
    /// The gutter thumb sits at the top at offset zero and moves to the gutter's end at
    /// MaxOffset — the scrollbar math spans the full 12,000 rows.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Rows12k_GutterThumb_TracksOffset_EndToEnd()
    {
        var (auto, runTask, _) = StartTreeApp();
        var sp = await WaitForPanelAsync(auto);

        var topThumb = -1;
        await auto.WaitUntilAsync(s => (topThumb = FirstThumbY(s, sp)) >= 0,
            description: "thumb painted at offset 0");
        Assert.AreEqual(sp.Bounds.Y, topThumb);

        _state!.IlTreeScrollOffset = RowCount; // clamps to MaxOffset
        InvalidateAfterDirectStateMutation();
        await auto.WaitUntilAsync(s =>
        {
            var y = FirstThumbY(s, sp);
            return y > topThumb && y == sp.Bounds.Y + sp.Bounds.Height - 1;
        }, description: "thumb reaches the gutter end at MaxOffset");

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>
    /// Wheel over the tree body advances the offset by 3 without changing the selection,
    /// even when the selected row scrolls offscreen — the #167 decoupling at 12k rows.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Rows12k_Wheel_MovesViewport_NotSelection()
    {
        var (auto, runTask, ct) = StartTreeApp();
        var sp = await WaitForPanelAsync(auto);

        _state!.IlFocusedTreeKey = "row:0";
        InvalidateAfterDirectStateMutation();

        await new Hex1bTerminalInputSequenceBuilder()
            .MouseMoveTo(sp.Bounds.X + 5, sp.Bounds.Y + 5)
            .ScrollDown()
            .Build()
            .ApplyAsync(_terminal!, ct);
        await auto.WaitUntilAsync(_ => _state.IlTreeScrollOffset == 3,
            description: "wheel advanced offset by exactly 3");

        Assert.AreEqual("row:0", _state.IlFocusedTreeKey as string);

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>
    /// End selects the last of 12,000 rows, scrolls it into view, and renders it; a
    /// further DownArrow does not wrap.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Rows12k_End_SelectsLastRow_ScrollsIntoView_NoWrap()
    {
        var (auto, runTask, ct) = StartTreeApp();
        var sp = await WaitForPanelAsync(auto);

        await auto.KeyAsync(Hex1bKey.End, ct: ct);
        await auto.WaitUntilAsync(_ => _state!.IlFocusedTreeKey as string == "row:11999",
            description: "End selects the last row");
        await auto.WaitUntilTextAsync("row-11999");
        Assert.AreEqual(RowCount - sp.ViewportSize, _state!.IlTreeScrollOffset);

        await auto.KeyAsync(Hex1bKey.DownArrow, ct: ct);
        await Task.Delay(50, ct);
        Assert.AreEqual("row:11999", _state.IlFocusedTreeKey as string);

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>
    /// A click selects the row's absolute index — viewport row plus the scroll offset —
    /// under a deep offset where windowing bugs would select the wrong row.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Rows12k_Click_SelectsAbsoluteRow_UnderDeepOffset()
    {
        var (auto, runTask, ct) = StartTreeApp();
        var sp = await WaitForPanelAsync(auto);

        _state!.IlTreeScrollOffset = 11_500;
        InvalidateAfterDirectStateMutation();
        await auto.WaitUntilTextAsync("row-11500");

        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(sp.Bounds.X + 3, sp.Bounds.Y + 5)
            .Build()
            .ApplyAsync(_terminal!, ct);
        await auto.WaitUntilAsync(_ => _state.IlFocusedTreeKey as string == "row:11505",
            description: "click selects offset + viewport row");

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>
    /// An external jump to a deep key (the pending-scroll path used by cross-view
    /// navigation and search) lands the row inside the rendered viewport.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Rows12k_PendingScroll_DeepKey_LandsInViewport()
    {
        var (auto, runTask, _) = StartTreeApp();
        var sp = await WaitForPanelAsync(auto);

        _state!.SetIlFocusedTreeKey("row:11500");
        await auto.WaitUntilAsync(_ => !_state.IlScrollSelectionIntoViewPending,
            description: "pending scroll consumed");
        await auto.WaitUntilTextAsync("row-11500");
        Assert.IsInRange(_state.IlTreeScrollOffset, _state.IlTreeScrollOffset + sp.ViewportSize - 1, 11_500);

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>
    /// A track click below the thumb pages the viewport by one page (viewport − 1) and
    /// leaves the selection alone — the hand-rolled gutter matches the old panel gutter.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Rows12k_GutterTrackClick_PagesViewport_WithoutChangingSelection()
    {
        var (auto, runTask, ct) = StartTreeApp();
        var sp = await WaitForPanelAsync(auto);

        var beforeKey = _state!.IlFocusedTreeKey as string;
        var expected = Math.Max(1, sp.ViewportSize - 1);
        await new Hex1bTerminalInputSequenceBuilder()
            .ClickAt(GutterCol(sp), sp.Bounds.Y + sp.Bounds.Height - 2)
            .Build()
            .ApplyAsync(_terminal!, ct);
        await auto.WaitUntilAsync(_ => _state.IlTreeScrollOffset == expected,
            description: "track click pages the viewport");

        Assert.AreEqual(beforeKey, _state.IlFocusedTreeKey as string);

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>
    /// Dragging the thumb scrolls proportionally across all 12,000 rows without changing
    /// the selection, and the panel keeps keyboard focus through the drag.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Rows12k_GutterThumbDrag_UpdatesOffset_KeepsSelectionAndFocus()
    {
        var (auto, runTask, ct) = StartTreeApp();
        var sp = await WaitForPanelAsync(auto);

        var thumbY = -1;
        await auto.WaitUntilAsync(s => (thumbY = FirstThumbY(s, sp)) >= 0,
            description: "thumb painted");

        var beforeKey = _state!.IlFocusedTreeKey as string;
        await new Hex1bTerminalInputSequenceBuilder()
            .Drag(GutterCol(sp), thumbY, GutterCol(sp), thumbY + 3)
            .Build()
            .ApplyAsync(_terminal!, ct);
        await auto.WaitUntilAsync(_ => _state.IlTreeScrollOffset > 0,
            description: "thumb drag advanced the offset");

        Assert.AreEqual(beforeKey, _state.IlFocusedTreeKey as string);

        // The panel must still own the keyboard: DownArrow moves the selection.
        await auto.KeyAsync(Hex1bKey.DownArrow, ct: ct);
        await auto.WaitUntilAsync(_ => _state.IlFocusedTreeKey as string != beforeKey,
            description: "DownArrow advances selection after gutter drag");

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>
    /// When a render changes the pane height (search bar toggle, terminal resize), the
    /// window was built against the old height and nothing else schedules a rebuild —
    /// the viewport verifier must re-clamp the offset and refill the viewport without
    /// any further input.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public async Task Rows12k_ViewportGrows_WindowRefills_WithoutInput()
    {
        _headerRows = 1;
        var (auto, runTask, _) = StartTreeApp();
        var sp = await WaitForPanelAsync(auto);
        var oldViewport = sp.ViewportSize;

        _state!.IlTreeScrollOffset = int.MaxValue; // clamps to MaxOffset for the old height
        InvalidateAfterDirectStateMutation();
        await auto.WaitUntilTextAsync("row-11999");
        Assert.AreEqual(RowCount - oldViewport, _state.IlTreeScrollOffset);

        // Grow the pane by removing the header; no input follows.
        _headerRows = 0;
        InvalidateAfterDirectStateMutation();

        await auto.WaitUntilAsync(_ =>
            sp.ViewportSize == oldViewport + 1
            && _state.IlTreeScrollOffset == RowCount - sp.ViewportSize,
            description: "offset re-clamps to the grown viewport");
        // The viewport is full from its new first row — a stale window would leave the
        // last pane line blank.
        await auto.WaitUntilTextAsync($"row-{RowCount - sp.ViewportSize}");
        await auto.WaitUntilTextAsync("row-11999");

        _cts!.Cancel();
        await runTask;
    }

    /// <summary>
    /// Only the root build may advance the nudger generation. A mid-frame advance from
    /// the tree sync would make a nudger armed concurrently from a socket thread
    /// believe a later build already ran and exit without nudging.
    /// </summary>
    [TestMethod]
    [Timeout(30_000, CooperativeCancellation = true)]
    public void SyncTreeScroll_DoesNotAdvanceBuildGeneration()
    {
        _workload = new Hex1bAppWorkloadAdapter();
        _terminal = Hex1bTerminal.CreateBuilder().WithWorkload(_workload).WithHeadless().WithDimensions(80, 24).Build();
        _app = new Hex1bApp(_ => Task.FromResult<Hex1bWidget>(new TextBlockWidget("t")),
            new Hex1bAppOptions { WorkloadAdapter = _workload });
        _state = new DotsiderState(_app, Samples.HelloWorldDll)
        {
            BuildGeneration = 41,
            ExtraFrameArmed = true
        };

        IlInspectorView.SyncTreeScroll(_state, []);

        Assert.AreEqual(41, _state.BuildGeneration);
        Assert.IsTrue(_state.ExtraFrameArmed);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cts?.Cancel();
        _app?.Dispose();
        _terminal?.Dispose();
        _state?.Dispose();
        _cts?.Dispose();
    }
}
