using Hex1b;
using Hex1b.Documents;
using Hex1b.Input;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Implements vim-style text object selection (iw, iW) and configures
/// read-only editor bindings for the vim text-object state machine.
/// </summary>
public static class TextObjectHelper
{
    /// <summary>
    /// Selects the inner word under the cursor (vim <c>iw</c> semantics).
    /// Word characters are letters, digits, and underscores. Punctuation and
    /// whitespace each form their own classes. Newlines are boundaries.
    /// </summary>
    /// <param name="state">The editor state to modify.</param>
    public static void SelectInnerWord(EditorState state)
    {
        if (state.Document.Length == 0) return;
        var offset = state.Cursor.Position.Value;
        if (offset >= state.Document.Length) return;

        var text = state.Document.GetText();
        var ch = text[offset];
        var charClass = ClassifyChar(ch);

        // Find contiguous run of same class
        var start = offset;
        while (start > 0 && ClassifyChar(text[start - 1]) == charClass && text[start - 1] != '\n' && ch != '\n')
            start--;

        var end = offset;
        if (ch == '\n')
        {
            // Newline is its own "word"
            end = offset + 1;
        }
        else
        {
            while (end < text.Length && ClassifyChar(text[end]) == charClass && text[end] != '\n')
                end++;
        }

        if (start == end) return;

        // Position at last char (inclusive) so PerformEditorYank's cursor+1 extension
        // produces the correct exclusive end without grabbing a trailing delimiter.
        state.Cursor.SelectionAnchor = new DocumentOffset(start);
        state.Cursor.Position = new DocumentOffset(end - 1);
    }

    /// <summary>
    /// Selects the inner WORD under the cursor (vim <c>iW</c> semantics).
    /// A WORD is any contiguous run of non-whitespace characters.
    /// Newlines are boundaries.
    /// </summary>
    /// <param name="state">The editor state to modify.</param>
    public static void SelectInnerWORD(EditorState state)
    {
        if (state.Document.Length == 0) return;
        var offset = state.Cursor.Position.Value;
        if (offset >= state.Document.Length) return;

        var text = state.Document.GetText();
        var ch = text[offset];
        var isWhitespace = ch != '\n' && char.IsWhiteSpace(ch);

        if (ch == '\n')
        {
            // Newline is its own "word" — anchor at offset, position at offset (single char)
            state.Cursor.SelectionAnchor = new DocumentOffset(offset);
            state.Cursor.Position = new DocumentOffset(offset);
            return;
        }

        var start = offset;
        var end = offset;

        if (isWhitespace)
        {
            // Select contiguous whitespace (same line)
            while (start > 0 && text[start - 1] != '\n' && char.IsWhiteSpace(text[start - 1]))
                start--;
            while (end < text.Length && text[end] != '\n' && char.IsWhiteSpace(text[end]))
                end++;
        }
        else
        {
            // Select contiguous non-whitespace (same line)
            while (start > 0 && text[start - 1] != '\n' && !char.IsWhiteSpace(text[start - 1]))
                start--;
            while (end < text.Length && text[end] != '\n' && !char.IsWhiteSpace(text[end]))
                end++;
        }

        if (start == end) return;

        state.Cursor.SelectionAnchor = new DocumentOffset(start);
        state.Cursor.Position = new DocumentOffset(end - 1);
    }

