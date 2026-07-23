using Dotsider.Core.Analysis.Models;
using Hex1b;
using Hex1b.Documents;
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
                .Decorations(state.IlNativeSyntaxProvider)
                .Decorations(state.IlSourceLinkProvider)
                .Decorations(state.IlSearchProvider)
                .Decorations(state.IlYankProvider)
                .Decorations(state.IlNavigationProvider)
                .Decorations(state.IlNativeNavigationProvider)
                .InputBindings(bindings =>
                {
                    // Escape: IL back navigation takes priority over vim cancel.
                    // Must be registered BEFORE TextObjectHelper which also binds Escape.
                    // First match wins in the binding walk.
                    bindings.Key(Hex1bKey.Escape).Action(_ =>
                    {
                        if (state.IlNativeBackStack.Count > 0)
                        {
                            state.RestoreFromNativeBackEntry(state.IlNativeBackStack.Pop());
                        }
                        else if (state.IlBackStack.Count > 0)
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
                    bindings.Key(Hex1bKey.O).Action(_ => OpenEmbeddedSource(state), "Open embedded source");
                    bindings.Key(Hex1bKey.U).Action(ctx => YankSourceLinkUrl(state, ctx), "Yank source URL");

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

    private static void PerformGoToDefinition(DotsiderState state)
    {
        // Native mode: resolve the target of the instruction under the cursor.
        if (state.IlNativeInstructions is { } nativeInstructions
            && state.IlEditorState is { } nativeEditor
            && state.Analyzer.NativeSymbols is { } info)
        {
            var inst = NativeNavigationHelper.GetInstructionAtCursor(nativeEditor, nativeInstructions);
            if (inst?.TargetAddress is not { } target)
                return;

            // An intra-function local label jumps within the current listing, not to another symbol;
            // record the pre-jump cursor so Esc returns here.
            if (inst.TargetKind == NativeTargetKind.LocalLabel)
            {
                if (nativeInstructions.FirstOrDefault(i => i.Address == target)?.DisplayLine is { } line
                    && state.IlSelectedNativeSymbol is { } currentSymbol)
                {
                    state.IlNativeBackStack.Push(new NativeBackEntry(
                        currentSymbol, nativeEditor, state.IlEditorKey,
                        state.IlNativeInstructions, state.IlNativeHeaderLineCount,
                        state.IlFocusedTreeKey,
                        new Dictionary<string, bool>(state.IlTreeExpansionState),
                        nativeEditor.Cursor.Position.Value));
                    var offset = LineStartOffset(nativeEditor.Document.GetText(), line);
                    nativeEditor.SetCursorPosition(new DocumentOffset(offset));
                    state.App.Invalidate();
                }

                return;
            }

            if (info.TryFindByAddress(target, out var symbol))
            {
                state.NavigateToNativeSymbol(symbol);
                state.App.Invalidate();
            }

            return;
        }

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

    private static void YankSourceLinkUrl(DotsiderState state, InputBindingActionContext ctx)
    {
        if (state.IlInstructions is not { } instructions
            || state.IlEditorState is not { } editorState)
            return;

        var url = IlNavigationHelper.GetSourceLinkUrlAtCursor(editorState, instructions);
        if (url is null)
        {
            state.ShowTransientNotice("No Source Link URL at cursor");
            return;
        }

        ctx.CopyToClipboard(url);
        if (IlNavigationHelper.GetSourceLinkYankRangeAtCursor(editorState, instructions) is { } range)
            FlashSourceLinkMarker(state, range);
        state.ShowTransientNotice("Yanked Source Link URL");
    }

    private static void FlashSourceLinkMarker(
        DotsiderState state,
        (DocumentPosition Start, DocumentPosition End) range)
    {
        state.IlYankProvider.HighlightRange = range;
        state.App.Invalidate();
        _ = Task.Delay(TimeSpan.FromMilliseconds(150)).ContinueWith(_ =>
        {
            state.IlYankProvider.HighlightRange = null;
            state.App.Invalidate();
        }, TaskScheduler.Default);
    }

    private static void OpenEmbeddedSource(DotsiderState state)
    {
        if (state.IlSelectedMethod is null)
            return;

        var source = (state.IlSelectedMethodOwner ?? state.MetadataAnalyzer)
            .GetEmbeddedSource(state.IlSelectedMethod);
        if (source is null)
        {
            state.ShowTransientNotice("No embedded source for this method");
            return;
        }

        string tempPath;
        try
        {
            tempPath = state.EmbeddedSourceTempFiles.Write(
                state.IlSelectedMethod.Name,
                source.Document,
                source.Bytes);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            state.ShowTransientNotice("Could not write embedded source");
            return;
        }

        var status = EditorLauncher.Launch(
            state.EmbeddedSourceTempFiles,
            tempPath,
            out var openedPath);
        if (status == EditorLaunchStatus.Started)
        {
            state.ShowTransientNotice($"Opened embedded source: {openedPath}");
            return;
        }

        state.ShowTransientNotice($"Could not open embedded source: {openedPath}");
    }
}
