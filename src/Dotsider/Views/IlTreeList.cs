using Hex1b;
using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Builds the IL Inspector's namespace/type/method tree as a windowed row list: a
/// <see cref="ScrollPanelWidget"/> hosting only the visible rows plus the
/// <see cref="IlTreeScrollbar"/> gutter column. The panel never scrolls itself — Hex1b
/// clamps a scroll child's render surface to 10,000 rows, which blanks fully expanded
/// native trees — so the scroll offset lives in <see cref="DotsiderState.IlTreeScrollOffset"/>
/// and each render materializes rows <c>[offset, offset + viewport)</c>. The panel is the
/// tree's only focusable and owns all input: keys, wheel, row clicks, and gutter presses
/// (thumb drag, track-click pagination). Scrolling stays independent of the selection, so
/// wheel scrolls can push the selection offscreen and stay there. Selection lives in
/// <see cref="DotsiderState.IlFocusedTreeKey"/>; keyboard handlers move the key and pull
/// the viewport along via <see cref="IlInspectorView.EnsureSelectionVisible(DotsiderState, ScrollPanelNode, int, int)"/>.
/// </summary>
internal static class IlTreeList
{
    /// <summary>
    /// Rows rendered on the first frame, before the panel has been captured and its
    /// viewport height is known. Generous enough to fill any plausible terminal; the
    /// bootstrap invalidate in <see cref="IlInspectorView"/> re-renders with the exact
    /// height one frame later.
    /// </summary>
    private const int FallbackViewportRows = 256;

