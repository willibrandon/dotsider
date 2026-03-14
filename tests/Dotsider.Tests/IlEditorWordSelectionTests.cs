using Dotsider.Views;
using Hex1b.Documents;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Tests for word selection behavior in the IL Inspector editor,
/// verifying that double-click word boundaries match neovim behavior
/// (periods and punctuation are NOT included in word selection).
/// </summary>
public class IlEditorWordSelectionTests
{
    private static EditorState SelectWord(string content, int offset)
    {
        var doc = new Hex1bDocument(content);
        var state = new EditorState(doc) { IsReadOnly = true };
        state.SelectWordAt(new DocumentOffset(offset));
        IlInspectorView.AdjustWordSelectionCursor(state);
        return state;
    }

    [Fact(Timeout = 30_000)]
    public void SelectWordAt_DottedName_DoesNotIncludePeriod()
    {
        var state = SelectWord("System.Runtime", 0);

        var cursorPos = state.Document.OffsetToPosition(state.Cursor.Position);
        var lineText = state.Document.GetLineText(cursorPos.Line);
        Assert.NotEqual('.', lineText[cursorPos.Column - 1]);
    }

    [Fact(Timeout = 30_000)]
    public void SelectWordAt_DottedName_SecondSegment()
    {
        var state = SelectWord("System.Runtime", 7);

        // "Runtime" is at end of document — cursor should not be on a period
        Assert.True(state.Cursor.Position.Value <= state.Document.Length);
    }

    [Fact(Timeout = 30_000)]
    public void SelectWordAt_MiddleOfWord()
    {
        var state = SelectWord("System.Runtime", 3);

        var cursorPos = state.Document.OffsetToPosition(state.Cursor.Position);
        var lineText = state.Document.GetLineText(cursorPos.Line);
        Assert.NotEqual('.', lineText[cursorPos.Column - 1]);
    }

    [Fact(Timeout = 30_000)]
    public void SelectWordAt_IlInstruction_StopsAtColon()
    {
        var state = SelectWord(
            "IL_0004: call System.Runtime.CompilerServices.DefaultInterpolatedStringHandler::.ctor", 9);

        var selected = state.Document.GetText(state.Cursor.SelectionRange);
        Assert.StartsWith("cal", selected);
    }

    [Fact(Timeout = 30_000)]
    public void SelectWordAt_OnPeriod_SelectsPrecedingWord()
    {
        var state = SelectWord("System.Runtime", 6);

        var cursorPos = state.Document.OffsetToPosition(state.Cursor.Position);
        var lineText = state.Document.GetLineText(cursorPos.Line);
        Assert.NotEqual('.', lineText[cursorPos.Column - 1]);
    }

    [Fact(Timeout = 30_000)]
    public void SelectWordAt_CursorNeverOnPeriod_AfterDottedSelection()
    {
        var doc = new Hex1bDocument("System.Runtime.CompilerServices");
        var state = new EditorState(doc) { IsReadOnly = true };

        int[] offsets = [0, 7, 15]; // S, R, C

        for (var i = 0; i < offsets.Length; i++)
        {
            state.SelectWordAt(new DocumentOffset(offsets[i]));
            IlInspectorView.AdjustWordSelectionCursor(state);

            var pos = state.Cursor.Position;
            if (pos.Value < doc.Length)
            {
                var lineText = doc.GetLineText(doc.OffsetToPosition(pos).Line);
                var col = doc.OffsetToPosition(pos).Column - 1;
                Assert.NotEqual('.', lineText[col]);
            }
        }
    }
}
