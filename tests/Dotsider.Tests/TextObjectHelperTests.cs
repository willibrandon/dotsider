using Dotsider.Views;
using Hex1b.Documents;
using Hex1b.Widgets;

namespace Dotsider.Tests;

/// <summary>
/// Tests for Text Object Helper.
/// </summary>
[TestClass]
public class TextObjectHelperTests
{
    private static EditorState CreateEditor(string text, int cursorOffset = 0)
    {
        var doc = new Hex1bDocument(text);
        var state = new EditorState(doc) { IsReadOnly = true };
        state.SetCursorPosition(new DocumentOffset(cursorOffset));
        return state;
    }

    private static string? GetSelectedText(EditorState state)
    {
        if (!state.Cursor.HasSelection) return null;
        // Mirror PerformEditorYank: extend to cursor + 1 to include the cursor character
        var range = state.Cursor.SelectionRange;
        var yankEnd = new DocumentOffset(Math.Min(
            Math.Max(range.End.Value, state.Cursor.Position.Value + 1),
            state.Document.Length));
        return state.Document.GetText(new DocumentRange(range.Start, yankEnd));
    }

    // --- SelectInnerWord ---

    /// <summary>
    /// Verifies select inner word on word chars selects contiguous run.
    /// </summary>
    [TestMethod]
    public void SelectInnerWord_OnWordChars_SelectsContiguousRun()
    {
        var state = CreateEditor("hello_world foo", cursorOffset: 3); // cursor on 'l'
        TextObjectHelper.SelectInnerWord(state);
        Assert.AreEqual("hello_world", GetSelectedText(state));
    }

    /// <summary>
    /// Verifies select inner word includes underscores.
    /// </summary>
    [TestMethod]
    public void SelectInnerWord_IncludesUnderscores()
    {
        var state = CreateEditor("foo_bar", cursorOffset: 3); // cursor on '_'
        TextObjectHelper.SelectInnerWord(state);
        Assert.AreEqual("foo_bar", GetSelectedText(state));
    }

    /// <summary>
    /// Verifies select inner word on punctuation selects punctuation run.
    /// </summary>
    [TestMethod]
    public void SelectInnerWord_OnPunctuation_SelectsPunctuationRun()
    {
        var state = CreateEditor("foo::bar", cursorOffset: 3); // cursor on first ':'
        TextObjectHelper.SelectInnerWord(state);
        Assert.AreEqual("::", GetSelectedText(state));
    }

    /// <summary>
    /// Verifies select inner word on whitespace selects whitespace run.
    /// </summary>
    [TestMethod]
    public void SelectInnerWord_OnWhitespace_SelectsWhitespaceRun()
    {
        var state = CreateEditor("a   b", cursorOffset: 2); // cursor on middle space
        TextObjectHelper.SelectInnerWord(state);
        Assert.AreEqual("   ", GetSelectedText(state));
    }

    /// <summary>
    /// Verifies select inner word on tab selects tab run.
    /// </summary>
    [TestMethod]
    public void SelectInnerWord_OnTab_SelectsTabRun()
    {
        var state = CreateEditor("a\t\tb", cursorOffset: 1); // cursor on first tab
        TextObjectHelper.SelectInnerWord(state);
        Assert.AreEqual("\t\t", GetSelectedText(state));
    }

    /// <summary>
    /// Verifies select inner word does not cross newline.
    /// </summary>
    [TestMethod]
    public void SelectInnerWord_DoesNotCrossNewline()
    {
        var state = CreateEditor("abc\ndef", cursorOffset: 2); // cursor on 'c'
        TextObjectHelper.SelectInnerWord(state);
        Assert.AreEqual("abc", GetSelectedText(state));
    }

    /// <summary>
    /// Verifies select inner word on newline no selection.
    /// </summary>
    [TestMethod]
    public void SelectInnerWord_OnNewline_NoSelection()
    {
        // Newlines are word boundaries and single-char tokens.
        // iw on a newline is a no-op — cursor stays put, no selection.
        var state = CreateEditor("abc\ndef", cursorOffset: 3); // cursor on '\n'
        TextObjectHelper.SelectInnerWord(state);
        Assert.IsNull(GetSelectedText(state));
    }