    /// <summary>
    /// Configures key, mouse, and drag bindings on a read-only <see cref="EditorWidget"/>
    /// to support vim text-object sequences (iw, iW, yiw, yiW) with comprehensive
    /// cancellation on any intervening input.
    /// </summary>
    /// <param name="bindings">The editor's input bindings builder.</param>
    /// <param name="thisEditorState">The <see cref="EditorState"/> of this specific editor.</param>
    /// <param name="getVimPending">Returns the current <see cref="VimMotionState"/>.</param>
    /// <param name="getVimPendingEditor">Returns the editor that started the sequence.</param>
    /// <param name="getVimPendingCursorOffset">Returns the cursor offset when the sequence was armed.</param>
    /// <param name="getVimPendingTimestamp">Returns the timestamp when the sequence was armed.</param>
    /// <param name="setVimState">Sets <see cref="VimMotionState"/>, pending editor, and cursor offset.</param>
    /// <param name="performYank">Called for <c>yiw</c>/<c>yiW</c> to yank the selection.</param>
    /// <param name="invalidate">Requests a UI refresh.</param>
    public static void ConfigureReadOnlyEditorBindings(
        InputBindingsBuilder bindings,
        EditorState thisEditorState,
        Func<VimMotionState> getVimPending,
        Func<EditorState?> getVimPendingEditor,
        Func<int> getVimPendingCursorOffset,
        Func<DateTime> getVimPendingTimestamp,
        Action<VimMotionState, EditorState?, int> setVimState,
        Action<InputBindingActionContext, EditorNode>? performYank,
        Action invalidate)
    {
        void ResetToIdle() => setVimState(VimMotionState.Idle, null, 0);

        // --- I binding (always registered — starts text object from Idle) ---
        bindings.Key(Hex1bKey.I).Action(ctx =>
        {
            // Timeout check — reset stale state, then fall through to process
            // this i as a fresh sequence start (don't discard the keypress)
            if (getVimPending() != VimMotionState.Idle
                && (DateTime.UtcNow - getVimPendingTimestamp()).TotalSeconds > 1.0)
            {
                ResetToIdle();
            }

            var pending = getVimPending();
            switch (pending)
            {
                case VimMotionState.Idle:
                    setVimState(
                        VimMotionState.WaitingForTextObject,
                        thisEditorState,
                        thisEditorState.Cursor.Position.Value);
                    break;

                case VimMotionState.WaitingForYMotion
                    when thisEditorState == getVimPendingEditor():
                    setVimState(
                        VimMotionState.WaitingForYTextObject,
                        thisEditorState,
                        thisEditorState.Cursor.Position.Value);
                    break;

                default:
                    // Editor mismatch on WaitingForYMotion, or unexpected state
                    ResetToIdle();
                    return;
            }

            invalidate();
        }, "");

        // --- Triple-click override: select line content only (no trailing newline) ---
        // The default EditorWidget triple-click uses SelectLineAt which positions the
        // cursor at the start of the NEXT line (exclusive end convention). PerformEditorYank
        // adds +1 to cursor.Position (inclusive/neovim convention for iw/iW). These two
        // conventions clash, causing yank to grab the newline plus the first character of
        // the next line. Fix: replace the default handler with one that positions the
        // cursor on the last visible character of the line (inclusive end).
        bindings.Remove(EditorWidget.TripleClick);
        bindings.Mouse(MouseButton.Left).TripleClick().Action(_ =>
        {
            SelectLine(thisEditorState);
            invalidate();
        }, "Triple-click to select line");

        // --- Shift+V: visual line select (vim V) ---
        bindings.Shift().Key(Hex1bKey.V).Action(_ =>
        {
            ResetToIdle();
            SelectLine(thisEditorState);
            invalidate();
        }, "Select line");

        // --- All other bindings (conditionally registered when pending) ---
        if (getVimPending() == VimMotionState.Idle) return;

        // W — select inner word (completion)
        bindings.Key(Hex1bKey.W).Action(ctx =>
        {
            CompleteTextObject(ctx, thisEditorState, getVimPending, getVimPendingEditor,
                getVimPendingCursorOffset, getVimPendingTimestamp, setVimState,
                performYank, invalidate, isWord: true);
        }, "");

        // Shift+W — select inner WORD (completion)
        bindings.Shift().Key(Hex1bKey.W).Action(ctx =>
        {
            CompleteTextObject(ctx, thisEditorState, getVimPending, getVimPendingEditor,
                getVimPendingCursorOffset, getVimPendingTimestamp, setVimState,
                performYank, invalidate, isWord: false);
        }, "");

        // --- Cancellation: printable keys ---
        for (var k = Hex1bKey.A; k <= Hex1bKey.Z; k++)
        {
            if (k is Hex1bKey.I or Hex1bKey.W) continue;
            bindings.Key(k).Action(_ => ResetToIdle(), "");
        }

        Hex1bKey[] cancelKeys =
        [
            Hex1bKey.D0, Hex1bKey.D1, Hex1bKey.D2, Hex1bKey.D3, Hex1bKey.D4,
            Hex1bKey.D5, Hex1bKey.D6, Hex1bKey.D7, Hex1bKey.D8, Hex1bKey.D9,
            Hex1bKey.Spacebar, Hex1bKey.None,
            Hex1bKey.OemComma, Hex1bKey.OemPeriod, Hex1bKey.OemMinus,
            Hex1bKey.OemPlus, Hex1bKey.OemQuestion, Hex1bKey.Oem1,
            Hex1bKey.Oem4, Hex1bKey.Oem5, Hex1bKey.Oem6, Hex1bKey.Oem7,
            Hex1bKey.OemTilde,
            // Non-navigation EditorNode bindings (no-ops on read-only)
            Hex1bKey.Backspace, Hex1bKey.Delete, Hex1bKey.Enter, Hex1bKey.Tab,
            Hex1bKey.Escape, Hex1bKey.F4, Hex1bKey.F12
        ];

        foreach (var k in cancelKeys)
            bindings.Key(k).Action(_ => ResetToIdle(), "");

        // Ctrl+key cancellation
        bindings.Ctrl().Key(Hex1bKey.Backspace).Action(_ => ResetToIdle(), "");
        bindings.Ctrl().Key(Hex1bKey.Delete).Action(_ => ResetToIdle(), "");
        bindings.Ctrl().Key(Hex1bKey.A).Action(_ => ResetToIdle(), "");
        bindings.Ctrl().Key(Hex1bKey.D).Action(_ => ResetToIdle(), "");
        bindings.Ctrl().Key(Hex1bKey.Z).Action(_ => ResetToIdle(), "");
        bindings.Ctrl().Key(Hex1bKey.Y).Action(_ => ResetToIdle(), "");
        bindings.Ctrl().Key(Hex1bKey.Spacebar).Action(_ => ResetToIdle(), "");
        bindings.Ctrl().Key(Hex1bKey.K).Action(_ => ResetToIdle(), "");
        bindings.Shift().Key(Hex1bKey.F12).Action(_ => ResetToIdle(), "");

        // --- Cancellation: navigation keys ---
        Hex1bKey[] navKeys =
        [
            Hex1bKey.LeftArrow, Hex1bKey.RightArrow, Hex1bKey.UpArrow, Hex1bKey.DownArrow,
            Hex1bKey.Home, Hex1bKey.End, Hex1bKey.PageUp, Hex1bKey.PageDown
        ];

        foreach (var k in navKeys)
        {
            bindings.Key(k).Action(_ => ResetToIdle(), "");
            bindings.Shift().Key(k).Action(_ => ResetToIdle(), "");
            bindings.Ctrl().Key(k).Action(_ => ResetToIdle(), "");
        }

        // Note: Ctrl+Shift combos cannot be registered via the fluent builder (Ctrl and Shift
        // are mutually exclusive in hex1b's KeyStepBuilder). These are covered by the Ctrl-only
        // and Shift-only navigation cancellation bindings above — the cursor-affinity check
        // catches any movement regardless.

        // --- Cancellation: mouse bindings ---
        bindings.Mouse(MouseButton.Left).Action(() => ResetToIdle(), "");
        bindings.Mouse(MouseButton.Left).Ctrl().Action(() => ResetToIdle(), "");
        bindings.Mouse(MouseButton.Left).DoubleClick().Action(() => ResetToIdle(), "");
        bindings.Mouse(MouseButton.Left).TripleClick().Action(() => ResetToIdle(), "");
        bindings.Drag(MouseButton.Left).Action((_, _) =>
        {
            ResetToIdle();
            return new DragHandler(); // empty → rejected
        });
    }