    /// <summary>
    /// Builds the tree-list widget tree.
    /// </summary>
    /// <param name="rows">The flattened tree rows.</param>
    /// <param name="formattedRows">Pre-formatted display strings (one per row).</param>
    /// <param name="state">The shared application state. Read live by every binding so
    /// coalesced input batches operate on post-mutation selection.</param>
    /// <param name="selectionChanged">Invoked by keyboard navigation and row clicks
    /// after the new index is computed. The caller is expected to assign
    /// <c>state.IlFocusedTreeKey</c> directly (no <see cref="DotsiderState.SetIlFocusedTreeKey"/>),
    /// because the keyboard path manages scroll-into-view itself and must not arm
    /// <see cref="DotsiderState.IlScrollSelectionIntoViewPending"/>.</param>
    /// <param name="itemActivated">Invoked on Enter/Space/click for the currently
    /// selected row.</param>
    /// <param name="expandRow">Invoked when RightArrow targets an expandable
    /// collapsed row.</param>
    /// <param name="collapseRow">Invoked when LeftArrow targets an expandable
    /// expanded row.</param>
    /// <returns>The composed <see cref="ScrollPanelWidget"/>-rooted tree widget.</returns>
    internal static Hex1bWidget Build(
        IReadOnlyList<IlTreeRow> rows,
        IReadOnlyList<string> formattedRows,
        DotsiderState state,
        Action<int>? selectionChanged,
        Action<int>? itemActivated,
        Action<int>? expandRow,
        Action<int>? collapseRow)
    {
        var selectedIndex = ResolveEffectiveIndex(rows, state.IlFocusedTreeKey);

        // Viewport height comes from the panel arranged last frame; frame 1 renders a
        // generous window from the current offset until the panel is captured.
        var hasViewport = state.IlScrollPanelNode is { ViewportSize: > 0 };
        var viewportH = hasViewport ? state.IlScrollPanelNode!.ViewportSize : FallbackViewportRows;

        // Clamp the state-owned offset against the current rows (collapse shrinks them).
        var maxOffset = Math.Max(0, rows.Count - viewportH);
        state.IlTreeScrollOffset = Math.Clamp(state.IlTreeScrollOffset, 0, maxOffset);
        var offset = state.IlTreeScrollOffset;

        // Materialize only the visible window. Rows outside it never become widgets, so
        // the scroll child stays viewport-sized regardless of the total row count.
        var windowCount = Math.Max(0, Math.Min(viewportH, rows.Count - offset));
        var rowWidgets = new Hex1bWidget[windowCount];
        for (var i = 0; i < windowCount; i++)
        {
            var rowIndex = offset + i;
            Hex1bWidget rowWidget = new TextBlockWidget(formattedRows[rowIndex]);
            if (rowIndex == selectedIndex)
            {
                // Pull selection colors from the active theme's ListTheme so the IL
                // tree's selected row matches whatever the rest of the app's lists
                // paint under the same theme (default, alternate, high-contrast).
                rowWidget = new ThemePanelWidget(
                    theme => theme
                        .Set(GlobalTheme.ForegroundColor, theme.Get(ListTheme.SelectedForegroundColor))
                        .Set(GlobalTheme.BackgroundColor, theme.Get(ListTheme.SelectedBackgroundColor)),
                    rowWidget).FillWidth();
            }
            rowWidgets[i] = rowWidget;
        }

        var stack = new VStackWidget(rowWidgets).FillWidth();

        // The gutter is rendered inside the panel's child so the panel's bounds cover it:
        // Hex1b routes mouse presses to the focusable hit node, and the panel must be the
        // tree's only focusable — presses on the gutter column reach the panel's drag
        // binding below. When the content fits (or the viewport is not yet known), the
        // gutter slot collapses to zero width so rows span the full pane.
        var treeScrollable = hasViewport && rows.Count > viewportH;
        var gutter = treeScrollable
            ? IlTreeScrollbar.Build(state, rows.Count, viewportH)
            : new TextBlockWidget(string.Empty).FixedWidth(0);
        var content = new HStackWidget([stack, gutter]).FillWidth();

        var panel = new ScrollPanelWidget(content, ScrollOrientation.Vertical, showScrollbar: false)
            .InputBindings(bindings =>
            {
                // The panel's own scrolling is inert (its child is viewport-sized), but
                // its defaults would still swallow the keys and the wheel — replace them
                // with selection-driven navigation and state-offset wheel scrolling.
                bindings.Remove(ScrollPanelWidget.ScrollUpAction);
                bindings.Remove(ScrollPanelWidget.ScrollDownAction);
                bindings.Remove(ScrollPanelWidget.ScrollLeftAction);
                bindings.Remove(ScrollPanelWidget.ScrollRightAction);
                bindings.Remove(ScrollPanelWidget.PageUpAction);
                bindings.Remove(ScrollPanelWidget.PageDownAction);
                bindings.Remove(ScrollPanelWidget.ScrollToStartAction);
                bindings.Remove(ScrollPanelWidget.ScrollToEndAction);
                bindings.Remove(ScrollPanelWidget.MouseScrollUpAction);
                bindings.Remove(ScrollPanelWidget.MouseScrollDownAction);

                bindings.Key(Hex1bKey.UpArrow).Action(e =>
                    MoveSelection(e, rows, state, selectionChanged, -1), "Move up");

                bindings.Key(Hex1bKey.DownArrow).Action(e =>
                    MoveSelection(e, rows, state, selectionChanged, +1), "Move down");

                bindings.Key(Hex1bKey.Home).Action(e =>
                    SetSelection(e, rows, state, selectionChanged, 0), "Top");

                bindings.Key(Hex1bKey.End).Action(e =>
                    SetSelection(e, rows, state, selectionChanged, rows.Count - 1), "Bottom");

                bindings.Key(Hex1bKey.PageUp).Action(e =>
                {
                    if (e.FocusedNode is not ScrollPanelNode sp) return;
                    var step = Math.Max(1, sp.ViewportSize - 1);
                    MoveSelection(e, rows, state, selectionChanged, -step);
                }, "Page up");

                bindings.Key(Hex1bKey.PageDown).Action(e =>
                {
                    if (e.FocusedNode is not ScrollPanelNode sp) return;
                    var step = Math.Max(1, sp.ViewportSize - 1);
                    MoveSelection(e, rows, state, selectionChanged, +step);
                }, "Page down");

                bindings.Key(Hex1bKey.Enter).Action(_ =>
                    ActivateCurrent(rows, state, itemActivated), "Activate");

                bindings.Key(Hex1bKey.Spacebar).Action(_ =>
                    ActivateCurrent(rows, state, itemActivated), "Activate");

                bindings.Key(Hex1bKey.LeftArrow).Action(e =>
                    HandleLeft(e, rows, state, selectionChanged, collapseRow), "Collapse / parent");

                bindings.Key(Hex1bKey.RightArrow).Action(e =>
                    HandleRight(e, rows, state, selectionChanged, expandRow), "Expand / child");

                // Wheel scrolls the viewport only — the selection stays put, even
                // offscreen, mirroring the old panel's decoupled wheel behavior.
                bindings.Mouse(MouseButton.ScrollUp).Action(_ =>
                    ScrollViewport(state, rows.Count, -3), "Scroll up");

                bindings.Mouse(MouseButton.ScrollDown).Action(_ =>
                    ScrollViewport(state, rows.Count, +3), "Scroll down");

                // Gutter presses: thumb drag or track-click paging. Row-area presses
                // return an empty handler, which Hex1b treats as a rejected drag and
                // falls through to the Left-click row selection below.
                bindings.Drag(MouseButton.Left).Action((localX, localY) =>
                {
                    var sp = state.IlScrollPanelNode;
                    if (sp is null || rows.Count <= sp.ViewportSize) return new DragHandler();
                    if (localX < sp.Bounds.Width - 1) return new DragHandler();
                    return IlTreeScrollbar.HandleDrag(state, rows.Count, sp.ViewportSize, localY);
                }, "Drag scrollbar");

                bindings.Mouse(MouseButton.Left).Action(e =>
                {
                    if (e.FocusedNode is not ScrollPanelNode sp) return;
                    if (rows.Count == 0) return;

                    // The rightmost column is the scrollbar gutter only when the tree
                    // actually overflows; when content fits, that column is normal row
                    // area and a click there must still select the row.
                    var localX = e.MouseX - sp.Bounds.X;
                    if (localX < 0 || localX >= sp.Bounds.Width) return;
                    if (rows.Count > sp.ViewportSize && localX >= sp.Bounds.Width - 1) return;

                    var rowIndex = (e.MouseY - sp.Bounds.Y) + state.IlTreeScrollOffset;
                    if (rowIndex < 0 || rowIndex >= rows.Count) return;

                    selectionChanged?.Invoke(rowIndex);
                    itemActivated?.Invoke(rowIndex);
                }, "Select row");
            })
            .Fill();

        // Record the viewport this window was built against and verify it after the
        // frame: a render that changes the pane height (search bar toggle, terminal
        // resize) otherwise leaves a stale window with nothing scheduling a rebuild.
        state.IlTreeWindowViewport = viewportH;
        state.RequestIlTreeViewportCheck();

        return panel;
    }

