using Dotsider.Core.Analysis.Models;
using Hex1b.Widgets;

namespace Dotsider.Views;

/// <summary>
/// Maps the native disassembly editor's cursor to the decoded instruction on that line, so
/// go-to-definition can resolve the instruction's call/branch/data target to a symbol. The mapping
/// is structural — by <see cref="NativeInstruction.DisplayLine"/> — never by re-parsing the text.
/// </summary>
internal static class NativeNavigationHelper
{
    /// <summary>Returns the instruction on the editor's current cursor line, or null.</summary>
    /// <param name="editorState">The native disassembly editor state.</param>
    /// <param name="instructions">The decoded instructions, each carrying its display line.</param>
    public static NativeInstruction? GetInstructionAtCursor(
        EditorState editorState, IReadOnlyList<NativeInstruction> instructions)
    {
        var cursorLine = GetCursorLine(editorState);
        return instructions.FirstOrDefault(i => i.DisplayLine == cursorLine);
    }

    private static int GetCursorLine(EditorState editorState) =>
        // Hex1b maps an offset to its 1-based line with an indexed lookup — no full-text scan.
        editorState.Document.OffsetToPosition(editorState.Cursor.Position).Line;
}
