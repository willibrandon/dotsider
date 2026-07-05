using Dotsider.Core.Analysis.Models;
using Hex1b;
using Hex1b.Documents;
using Hex1b.Input;
using Hex1b.Theming;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Builds the native disassembly editor for the pre-ILC side-by-side pair pane — the
/// sibling of <see cref="IlEditorHost"/> that attaches only the pair-scoped decoration
/// providers, so the two live editors never share span-driven state.
/// </summary>
internal static class IlPairNativeEditorHost
{
    /// <summary>
    /// Builds the themed pair-pane editor with vim motions, yank, and correlation-aware
    /// go-to-definition.
    /// </summary>
    /// <param name="editorState">The pair pane's editor state.</param>
    /// <param name="state">The shared application state.</param>
    internal static Hex1bWidget Build(EditorState editorState, DotsiderState state)
    {
        return new ThemePanelWidget(
            t => t
                .Set(EditorTheme.SelectionForegroundColor, Hex1bColor.Default)
                .Set(EditorTheme.SelectionBackgroundColor, Hex1bColor.FromRgb(79, 82, 88)),
            new EditorWidget(editorState)
                .Decorations(state.IlPairNativeSyntaxProvider)
                .Decorations(state.IlPairSearchProvider)
                .Decorations(state.IlPairYankProvider)
                .Decorations(state.IlPairNativeNavigationProvider)
                .InputBindings(bindings =>
                {
                    // Esc returns from an intra-listing local-label jump before falling through to
                    // vim cancel. Registered before TextObjectHelper (which also binds Escape) so
                    // the back stack wins — first match in the binding walk.
                    bindings.Key(Hex1bKey.Escape).Action(_ =>
                    {
                        if (state.IlPairNativeBackStack.Count > 0)
                        {
                            state.IlPairNativeEditorState?.SetCursorPosition(
                                new DocumentOffset(state.IlPairNativeBackStack.Pop()));
                            state.App.Invalidate();
                        }
                        else
                        {
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

    /// <summary>
    /// Resolves the instruction target under the pair pane's cursor: a local label jumps
    /// within the listing; a correlated symbol selects its managed method (both panes
    /// follow, with an IL back entry for Esc); an uncorrelated symbol flips the tree to
    /// the native view and navigates there — full native capability is never lost.
    /// </summary>
    private static void PerformGoToDefinition(DotsiderState state)
    {
        if (state.IlPairNativeInstructions is not { } instructions
            || state.IlPairNativeEditorState is not { } editor
            || state.Analyzer.NativeSymbols is not { } info)
        {
            return;
        }

        var inst = NativeNavigationHelper.GetInstructionAtCursor(editor, instructions);
        if (inst?.TargetAddress is not { } target)
            return;

        if (inst.TargetKind == NativeTargetKind.LocalLabel)
        {
            if (instructions.FirstOrDefault(i => i.Address == target)?.DisplayLine is { } line)
            {
                // Record the departure cursor so Esc returns here — mirrors the solo native editor.
                state.IlPairNativeBackStack.Push(editor.Cursor.Position.Value);
                var offset = LineStartOffset(editor.Document.GetText(), line);
                editor.SetCursorPosition(new DocumentOffset(offset));
                state.App.Invalidate();
            }

            return;
        }

        if (!info.TryFindByAddress(target, out var symbol))
            return;

        if (state.PreIlcIndex?.FindByAddress(symbol.VirtualAddress) is { } correlation
            && state.Analyzer.PreIlcCompanions is { } companions)
        {
            var member = companions.FindByAssemblyName(correlation.AssemblyName);
            var owner = member is not null && !ReferenceEquals(member, companions.Root) ? member : null;
            state.NavigateToPreIlcMethod(correlation.Method, owner);
            return;
        }

        // No managed source (runtime/stub code): flip to the native tree and go there.
        state.IlAotTreeNativeView = true;
        state.NavigateToNativeSymbol(symbol);
        state.App.Invalidate();
    }

    private static int LineStartOffset(string text, int line)
    {
        if (line <= 1) return 0;
        var current = 1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\n') continue;
            if (++current == line) return i + 1;
        }

        return 0;
    }
}
