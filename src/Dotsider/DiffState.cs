using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Hex1b;
using Hex1b.Widgets;

namespace Dotsider;

/// <summary>
/// State for diff mode — holds two analyzers and the diff result.
/// </summary>
public sealed class DiffState : IDisposable
{
    /// <summary>
    /// Creates a new diff state comparing two assemblies.
    /// </summary>
    /// <param name="app">The Hex1b application instance.</param>
    /// <param name="leftPath">File path to the left (baseline) assembly.</param>
    /// <param name="rightPath">File path to the right (changed) assembly.</param>
    public DiffState(Hex1bApp app, string leftPath, string rightPath)
    {
        App = app;
        Left = new AssemblyAnalyzer(leftPath);
        Right = new AssemblyAnalyzer(rightPath);
        DiffResult = AssemblyDiffer.Compare(Left, Right);
    }

    /// <summary>
    /// Creates a new diff state comparing two pre-built analyzers.
    /// Used when the analyzers were created via <see cref="AssemblyLoader"/>.
    /// </summary>
    /// <param name="app">The Hex1b application instance.</param>
    /// <param name="left">The analyzer for the left (baseline) assembly.</param>
    /// <param name="right">The analyzer for the right (changed) assembly.</param>
    public DiffState(Hex1bApp app, AssemblyAnalyzer left, AssemblyAnalyzer right)
    {
        App = app;
        Left = left;
        Right = right;
        DiffResult = AssemblyDiffer.Compare(Left, Right);
    }

    /// <summary>The Hex1b application instance.</summary>
    public Hex1bApp App { get; }

    /// <summary>The analyzer for the left (baseline) assembly.</summary>
    public AssemblyAnalyzer Left { get; }

    /// <summary>The analyzer for the right (changed) assembly.</summary>
    public AssemblyAnalyzer Right { get; }

    /// <summary>The computed diff result between the two assemblies.</summary>
    public AssemblyDiffResult DiffResult { get; }

    /// <summary>The currently selected diff tab index.</summary>
    public int CurrentTab { get; set; }

    /// <summary>The focused row key in the current diff table.</summary>
    public object? DiffFocusedKey { get; set; }

    /// <summary>The active filter mode for diff entries.</summary>
    public DiffFilterMode FilterMode { get; set; } = DiffFilterMode.All;

    /// <summary>Per-tab search state for diff views (Summary=0, Types=1, Methods=2, References=3).</summary>
    public SearchState[] Search { get; } = [.. Enumerable.Range(0, 4).Select(_ => new SearchState())];

    /// <summary>Delegate to navigate to the next search match in the current diff view.</summary>
    public Action? NavigateNextMatch { get; set; }

    /// <summary>Delegate to navigate to the previous search match in the current diff view.</summary>
    public Action? NavigatePrevMatch { get; set; }

    // --- Yank State ---

    /// <summary>Yank notification message shown in the hints bar, auto-clears after 1.5 seconds.</summary>
    public string? YankNotification { get; set; }

    /// <summary>Generation counter for yank notification timer race prevention.</summary>
    public long YankGeneration { get; set; }

    /// <summary>Whether the focused table row should flash with yank highlight colors. Auto-clears after 150ms.</summary>
    public bool YankFlashRow { get; set; }

    // --- Vim Text Object State ---

    /// <summary>Current state of a pending vim text-object sequence (iw, iW, yiw, yiW).</summary>
    public VimMotionState VimPending { get; set; }

    /// <summary>The editor that started the current vim text-object sequence, for affinity checking.</summary>
    public EditorState? VimPendingEditor { get; set; }

    /// <summary>Cursor position when the text-object sequence was armed, for cursor affinity.</summary>
    public int VimPendingCursorOffset { get; set; }

    /// <summary>Timestamp when the text-object sequence was armed, for 1-second timeout.</summary>
    public DateTime VimPendingTimestamp { get; set; }

    /// <summary>Delegate to perform a neovim-style editor yank, set by the host app (DiffApp).</summary>
    public Action<Hex1b.Input.InputBindingActionContext, EditorNode>? PerformEditorYank { get; set; }

    // --- Read-Only Editor State (for text selection + yank) ---

    /// <summary>Read-only editor for the left assembly info panel in diff summary.</summary>
    public EditorState? LeftInfoEditorState { get; set; }

    /// <summary>Source text used to build <see cref="LeftInfoEditorState"/>, for staleness detection.</summary>
    public string? LeftInfoEditorText { get; set; }

    /// <summary>Read-only editor for the right assembly info panel in diff summary.</summary>
    public EditorState? RightInfoEditorState { get; set; }

    /// <summary>Source text used to build <see cref="RightInfoEditorState"/>, for staleness detection.</summary>
    public string? RightInfoEditorText { get; set; }

    /// <summary>Read-only editor for the change statistics panel in diff summary.</summary>
    public EditorState? ChangeStatsEditorState { get; set; }

    /// <summary>Source text used to build <see cref="ChangeStatsEditorState"/>, for staleness detection.</summary>
    public string? ChangeStatsEditorText { get; set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        Left.Dispose();
        Right.Dispose();
    }
}
