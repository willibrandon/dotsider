using Hex1b.Nodes;

namespace Dotsider.Views;

/// <summary>
/// Node for the IL editor host composite widget.
/// Tracks the editor content key so that a fresh EditorNode is created
/// when the method or analyzer changes, resetting native scroll to line 1.
/// </summary>
public sealed class IlEditorHostNode : CompositeNode
{
    /// <summary>
    /// The last editor key that was reconciled. When the key changes,
    /// <see cref="CompositeNode.ContentChild"/> is set to null to force
    /// creation of a fresh EditorNode.
    /// </summary>
    public object? LastEditorKey { get; set; }
}