    /// <summary>
    /// Resolves <paramref name="key"/> to a row index. Returns the matching index
    /// when the key resolves; returns <c>0</c> when the key is null or unresolved
    /// and at least one row exists; returns <c>-1</c> only on an empty list.
    /// </summary>
    /// <param name="rows">The flattened tree rows.</param>
    /// <param name="key">The current focused-tree key.</param>
    /// <returns>The effective selection index.</returns>
    internal static int ResolveEffectiveIndex(IReadOnlyList<IlTreeRow> rows, object? key)
    {
        if (rows.Count == 0) return -1;
        if (key is string s)
        {
            var idx = FindRowIndex(rows, s);
            if (idx >= 0) return idx;
        }
        return 0;
    }

    /// <summary>
    /// Returns the index of the row whose <see cref="IlTreeRow.Key"/> equals
    /// <paramref name="key"/>, or <c>-1</c> when no row matches.
    /// </summary>
    /// <param name="rows">The flattened tree rows.</param>
    /// <param name="key">The key to search for.</param>
    /// <returns>The matching index, or <c>-1</c>.</returns>
    internal static int FindRowIndex(IReadOnlyList<IlTreeRow> rows, string key)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].Key == key)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Moves <see cref="DotsiderState.IlTreeScrollOffset"/> by <paramref name="delta"/>,
    /// clamped to the scrollable range, without touching the selection.
    /// </summary>
    private static void ScrollViewport(DotsiderState state, int rowCount, int delta)
    {
        var viewportH = state.IlScrollPanelNode is { ViewportSize: > 0 } sp ? sp.ViewportSize : rowCount;
        var next = Math.Clamp(state.IlTreeScrollOffset + delta, 0, Math.Max(0, rowCount - viewportH));
        if (next == state.IlTreeScrollOffset) return;
        state.IlTreeScrollOffset = next;
        state.App.Invalidate();
    }

    private static void MoveSelection(
        InputBindingActionContext e,
        IReadOnlyList<IlTreeRow> rows,
        DotsiderState state,
        Action<int>? selectionChanged,
        int delta)
    {
        if (e.FocusedNode is not ScrollPanelNode sp) return;
        if (rows.Count == 0) return;

        var currentIdx = ResolveEffectiveIndex(rows, state.IlFocusedTreeKey);
        if (currentIdx < 0) return;
        var newIndex = Math.Clamp(currentIdx + delta, 0, rows.Count - 1);

        if (newIndex == currentIdx)
        {
            // Boundary: keyboard nav still re-anchors the viewport on the selection
            // even when the index does not move (Up at row 0 after wheel-down, etc.).
            IlInspectorView.EnsureSelectionVisible(state, sp, currentIdx, rows.Count);
            state.App.Invalidate();
            return;
        }

        selectionChanged?.Invoke(newIndex);
        IlInspectorView.EnsureSelectionVisible(state, sp, newIndex, rows.Count);
        state.App.Invalidate();
    }

    private static void SetSelection(
        InputBindingActionContext e,
        IReadOnlyList<IlTreeRow> rows,
        DotsiderState state,
        Action<int>? selectionChanged,
        int target)
    {
        if (e.FocusedNode is not ScrollPanelNode sp) return;
        if (rows.Count == 0) return;

        var currentIdx = ResolveEffectiveIndex(rows, state.IlFocusedTreeKey);
        var newIndex = Math.Clamp(target, 0, rows.Count - 1);

        if (newIndex == currentIdx)
        {
            IlInspectorView.EnsureSelectionVisible(state, sp, currentIdx, rows.Count);
            state.App.Invalidate();
            return;
        }

        selectionChanged?.Invoke(newIndex);
        IlInspectorView.EnsureSelectionVisible(state, sp, newIndex, rows.Count);
        state.App.Invalidate();
    }

    private static void ActivateCurrent(
        IReadOnlyList<IlTreeRow> rows,
        DotsiderState state,
        Action<int>? itemActivated)
    {
        if (rows.Count == 0) return;
        var idx = ResolveEffectiveIndex(rows, state.IlFocusedTreeKey);
        if (idx >= 0 && idx < rows.Count)
            itemActivated?.Invoke(idx);
    }

    private static void HandleLeft(
        InputBindingActionContext e,
        IReadOnlyList<IlTreeRow> rows,
        DotsiderState state,
        Action<int>? selectionChanged,
        Action<int>? collapseRow)
    {
        if (e.FocusedNode is not ScrollPanelNode sp) return;
        if (rows.Count == 0) return;
        var idx = ResolveEffectiveIndex(rows, state.IlFocusedTreeKey);
        if (idx < 0 || idx >= rows.Count) return;

        var row = rows[idx];
        if (row is { CanExpand: true, IsExpanded: true })
        {
            collapseRow?.Invoke(idx);
            return;
        }

        // Move selection to the parent (the closest preceding row at Depth - 1).
        if (row.Depth == 0) return;
        for (var i = idx - 1; i >= 0; i--)
        {
            if (rows[i].Depth < row.Depth)
            {
                selectionChanged?.Invoke(i);
                IlInspectorView.EnsureSelectionVisible(state, sp, i, rows.Count);
                state.App.Invalidate();
                return;
            }
        }
    }

    private static void HandleRight(
        InputBindingActionContext e,
        IReadOnlyList<IlTreeRow> rows,
        DotsiderState state,
        Action<int>? selectionChanged,
        Action<int>? expandRow)
    {
        if (e.FocusedNode is not ScrollPanelNode sp) return;
        if (rows.Count == 0) return;
        var idx = ResolveEffectiveIndex(rows, state.IlFocusedTreeKey);
        if (idx < 0 || idx >= rows.Count) return;

        var row = rows[idx];
        if (row is { CanExpand: true, IsExpanded: false })
        {
            expandRow?.Invoke(idx);
            return;
        }

        // Already expanded (or leaf) — move to first child if one exists in the next row.
        if (row.IsExpanded && idx + 1 < rows.Count && rows[idx + 1].Depth == row.Depth + 1)
        {
            var childIdx = idx + 1;
            selectionChanged?.Invoke(childIdx);
            IlInspectorView.EnsureSelectionVisible(state, sp, childIdx, rows.Count);
            state.App.Invalidate();
        }
    }
}
