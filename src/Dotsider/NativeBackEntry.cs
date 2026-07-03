using Dotsider.Core.Analysis.Models;

namespace Dotsider;

/// <summary>
/// One entry in the native IL-inspector go-to-definition back stack, capturing the state needed to
/// restore the previous native view on Esc: the symbol that was displayed, the focused tree row, and
/// a snapshot of the tree expansion so the originating function is visible again.
/// </summary>
/// <param name="Symbol">The native symbol that was displayed before navigation.</param>
/// <param name="FocusedTreeKey">The focused row key in the native tree table.</param>
/// <param name="TreeExpansionState">A cloned snapshot of the tree expansion state.</param>
/// <param name="CursorOffset">The editor cursor offset to restore for an intra-function (local-label) jump, or null when the entry navigated to a different symbol.</param>
public sealed record NativeBackEntry(
    NativeSymbol Symbol,
    object? FocusedTreeKey,
    Dictionary<string, bool> TreeExpansionState,
    int? CursorOffset = null);
