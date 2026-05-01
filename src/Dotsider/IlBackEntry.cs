using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Hex1b.Widgets;

namespace Dotsider;

/// <summary>
/// A single entry in the IL go-to-definition back stack, capturing all state
/// needed to fully restore the IL inspector to its previous view on Esc back.
/// </summary>
/// <param name="Method">The method that was selected before navigation.</param>
/// <param name="EditorState">The editor state instance (document + cursor) for scroll/cursor preservation.</param>
/// <param name="EditorMethod">The method loaded in the editor (for BuildEditorPane staleness check).</param>
/// <param name="EditorAnalyzer">The analyzer that built the editor content (for reload detection).</param>
/// <param name="FocusedTreeKey">The focused row key in the IL tree table.</param>
/// <param name="TreeExpansionState">Cloned snapshot of the tree expansion state.</param>
/// <param name="CrossAssembly">Whether PushAssembly was called for this navigation (requires PopAssembly on back).</param>
/// <param name="EditorKey">The editor identity key for StatePanelWidget matching on back-nav.</param>
/// <param name="PreviousCrossViewBackTarget">Snapshot of <c>CrossViewBackTarget</c> taken before the push, so cross-assembly back can restore the originating tab (e.g. Size Map) after PopAssembly clears it.</param>
public sealed record IlBackEntry(
    MethodDefInfo Method,
    EditorState EditorState,
    MethodDefInfo EditorMethod,
    AssemblyAnalyzer EditorAnalyzer,
    object? FocusedTreeKey,
    Dictionary<string, bool> TreeExpansionState,
    bool CrossAssembly,
    object? EditorKey,
    (int Tab, int SubTab)? PreviousCrossViewBackTarget);
