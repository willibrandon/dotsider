using Hex1b;
using Hex1b.Input;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// A composite widget that hosts the IL disassembly editor. When the
/// <see cref="EditorKey"/> changes (different method or analyzer reload),
/// the inner EditorNode is discarded and recreated, which resets native
/// scroll to line 1. When the key is unchanged (tab switch), the same
/// EditorNode survives and preserves its scroll position.
///
/// For go-to-definition back-navigation, evicted EditorNodes are cached
/// by key so they can be restored with their scroll position intact.
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
        if (!Equals(node.LastEditorKey, EditorKey))
        {
            if (node.LastEditorKey is not null && node.ContentChild is not null)
                node.SavedEditors[node.LastEditorKey] = node.ContentChild;

            node.LastEditorKey = EditorKey;

            if (node.SavedEditors.Remove(EditorKey, out var saved))
                node.ContentChild = saved;
            else
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
                .Decorations(AppState.IlNavigationProvider)
                .WithInputBindings(bindings =>
                {
                    // Escape: IL back navigation takes priority over vim cancel.
                    // Must be registered BEFORE TextObjectHelper which also binds Escape.
                    // First match wins in the binding walk.
                    bindings.Key(Hex1bKey.Escape).Action(_ =>
                    {
                        if (AppState.IlBackStack.Count > 0)
                        {
                            var entry = AppState.IlBackStack.Pop();
                            AppState.RestoreFromIlBackEntry(entry);
                        }
                        else
                        {
                            // Fall through: reset vim text-object state (matches TextObjectHelper behavior)
                            AppState.VimPending = VimMotionState.Idle;
                            AppState.App.Invalidate();
                        }
                    }, "Back");

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

                    bindings.Key(Hex1bKey.Enter).Action(_ => PerformGoToDefinition(), "Go to definition");

                    bindings.Key(Hex1bKey.G).Action(_ =>
                    {
                        AppState.IlGdPending = true;
                        AppState.IlGdTimestamp = DateTime.UtcNow;
                        AppState.App.Invalidate();
                    }, "");

                    if (AppState.IlGdPending)
                    {
                        bindings.Key(Hex1bKey.D).Action(_ =>
                        {
                            AppState.IlGdPending = false;
                            PerformGoToDefinition();
                        }, "Go to definition");
                    }

                    if (AppState.IlGdPending
                        && (DateTime.UtcNow - AppState.IlGdTimestamp).TotalSeconds > 1.0)
                        AppState.IlGdPending = false;
                })
                .FillWidth()
                .FillHeight())
            .FillWidth()
            .FillHeight();

        return Task.FromResult(content);
    }

    private void PerformGoToDefinition()
    {
        if (AppState.IlInstructions is { } instructions
            && AppState.IlEditorState is { } es)
        {
            var inst = IlNavigationHelper.GetInstructionAtCursor(
                es, instructions, AppState.IlHeaderLineCount);
            if (inst?.MetadataToken is not null)
            {
                AppState.NavigateToIlDefinition(inst.MetadataToken.Value);
                AppState.App.Invalidate();
            }
        }
    }
}
