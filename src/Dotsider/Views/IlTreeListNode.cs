using Hex1b;
using Hex1b.Nodes;

namespace Dotsider.Views;

/// <summary>
/// Node for the IL tree list composite widget.
/// Holds mutable state that survives reconciliation.
/// </summary>
public sealed class IlTreeListNode : CompositeNode
{
    /// <summary>The current flattened tree rows.</summary>
    public IReadOnlyList<IlTreeRow> Rows { get; set; } = [];

    /// <summary>The focused row key, or null.</summary>
    public string? FocusedKey { get; set; }

    /// <summary>Callback when selection changes (arrow keys or click). Receives row index.</summary>
    public Action<int>? SelectionChangedCallback { get; set; }

    /// <summary>Callback when an item is activated (Enter key). Receives row index.</summary>
    public Action<int>? ItemActivatedCallback { get; set; }

    /// <summary>Callback to expand a row. Receives row index.</summary>
    public Action<int>? ExpandCallback { get; set; }

    /// <summary>Callback to collapse a row. Receives row index.</summary>
    public Action<int>? CollapseCallback { get; set; }

    /// <summary>
    /// Recursively finds the inner <see cref="ListNode"/> through the child tree.
    /// Handles any intermediate wrapper nodes (ThemePanel, Layout, etc.).
    /// </summary>
    public ListNode? FindListNode()
    {
        return FindListNodeRecursive(ContentChild);
    }

    private static ListNode? FindListNodeRecursive(Hex1bNode? node)
    {
        if (node is ListNode listNode)
            return listNode;
        if (node is null)
            return null;
        foreach (var child in node.GetChildren())
        {
            var found = FindListNodeRecursive(child);
            if (found is not null)
                return found;
        }
        return null;
    }
}
