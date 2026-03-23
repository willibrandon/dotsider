using Hex1b;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// A composite widget that hosts the IL disassembly editor. When the
/// <see cref="EditorKey"/> changes (different method or analyzer reload),
/// the inner EditorNode is discarded and recreated, which resets native
/// scroll to line 1. When the key is unchanged (tab switch), the same
/// EditorNode survives and preserves its scroll position.
/// </summary>
public sealed record IlEditorHostWidget : CompositeWidget<IlEditorHostNode>
{
    /// <summary>
    /// Identity key for the editor content. Change this to force a fresh EditorNode.
    /// Typically <c>(state.Analyzer, method.Token)</c>.
    /// </summary>
    public required object EditorKey { get; init; }

    /// <summary>The editor state containing the IL document and cursor.</summary>
    public required EditorState State { get; init; }

    /// <summary>The shared application state for decoration providers.</summary>
    public required DotsiderState AppState { get; init; }

    /// <inheritdoc/>
    protected override void UpdateNode(IlEditorHostNode node)
    {
        // When the key changes, discard the old EditorNode so reconciliation
        // creates a fresh one with _scrollOffset starting at 0 (→ 1 after ArrangeCore).
        if (!Equals(node.LastEditorKey, EditorKey))
        {
            node.LastEditorKey = EditorKey;
            node.ContentChild = null;
        }
    }

    /// <inheritdoc/>
    protected override Task<Hex1bWidget> BuildContentAsync(IlEditorHostNode node, ReconcileContext context)
    {
        Hex1bWidget content = new ThemePanelWidget(
            t => t
                .Set(EditorTheme.SelectionForegroundColor, Hex1bColor.Default)
                .Set(EditorTheme.SelectionBackgroundColor, Hex1bColor.FromRgb(79, 82, 88)),
            new EditorWidget(State)
                .Decorations(AppState.IlSyntaxProvider)
                .Decorations(AppState.IlSearchProvider)
                .Decorations(AppState.IlYankProvider)
                .WithInputBindings(bindings =>
                {
                    TextObjectHelper.ConfigureReadOnlyEditorBindings(
                        bindings,
                        State,
                        () => AppState.VimPending,
                        () => AppState.VimPendingEditor,
                        () => AppState.VimPendingCursorOffset,
                        () => AppState.VimPendingTimestamp,
                        (s, e, o) => { AppState.VimPending = s; AppState.VimPendingEditor = e; AppState.VimPendingCursorOffset = o; AppState.VimPendingTimestamp = DateTime.UtcNow; },
                        AppState.PerformEditorYank,
                        () => AppState.App.Invalidate());
                })
                .FillWidth()
                .FillHeight())
            .FillWidth()
            .FillHeight();

        return Task.FromResult(content);
    }
}
