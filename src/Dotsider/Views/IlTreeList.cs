using Hex1b;
using Hex1b.Input;
using Hex1b.Nodes;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Builds the IL Inspector's namespace/type/method tree as a non-focusable VStack
/// of <see cref="TextBlockWidget"/> rows wrapped in a <see cref="ScrollPanelWidget"/>.
/// The panel owns viewport scrolling, the scrollbar gutter, mouse wheel, thumb drag,
/// and track-click pagination — independent of the selected row, so wheel scrolls
/// can push the selection offscreen and stay there. Selection lives in
/// <see cref="DotsiderState.IlFocusedTreeKey"/>; keyboard handlers move the key and
/// pull the viewport along via <see cref="IlInspectorView.EnsureSelectionVisible(ScrollPanelNode, int)"/>.
/// </summary>
internal static class IlTreeList
{
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
    /// <returns>A composed <see cref="ScrollPanelWidget"/>-rooted widget tree.</returns>
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

        var rowWidgets = new Hex1bWidget[formattedRows.Count];
        for (var i = 0; i < formattedRows.Count; i++)
        {
            var text = formattedRows[i];
            Hex1bWidget rowWidget = new TextBlockWidget(text);
            if (i == selectedIndex)
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

        return new ScrollPanelWidget(stack, ScrollOrientation.Vertical, showScrollbar: true)
            .InputBindings(bindings =>
            {
                // Replace the panel's default scroll-the-viewport keys with
                // selection-driven navigation. The wheel and gutter-drag defaults
                // (MouseScrollUpAction/MouseScrollDownAction, Drag(Left)) stay in place.
                bindings.Remove(ScrollPanelWidget.ScrollUpAction);
                bindings.Remove(ScrollPanelWidget.ScrollDownAction);
                bindings.Remove(ScrollPanelWidget.ScrollLeftAction);
                bindings.Remove(ScrollPanelWidget.ScrollRightAction);
                bindings.Remove(ScrollPanelWidget.PageUpAction);
                bindings.Remove(ScrollPanelWidget.PageDownAction);
                bindings.Remove(ScrollPanelWidget.ScrollToStartAction);
                bindings.Remove(ScrollPanelWidget.ScrollToEndAction);

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

                bindings.Mouse(MouseButton.Left).Action(e =>
                {
                    if (e.FocusedNode is not ScrollPanelNode sp) return;
                    if (rows.Count == 0) return;

                    // The rightmost column is the scrollbar gutter only when the panel
                    // is actually scrollable; when content fits, that column is normal
                    // row area and a click there must still select the row.
                    var localX = e.MouseX - sp.Bounds.X;
                    if (sp.IsScrollable && localX >= sp.Bounds.Width - 1) return;

                    var rowIndex = (e.MouseY - sp.Bounds.Y) + sp.Offset;
                    if (rowIndex < 0 || rowIndex >= rows.Count) return;

                    selectionChanged?.Invoke(rowIndex);
                    itemActivated?.Invoke(rowIndex);
                }, "Select row");
            })
            .Fill();
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
            IlInspectorView.EnsureSelectionVisible(sp, currentIdx);
            state.App.Invalidate();
            return;
        }

        selectionChanged?.Invoke(newIndex);
        IlInspectorView.EnsureSelectionVisible(sp, newIndex);
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
            IlInspectorView.EnsureSelectionVisible(sp, currentIdx);
            state.App.Invalidate();
            return;
        }

        selectionChanged?.Invoke(newIndex);
        IlInspectorView.EnsureSelectionVisible(sp, newIndex);
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
                IlInspectorView.EnsureSelectionVisible(sp, i);
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
            IlInspectorView.EnsureSelectionVisible(sp, childIdx);
            state.App.Invalidate();
        }
    }
}
