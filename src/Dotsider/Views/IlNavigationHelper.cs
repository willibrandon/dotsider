using Dotsider.Core.Analysis.Models;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Maps cursor position in the IL editor to the corresponding IL instruction.
/// </summary>
public static class IlNavigationHelper
{
    /// <summary>
    /// Returns the IL instruction at the current cursor line, or null.
    /// </summary>
    /// <param name="editorState">The IL editor state containing cursor position.</param>
    /// <param name="instructions">The instruction list for the current method.</param>
    /// <param name="headerLineCount">The number of header lines before instructions start.</param>
    /// <returns>The instruction at the cursor, or null.</returns>
    public static IlInstruction? GetInstructionAtCursor(
        EditorState editorState,
        IReadOnlyList<IlInstruction> instructions,
        int headerLineCount)
    {
        var cursorOffset = editorState.Cursor.Position.Value;
        var text = editorState.Document.GetText();
        var cursorLine = 1;
        for (var i = 0; i < cursorOffset && i < text.Length; i++)
        {
            if (text[i] == '\n') cursorLine++;
        }

        var instructionIndex = cursorLine - headerLineCount - 1;
        if (instructionIndex >= 0 && instructionIndex < instructions.Count)
            return instructions[instructionIndex];
        return null;
    }
}
