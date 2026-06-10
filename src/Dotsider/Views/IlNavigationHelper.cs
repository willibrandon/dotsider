using Dotsider.Core.Analysis.Models;
using Hex1b.Documents;
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
        var cursorLine = GetCursorLine(editorState);

        var instruction = instructions.FirstOrDefault(i => i.DisplayLine == cursorLine);
        if (instruction is not null)
            return instruction;

        var lineText = editorState.Document.GetLineText(cursorLine);
        if (!lineText.StartsWith("IL_", StringComparison.Ordinal))
            return null;

        var instructionIndex = cursorLine - headerLineCount - 1;
        if (instructionIndex >= 0 && instructionIndex < instructions.Count)
            return instructions[instructionIndex];
        return null;
    }

    /// <summary>
    /// Returns the resolved Source Link URL represented by the source comment line
    /// at the current cursor position, or null.
    /// </summary>
    /// <param name="editorState">The IL editor state containing cursor position.</param>
    /// <param name="instructions">The instruction list for the current method.</param>
    /// <returns>The resolved Source Link URL for the source-span line, or null.</returns>
    public static string? GetSourceLinkUrlAtCursor(
        EditorState editorState,
        IReadOnlyList<IlInstruction> instructions)
    {
        var cursorLine = GetCursorLine(editorState);
        var lineText = editorState.Document.GetLineText(cursorLine);
        if (!lineText.Contains(IlSourceLinkDecorationProvider.SourceLinkMarker, StringComparison.Ordinal))
            return null;

        var instruction = instructions.FirstOrDefault(i => i.DisplayLine == cursorLine + 1);
        if (instruction?.SequenceStartLine is null
            || string.IsNullOrWhiteSpace(instruction.SourceLinkUrl))
            return null;

        return instruction.SourceLinkUrl;
    }

    /// <summary>
    /// Returns the rendered marker range for a Source Link source comment at the cursor, or null.
    /// </summary>
    /// <param name="editorState">The IL editor state containing cursor position.</param>
    /// <param name="instructions">The instruction list for the current method.</param>
    /// <returns>The rendered marker range for the Source Link marker, or null.</returns>
    public static (DocumentPosition Start, DocumentPosition End)? GetSourceLinkMarkerRangeAtCursor(
        EditorState editorState,
        IReadOnlyList<IlInstruction> instructions)
    {
        var cursorLine = GetCursorLine(editorState);
        var lineText = editorState.Document.GetLineText(cursorLine);
        var markerStart = lineText.IndexOf(
            IlSourceLinkDecorationProvider.SourceLinkMarker,
            StringComparison.Ordinal);
        if (markerStart < 0
            || GetSourceLinkUrlAtCursor(editorState, instructions) is null)
            return null;

        return (
            new DocumentPosition(cursorLine, markerStart + 1),
            new DocumentPosition(
                cursorLine,
                markerStart + IlSourceLinkDecorationProvider.SourceLinkMarker.Length + 1));
    }

    private static int GetCursorLine(EditorState editorState)
    {
        var cursorOffset = editorState.Cursor.Position.Value;
        var text = editorState.Document.GetText();
        var cursorLine = 1;
        for (var i = 0; i < cursorOffset && i < text.Length; i++)
        {
            if (text[i] == '\n') cursorLine++;
        }

        return cursorLine;
    }
}
