using Dotsider.Core.Analysis.Models;
using Hex1b.Widgets;

namespace Dotsider;

/// <summary>
/// One entry in the native IL-inspector go-to-definition back stack, capturing everything needed to
/// restore the previous native view on Esc — modelled on the managed <see cref="IlBackEntry"/> so
/// cursor and scroll survive the round-trip. The editor instance is preserved directly (rather than
/// rebuilt) so returning lands on the exact line and offset the jump departed from.
/// </summary>
/// <param name="Symbol">The native symbol that was displayed before navigation.</param>
/// <param name="EditorState">The editor instance (document + cursor + scroll) for the displayed symbol.</param>
/// <param name="EditorKey">The editor identity key for StatePanelWidget matching on back-nav.</param>
/// <param name="Instructions">The decoded instructions of the displayed symbol, for the decoration providers.</param>
/// <param name="HeaderLineCount">The header line count of the displayed disassembly.</param>
/// <param name="FocusedTreeKey">The focused row key in the native tree table.</param>
/// <param name="TreeExpansionState">A cloned snapshot of the tree expansion state.</param>
/// <param name="CursorOffset">The editor cursor offset at the moment of navigation, restored on Esc.</param>
public sealed record NativeBackEntry(
    NativeSymbol Symbol,
    EditorState EditorState,
    object? EditorKey,
    IReadOnlyList<NativeInstruction>? Instructions,
    int HeaderLineCount,
    object? FocusedTreeKey,
    Dictionary<string, bool> TreeExpansionState,
    int CursorOffset);
