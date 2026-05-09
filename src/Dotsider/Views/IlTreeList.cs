using Hex1b;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Builds the IL Inspector's namespace/type/method tree as a flattened list
/// with expand/collapse support via arrow keys.
/// </summary>
internal static class IlTreeList
{
    /// <summary>
    /// Builds the tree list widget with input bindings for expand/collapse.
    /// </summary>
    /// <param name="rows">The flattened tree rows.</param>
    /// <param name="formattedRows">The formatted display strings for each row.</param>
    /// <param name="focusedKey">The focused row key, or null.</param>
    /// <param name="selectionChanged">Callback when selection changes. Receives row index.</param>
    /// <param name="itemActivated">Callback when an item is activated. Receives row index.</param>
    /// <param name="expandRow">Callback to expand a row. Receives row index.</param>
    /// <param name="collapseRow">Callback to collapse a row. Receives row index.</param>
    /// <param name="captureNode">Callback to capture the reconciled ListNode for per-render SelectedIndex sync.</param>
    /// <returns>A composed widget tree ready for rendering.</returns>
    internal static Hex1bWidget Build(
        IReadOnlyList<IlTreeRow> rows,
        IReadOnlyList<string> formattedRows,
        string? focusedKey,
        Action<int>? selectionChanged,
        Action<int>? itemActivated,
        Action<int>? expandRow,
        Action<int>? collapseRow,
        Action<ListNode>? captureNode = null)
    {
        var initialIndex = focusedKey is not null
            ? Math.Max(0, FindRowIndex(rows, focusedKey))
            : 0;

        return new ListWidget(formattedRows)
        {
            InitialSelectedIndex = initialIndex
        }
        .OnSelectionChanged(e =>
        {
            captureNode?.Invoke(e.Node);
            selectionChanged?.Invoke(e.SelectedIndex);
        })
        .OnItemActivated(e =>
        {
            captureNode?.Invoke(e.Node);
            itemActivated?.Invoke(e.ActivatedIndex);
        })
        .InputBindings(bindings =>
        {
            bindings.Key(Hex1bKey.RightArrow).Action(_ =>
            {
                if (focusedKey is not null)
                {
                    var index = FindRowIndex(rows, focusedKey);
                    if (index >= 0 && index < rows.Count)
                        expandRow?.Invoke(index);
                }
            }, "Expand");

            bindings.Key(Hex1bKey.LeftArrow).Action(_ =>
            {
                if (focusedKey is not null)
                {
                    var index = FindRowIndex(rows, focusedKey);
                    if (index >= 0 && index < rows.Count)
                        collapseRow?.Invoke(index);
                }
            }, "Collapse");
        })
        .FillWidth()
        .FillHeight();
    }

    internal static int FindRowIndex(IReadOnlyList<IlTreeRow> rows, string key)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].Key == key)
                return i;
        }
        return -1;
    }
}