    private static void CompleteTextObject(
        InputBindingActionContext ctx,
        EditorState thisEditorState,
        Func<VimMotionState> getVimPending,
        Func<EditorState?> getVimPendingEditor,
        Func<int> getVimPendingCursorOffset,
        Func<DateTime> getVimPendingTimestamp,
        Action<VimMotionState, EditorState?, int> setVimState,
        Action<InputBindingActionContext, EditorNode>? performYank,
        Action invalidate,
        bool isWord)
    {
        void ResetToIdle() => setVimState(VimMotionState.Idle, null, 0);

        // Timeout check
        if (getVimPending() != VimMotionState.Idle
            && (DateTime.UtcNow - getVimPendingTimestamp()).TotalSeconds > 1.0)
        {
            ResetToIdle();
            return;
        }

        var pending = getVimPending();

        // State check: must be waiting for text object completion
        if (pending is not (VimMotionState.WaitingForTextObject or VimMotionState.WaitingForYTextObject))
        {
            ResetToIdle();
            return;
        }

        // Editor affinity
        if (thisEditorState != getVimPendingEditor())
        {
            ResetToIdle();
            return;
        }

        // Cursor affinity
        if (thisEditorState.Cursor.Position.Value != getVimPendingCursorOffset())
        {
            ResetToIdle();
            return;
        }

        // Selection affinity — if something created a selection between I and W, cancel
        if (thisEditorState.Cursor.HasSelection)
        {
            ResetToIdle();
            return;
        }

        // Perform the selection
        if (isWord)
            SelectInnerWord(thisEditorState);
        else
            SelectInnerWORD(thisEditorState);

        // If this was a yiw/yiW sequence, perform the yank
        if (pending == VimMotionState.WaitingForYTextObject
            && performYank is not null
            && ctx.FocusedNode is EditorNode editor)
        {
            performYank(ctx, editor);
        }

        ResetToIdle();
        invalidate();
    }

