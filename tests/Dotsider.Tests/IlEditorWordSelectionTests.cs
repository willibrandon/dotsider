using Dotsider.Views;
using Hex1b.Documents;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Tests for word selection behavior in the IL Inspector editor,
/// verifying that double-click word boundaries match neovim behavior
/// (periods and punctuation are NOT included in word selection) and
/// that Shift+Arrow keyboard selection is not blocked at word boundaries.
/// </summary>
public class IlEditorWordSelectionTests
{
    /// <summary>
    /// Simulates a double-click word selection followed by the one-shot cursor adjustment.
    /// Previous tracking state starts null (first frame after editor creation).
    /// </summary>
    private static EditorState SelectWord(string content, int offset)
    {
        var doc = new Hex1bDocument(content);
        var state = new EditorState(doc) { IsReadOnly = true };

        // Simulate first frame with no selection to seed tracking state.
        DocumentOffset? prevAnchor = null;
        DocumentOffset? prevPosition = null;
        IlInspectorView.AdjustWordSelectionCursorOneShot(state, ref prevAnchor, ref prevPosition);

        // Double-click: SelectWordAt changes both anchor and position.
        state.SelectWordAt(new DocumentOffset(offset));
        IlInspectorView.AdjustWordSelectionCursorOneShot(state, ref prevAnchor, ref prevPosition);

        return state;
    }

    /// <summary>
    /// Verifies select word at dotted name cursor on last word char.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void SelectWordAt_DottedName_CursorOnLastWordChar()
    {
        // "System.Runtime" — double-click on "System" should place cursor on 'm' (offset 5),
        // not on '.' (offset 6).
        var state = SelectWord("System.Runtime", 0);

        Assert.Equal(5, state.Cursor.Position.Value); // 'm' in "System"
        var text = state.Document.GetText();
        Assert.Equal('m', text[state.Cursor.Position.Value]);
    }

    /// <summary>
    /// Verifies select word at dotted name yank copies full word.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void SelectWordAt_DottedName_YankCopiesFullWord()
    {
        // After double-click + one-shot, yank must copy the full word "System"
        // even though cursor is at offset 5 ('m') and SelectionRange is (0,5).
        // The yank logic uses Max(range.End, cursor.Position + 1) to compensate.
        var state = SelectWord("System.Runtime", 0);

        var range = state.Cursor.SelectionRange;
        var yankEnd = new DocumentOffset(Math.Min(
            Math.Max(range.End.Value, state.Cursor.Position.Value + 1),
            state.Document.Length));
        var yankRange = new DocumentRange(range.Start, yankEnd);
        var yanked = state.Document.GetText(yankRange);

        Assert.Equal("System", yanked);
    }

    /// <summary>
    /// Verifies select word at dotted name yank cursor on last char.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void SelectWordAt_DottedName_YankCursorOnLastChar()
    {
        // After yanking "System", cursor should collapse to 'm' (offset 5), not 'e' (offset 4).
        var state = SelectWord("System.Runtime", 0);

        var range = state.Cursor.SelectionRange;
        var yankEnd = new DocumentOffset(Math.Min(
            Math.Max(range.End.Value, state.Cursor.Position.Value + 1),
            state.Document.Length));
        var lastChar = new DocumentOffset(Math.Max(0, yankEnd.Value - 1));

        Assert.Equal(5, lastChar.Value); // 'm' in "System"
        Assert.Equal('m', state.Document.GetText()[lastChar.Value]);
    }

    /// <summary>
    /// Verifies select word at dotted name second segment.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void SelectWordAt_DottedName_SecondSegment()
    {
        var state = SelectWord("System.Runtime", 7);

        var selected = state.Document.GetText(state.Cursor.SelectionRange);
        Assert.DoesNotContain(".", selected);
    }

    /// <summary>
    /// Verifies select word at middle of word.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void SelectWordAt_MiddleOfWord()
    {
        var state = SelectWord("System.Runtime", 3);

        var cursorPos = state.Document.OffsetToPosition(state.Cursor.Position);
        var lineText = state.Document.GetLineText(cursorPos.Line);
        Assert.NotEqual('.', lineText[cursorPos.Column - 1]);
    }

    /// <summary>
    /// Verifies select word at il instruction stops at colon.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void SelectWordAt_IlInstruction_StopsAtColon()
    {
        var state = SelectWord(
            "IL_0004: call System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::.ctor", 9);

        var selected = state.Document.GetText(state.Cursor.SelectionRange);
        Assert.StartsWith("cal", selected);
    }

