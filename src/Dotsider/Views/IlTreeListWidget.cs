using Hex1b;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// A composite widget that renders the IL Inspector's namespace/type/method tree
/// as a flattened list with indentation and expand/collapse glyphs.
/// Wraps <see cref="ListWidget"/> and syncs <see cref="ListNode.SelectedIndex"/>
/// from the externally controlled <see cref="FocusedKey"/>.
/// </summary>
public sealed record IlTreeListWidget : CompositeWidget<IlTreeListNode>
{
    /// <summary>The flattened tree rows to display.</summary>
    public required IReadOnlyList<IlTreeRow> Rows { get; init; }

    /// <summary>The formatted display strings for each row.</summary>
    public required IReadOnlyList<string> FormattedRows { get; init; }

    /// <summary>The focused row key, or null.</summary>
    public string? FocusedKey { get; init; }

    /// <summary>Callback when selection changes (arrow keys or click). Receives row index.</summary>
    public Action<int>? SelectionChanged { get; init; }

    /// <summary>Callback when an item is activated (Enter key). Receives row index.</summary>
    public Action<int>? ItemActivated { get; init; }

    /// <summary>Callback to expand a row (Right arrow). Receives row index.</summary>
    public Action<int>? ExpandRow { get; init; }

    /// <summary>Callback to collapse a row (Left arrow). Receives row index.</summary>
    public Action<int>? CollapseRow { get; init; }

    /// <inheritdoc/>
    protected override void UpdateNode(IlTreeListNode node)
    {
        node.Rows = Rows;
        node.FocusedKey = FocusedKey;
        node.SelectionChangedCallback = SelectionChanged;
        node.ItemActivatedCallback = ItemActivated;
        node.ExpandCallback = ExpandRow;
        node.CollapseCallback = CollapseRow;

        // Sync FocusedKey → ListNode.SelectedIndex
        if (FocusedKey is not null)
        {
            var listNode = node.FindListNode();
            if (listNode is not null)
            {
                var targetIndex = FindRowIndex(Rows, FocusedKey);
                if (targetIndex >= 0 && listNode.SelectedIndex != targetIndex)
                    listNode.SelectedIndex = targetIndex;
            }
        }
    }

    /// <inheritdoc/>
    protected override Task<Hex1bWidget> BuildContentAsync(IlTreeListNode node, ReconcileContext context)
    {
        var initialIndex = FocusedKey is not null
            ? Math.Max(0, FindRowIndex(Rows, FocusedKey))
            : 0;

        // Return raw ListWidget so node.ContentChild is the ListNode directly.
        // Theme overrides (hiding the selection indicator) are applied at the call site.
        Hex1bWidget content = new ListWidget(FormattedRows)
        {
            InitialSelectedIndex = initialIndex
        }
        .OnSelectionChanged(e => node.SelectionChangedCallback?.Invoke(e.SelectedIndex))
        .OnItemActivated(e => node.ItemActivatedCallback?.Invoke(e.ActivatedIndex))
        .WithInputBindings(bindings =>
        {
            bindings.Key(Hex1bKey.RightArrow).Action(_ =>
            {
                var ln = node.FindListNode();
                if (ln is not null && ln.SelectedIndex >= 0 && ln.SelectedIndex < node.Rows.Count)
                    node.ExpandCallback?.Invoke(ln.SelectedIndex);
            }, "Expand");

            bindings.Key(Hex1bKey.LeftArrow).Action(_ =>
            {
                var ln = node.FindListNode();
                if (ln is not null && ln.SelectedIndex >= 0 && ln.SelectedIndex < node.Rows.Count)
                    node.CollapseCallback?.Invoke(ln.SelectedIndex);
            }, "Collapse");
        })
        .FillWidth()
        .FillHeight();

        return Task.FromResult(content);
    }

    private static int FindRowIndex(IReadOnlyList<IlTreeRow> rows, string key)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            if (rows[i].Key == key)
                return i;
        }
        return -1;
    }
}