    /// <summary>
    /// Selects the entire visible content of the line at the cursor position.
    /// Anchor is set to the exclusive end (one past the last visible character)
    /// and Position to the line start, producing a half-open selection
    /// <c>[lineStart, lineStart + len)</c> that the renderer highlights correctly.
    /// <c>PerformEditorYank</c>'s <c>+1</c> adjustment computes
    /// <c>Math.Max(lineStart + len, lineStart + 1) = lineStart + len</c>,
    /// extracting exactly the visible content without the trailing newline.
    /// </summary>
    /// <param name="state">The editor state to modify.</param>
    internal static void SelectLine(EditorState state)
    {
        if (state.Document.Length == 0) return;

        var pos = state.Document.OffsetToPosition(state.Cursor.Position);
        var lineStart = state.Document.PositionToOffset(new DocumentPosition(pos.Line, 1));
        var lineText = state.Document.GetLineText(pos.Line);

        state.Cursors.CollapseToSingle();

        if (lineText.Length == 0)
        {
            // Empty line — just position cursor at line start, no selection
            state.SetCursorPosition(lineStart);
            return;
        }

        // Anchor at exclusive end, Position at start. This makes:
        //   SelectionRange = (lineStart, lineStart + lineText.Length)
        //   HasSelection = true (even for single-char lines)
        //   Renderer highlights [lineStart, lineStart + len) — all visible chars
        //   Yank extracts [lineStart, lineStart + len) — no trailing newline
        state.Cursor.SelectionAnchor = new DocumentOffset(lineStart.Value + lineText.Length);
        state.Cursor.Position = lineStart;
    }

    private enum CharClass { Word, Punctuation, Whitespace, Newline }

    private static CharClass ClassifyChar(char c) => c switch
    {
        '\n' => CharClass.Newline,
        _ when char.IsLetterOrDigit(c) || c == '_' => CharClass.Word,
        _ when char.IsWhiteSpace(c) => CharClass.Whitespace,
        _ => CharClass.Punctuation
    };
}