    /// <summary>
    /// Verifies select inner word at document start selects from start.
    /// </summary>
    [TestMethod]
    public void SelectInnerWord_AtDocumentStart_SelectsFromStart()
    {
        var state = CreateEditor("hello world", cursorOffset: 0);
        TextObjectHelper.SelectInnerWord(state);
        Assert.AreEqual("hello", GetSelectedText(state));
    }

    /// <summary>
    /// Verifies select inner word at document end selects to end.
    /// </summary>
    [TestMethod]
    public void SelectInnerWord_AtDocumentEnd_SelectsToEnd()
    {
        var state = CreateEditor("hello world", cursorOffset: 10); // cursor on 'd'
        TextObjectHelper.SelectInnerWord(state);
        Assert.AreEqual("world", GetSelectedText(state));
    }

    /// <summary>
    /// Verifies select inner word empty document no selection.
    /// </summary>
    [TestMethod]
    public void SelectInnerWord_EmptyDocument_NoSelection()
    {
        var state = CreateEditor("");
        TextObjectHelper.SelectInnerWord(state);
        Assert.IsNull(GetSelectedText(state));
    }

    /// <summary>
    /// Verifies select inner word cursor at document length no selection.
    /// </summary>
    [TestMethod]
    public void SelectInnerWord_CursorAtDocumentLength_NoSelection()
    {
        var state = CreateEditor("abc", cursorOffset: 3);
        TextObjectHelper.SelectInnerWord(state);
        Assert.IsNull(GetSelectedText(state));
    }

    // --- SelectInnerWORD ---

    /// <summary>
    /// Verifies select inner word on non whitespace selects to whitespace.
    /// </summary>
    [TestMethod]
    public void SelectInnerWORD_OnNonWhitespace_SelectsToWhitespace()
    {
        var state = CreateEditor("foo::bar baz", cursorOffset: 4); // cursor on ':'
        TextObjectHelper.SelectInnerWORD(state);
        Assert.AreEqual("foo::bar", GetSelectedText(state));
    }

    /// <summary>
    /// Verifies select inner word qualified name selects entire fqn.
    /// </summary>
    [TestMethod]
    public void SelectInnerWORD_QualifiedName_SelectsEntireFQN()
    {
        var state = CreateEditor("System.Runtime.CompilerServices.NullableAttribute", cursorOffset: 10);
        TextObjectHelper.SelectInnerWORD(state);
        Assert.AreEqual("System.Runtime.CompilerServices.NullableAttribute", GetSelectedText(state));
    }

    /// <summary>
    /// Verifies select inner word on whitespace selects whitespace run.
    /// </summary>
    [TestMethod]
    public void SelectInnerWORD_OnWhitespace_SelectsWhitespaceRun()
    {
        var state = CreateEditor("a   b", cursorOffset: 2);
        TextObjectHelper.SelectInnerWORD(state);
        Assert.AreEqual("   ", GetSelectedText(state));
    }

    /// <summary>
    /// Verifies select inner word does not cross newline.
    /// </summary>
    [TestMethod]
    public void SelectInnerWORD_DoesNotCrossNewline()
    {
        var state = CreateEditor("foo::bar\nbaz", cursorOffset: 4);
        TextObjectHelper.SelectInnerWORD(state);
        Assert.AreEqual("foo::bar", GetSelectedText(state));
    }

    /// <summary>
    /// Verifies select inner word single token selects all.
    /// </summary>
    [TestMethod]
    public void SelectInnerWORD_SingleToken_SelectsAll()
    {
        var state = CreateEditor("abc", cursorOffset: 1);
        TextObjectHelper.SelectInnerWORD(state);
        Assert.AreEqual("abc", GetSelectedText(state));
    }

    /// <summary>
    /// Verifies select inner word empty document no selection.
    /// </summary>
    [TestMethod]
    public void SelectInnerWORD_EmptyDocument_NoSelection()
    {
        var state = CreateEditor("");
        TextObjectHelper.SelectInnerWORD(state);
        Assert.IsNull(GetSelectedText(state));
    }
}
