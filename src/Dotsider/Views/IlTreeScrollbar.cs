using Hex1b;
using Hex1b.Input;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// The IL tree's scrollbar gutter, driven by <see cref="DotsiderState.IlTreeScrollOffset"/>:
/// a one-column visual (<see cref="Build"/>) rendered inside the tree panel's child, and the
/// press resolution (<see cref="HandleDrag"/>) the panel binds on its own gutter column —
/// thumb drag and track-click paging, mirroring the <see cref="ScrollPanelWidget"/> built-in
/// gutter. The column carries no bindings and nothing focusable: Hex1b routes mouse presses
/// to the focusable hit node, so the gutter must sit inside the panel's bounds for clicks
/// to reach it, and the panel must stay the tree's only focusable.
/// </summary>
internal static class IlTreeScrollbar
{
    /// <summary>
    /// Builds the gutter column visual for the current scroll state.
    /// </summary>
    /// <param name="state">The shared application state owning the scroll offset.</param>
    /// <param name="rowCount">The total flattened tree row count (the content size).</param>
    /// <param name="viewportSize">The tree viewport height in rows (the track length).</param>
    /// <returns>The one-column gutter widget.</returns>
    internal static Hex1bWidget Build(DotsiderState state, int rowCount, int viewportSize)
    {
        var (thumbPosition, thumbSize) = ThumbMetrics(state, rowCount, viewportSize);

        var cells = new Hex1bWidget[viewportSize];
        for (var y = 0; y < viewportSize; y++)
        {
            var isThumb = y >= thumbPosition && y < thumbPosition + thumbSize;
            // Glyphs match Hex1b's ScrollTheme defaults; colors come from the live theme.
            cells[y] = new ThemePanelWidget(
                theme => theme.Set(GlobalTheme.ForegroundColor,
                    theme.Get(isThumb ? ScrollTheme.ThumbColor : ScrollTheme.TrackColor)),
                new TextBlockWidget(isThumb ? "▉" : "│"));
        }

        return new VStackWidget(cells).FixedWidth(1);
    }

    /// <summary>
    /// Resolves a Left press on the gutter column: a drag handler when it lands on the
    /// thumb, otherwise a one-page jump toward the press — the same behavior as the
    /// <see cref="ScrollPanelWidget"/> gutter.
    /// </summary>
    /// <param name="state">The shared application state owning the scroll offset.</param>
    /// <param name="rowCount">The total flattened tree row count.</param>
    /// <param name="viewportSize">The tree viewport height in rows.</param>
    /// <param name="localY">The press row relative to the panel top.</param>
    /// <returns>The drag handler; empty when the tree is not scrollable.</returns>
    internal static DragHandler HandleDrag(DotsiderState state, int rowCount, int viewportSize, int localY)
    {
        var maxOffset = Math.Max(0, rowCount - viewportSize);
        if (maxOffset == 0) return new DragHandler();

        var (thumbPosition, thumbSize) = ThumbMetrics(state, rowCount, viewportSize);
        if (localY >= thumbPosition && localY < thumbPosition + thumbSize)
        {
            // Thumb drag: same content-per-cell ratio as the panel gutter. The start
            // offset is captured once so rebuilds during the drag cannot skew the delta.
            var startOffset = state.IlTreeScrollOffset;
            var scrollRange = viewportSize - thumbSize;
            var contentPerCell = scrollRange > 0 ? (double)maxOffset / scrollRange : 0;
            return DragHandler.Simple(onMove: (_, deltaY) =>
            {
                if (contentPerCell <= 0) return;
                var next = (int)Math.Round(startOffset + deltaY * contentPerCell);
                SetOffset(state, Math.Clamp(next, 0, maxOffset));
            });
        }

        // Track click: page toward the press, one viewport minus a row of overlap.
        var page = Math.Max(1, viewportSize - 1);
        var delta = localY < thumbPosition ? -page : +page;
        SetOffset(state, Math.Clamp(state.IlTreeScrollOffset + delta, 0, maxOffset));
        return new DragHandler();
    }

    /// <summary>Computes the thumb position and size with the ScrollPanelNode gutter math.</summary>
    private static (int Position, int Size) ThumbMetrics(DotsiderState state, int rowCount, int viewportSize)
    {
        var maxOffset = Math.Max(0, rowCount - viewportSize);
        var offset = Math.Clamp(state.IlTreeScrollOffset, 0, maxOffset);
        var thumbSize = Math.Max(1, (int)Math.Ceiling((double)viewportSize / rowCount * viewportSize));
        var scrollRange = viewportSize - thumbSize;
        var thumbPosition = scrollRange > 0 && maxOffset > 0
            ? (int)Math.Round((double)offset / maxOffset * scrollRange)
            : 0;
        return (thumbPosition, thumbSize);
    }

    private static void SetOffset(DotsiderState state, int next)
    {
        if (next == state.IlTreeScrollOffset) return;
        state.IlTreeScrollOffset = next;
        state.App.Invalidate();
    }
}
