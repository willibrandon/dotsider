using Hex1b;
using Hex1b.Input;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Builds the IL disassembly editor widget with themed selection colors,
/// vim keybindings, and go-to-definition support.
/// </summary>
internal static class IlEditorHost
{
    /// <summary>
    /// Builds the themed IL editor widget with all input bindings for
    /// navigation, vim motions, and go-to-definition.
    /// </summary>
    /// <param name="editorState">The editor state containing the IL document and cursor.</param>
    /// <param name="state">The shared application state for decoration providers and navigation.</param>
    /// <returns>A composed widget tree ready for rendering.</returns>
    internal static Hex1bWidget Build(EditorState editorState, DotsiderState state)
    {
        return new ThemePanelWidget(
            t => t
                .Set(EditorTheme.SelectionForegroundColor, Hex1bColor.Default)
                .Set(EditorTheme.SelectionBackgroundColor, Hex1bColor.FromRgb(79, 82, 88)),
            new EditorWidget(editorState)
                .Decorations(state.IlSyntaxProvider)
                .Decorations(state.IlSearchProvider)
                .Decorations(state.IlYankProvider)
                .Decorations(state.IlNavigationProvider)
                .InputBindings(bindings =>
                {
                    // Escape: IL back navigation takes priority over vim cancel.
                    // Must be registered BEFORE TextObjectHelper which also binds Escape.
                    // First match wins in the binding walk.
                    bindings.Key(Hex1bKey.Escape).Action(_ =>
                    {
                        if (state.IlBackStack.Count > 0)
                        {
                            var entry = state.IlBackStack.Pop();
                            state.RestoreFromIlBackEntry(entry);
                        }
                        else
                        {
                            // Fall through: reset vim text-object state (matches TextObjectHelper behavior)
                            state.VimPending = VimMotionState.Idle;
                            state.App.Invalidate();
                        }
                    }, "Back");

                    TextObjectHelper.ConfigureReadOnlyEditorBindings(
                        bindings,
                        editorState,
                        () => state.VimPending,
                        () => state.VimPendingEditor,
                        () => state.VimPendingCursorOffset,
                        () => state.VimPendingTimestamp,
                        (s, e, o) => { state.VimPending = s; state.VimPendingEditor = e; state.VimPendingCursorOffset = o; state.VimPendingTimestamp = DateTime.UtcNow; },
                        state.PerformEditorYank,
                        () => state.App.Invalidate());

                    bindings.Key(Hex1bKey.Enter).Action(_ => PerformGoToDefinition(state), "Go to definition");

                    bindings.Key(Hex1bKey.G).Action(_ =>
                    {
                        state.IlGdPending = true;
                        state.IlGdTimestamp = DateTime.UtcNow;
                        state.App.Invalidate();
                    }, "");

                    if (state.IlGdPending)
                    {
                        bindings.Key(Hex1bKey.D).Action(_ =>
                        {
                            state.IlGdPending = false;
                            PerformGoToDefinition(state);
                        }, "Go to definition");
                    }

                    if (state.IlGdPending
                        && (DateTime.UtcNow - state.IlGdTimestamp).TotalSeconds > 1.0)
                        state.IlGdPending = false;
                })
                .FillWidth()
                .FillHeight())
            .FillWidth()
            .FillHeight();
    }

    private static void PerformGoToDefinition(DotsiderState state)
    {
        if (state.IlInstructions is { } instructions
            && state.IlEditorState is { } es)
        {
            var inst = IlNavigationHelper.GetInstructionAtCursor(
                es, instructions, state.IlHeaderLineCount);
            if (inst?.MetadataToken is not null)
            {
                state.NavigateToIlDefinition(inst.MetadataToken.Value);
                state.App.Invalidate();
            }
        }
    }
}