    /// <summary>
    /// Verifies select word at on period cursor not on period.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void SelectWordAt_OnPeriod_CursorNotOnPeriod()
    {
        var state = SelectWord("System.Runtime", 6);

        var cursorPos = state.Document.OffsetToPosition(state.Cursor.Position);
        var lineText = state.Document.GetLineText(cursorPos.Line);
        Assert.NotEqual('.', lineText[cursorPos.Column - 1]);
    }

    /// <summary>
    /// Verifies select word at cursor never on period after dotted selection.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void SelectWordAt_CursorNeverOnPeriod_AfterDottedSelection()
    {
        var doc = new Hex1bDocument("System.Runtime.CompilerServices");
        var state = new EditorState(doc) { IsReadOnly = true };

        DocumentOffset? prevAnchor = null;
        DocumentOffset? prevPosition = null;

        // Seed tracking state.
        IlInspectorView.AdjustWordSelectionCursorOneShot(state, ref prevAnchor, ref prevPosition);

        int[] offsets = [0, 7, 15]; // S, R, C

        for (var i = 0; i < offsets.Length; i++)
        {
            state.SelectWordAt(new DocumentOffset(offsets[i]));
            IlInspectorView.AdjustWordSelectionCursorOneShot(state, ref prevAnchor, ref prevPosition);

            var pos = state.Cursor.Position;
            if (pos.Value < doc.Length)
            {
                var lineText = doc.GetLineText(doc.OffsetToPosition(pos).Line);
                var col = doc.OffsetToPosition(pos).Column - 1;
                Assert.NotEqual('.', lineText[col]);
            }
        }
    }

    /// <summary>
    /// Verifies shift arrow selection crosses word boundary.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void ShiftArrow_SelectionCrossesWordBoundary()
    {
        // "System.Runtime" — Shift+Arrow from offset 0 should be able to select
        // past the period without being pulled back.
        var doc = new Hex1bDocument("System.Runtime");
        var state = new EditorState(doc) { IsReadOnly = true };

        DocumentOffset? prevAnchor = null;
        DocumentOffset? prevPosition = null;

        // Seed tracking state at offset 0 with no selection.
        state.SetCursorPosition(new DocumentOffset(0));
        IlInspectorView.AdjustWordSelectionCursorOneShot(state, ref prevAnchor, ref prevPosition);

        // Simulate Shift+Right one character at a time across the full string.
        // Shift+Arrow sets anchor once (first press) then only moves position.
        state.Cursor.EnsureSelectionAnchor();
        IlInspectorView.AdjustWordSelectionCursorOneShot(state, ref prevAnchor, ref prevPosition);

        for (var i = 1; i <= 13; i++) // "System.Runtime" is 14 chars, select to offset 13
        {
            // Only position changes — anchor stays at 0.
            state.Cursor.Position = new DocumentOffset(i);
            IlInspectorView.AdjustWordSelectionCursorOneShot(state, ref prevAnchor, ref prevPosition);

            // Cursor must NOT be pulled back — it should stay where we put it.
            Assert.Equal(i, state.Cursor.Position.Value);
        }

        // Final selection should span from 0 to 13, including the period.
        var selected = doc.GetText(state.Cursor.SelectionRange);
        Assert.Equal("System.Runtim", selected);
    }

    /// <summary>
    /// Verifies one shot does not re fire on subsequent frames.
    /// </summary>
    [Fact(Timeout = 30_000)]
    public void OneShot_DoesNotReFireOnSubsequentFrames()
    {
        // After the one-shot fires, repeated calls with the same state should not
        // re-adjust the cursor (it already recorded the adjusted position).
        var doc = new Hex1bDocument("System.Runtime");
        var state = new EditorState(doc) { IsReadOnly = true };

        DocumentOffset? prevAnchor = null;
        DocumentOffset? prevPosition = null;

        // Seed.
        IlInspectorView.AdjustWordSelectionCursorOneShot(state, ref prevAnchor, ref prevPosition);

        // Double-click selects "System".
        state.SelectWordAt(new DocumentOffset(0));
        IlInspectorView.AdjustWordSelectionCursorOneShot(state, ref prevAnchor, ref prevPosition);

        var afterFirst = state.Cursor.Position.Value;
        Assert.Equal(5, afterFirst); // 'm' in "System"

        // Simulate several more render frames with no user input.
        for (var i = 0; i < 5; i++)
        {
            IlInspectorView.AdjustWordSelectionCursorOneShot(state, ref prevAnchor, ref prevPosition);
            Assert.Equal(afterFirst, state.Cursor.Position.Value);
        }
    }
}
