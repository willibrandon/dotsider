using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Hex1b;
using Hex1b.Widgets;

namespace Dotsider;

/// <summary>
/// State for size-diff mode — two resolved mstat inputs, their computed size difference, and
/// the treemap navigation, filter, and popup state. Unlike <see cref="DiffState"/> there are
/// no managed analyzers at the core of this mode: the inputs may be bare <c>.mstat</c> files;
/// per-side analyzers exist lazily only when a binary backs a side (native disassembly).
/// </summary>
/// <remarks>
/// Creates a size-diff state comparing two resolved mstat inputs.
/// </remarks>
/// <param name="app">The Hex1b application instance.</param>
/// <param name="left">The baseline input.</param>
/// <param name="right">The input under comparison.</param>
public sealed class SizeDiffState(Hex1bApp app, MstatSource left, MstatSource right) : IDisposable
{
    private DgmlGraph? _leftDgml;
    private bool _leftDgmlProbed;
    private DgmlGraph? _rightDgml;
    private bool _rightDgmlProbed;
    private AssemblyAnalyzer? _leftAnalyzer;
    private bool _leftAnalyzerProbed;
    private AssemblyAnalyzer? _rightAnalyzer;
    private bool _rightAnalyzerProbed;

    /// <summary>The Hex1b application instance.</summary>
    public Hex1bApp App { get; } = app;

    /// <summary>The baseline input.</summary>
    public MstatSource LeftSource { get; } = left;

    /// <summary>The input under comparison.</summary>
    public MstatSource RightSource { get; } = right;

    /// <summary>The computed size difference.</summary>
    public MstatDiffResult Diff { get; } = MstatDiffer.Compare(left.Data, right.Data);

    /// <summary>The baseline's display name — the binary when one backs the side, else the mstat file.</summary>
    public string LeftName => Path.GetFileName(LeftSource.BinaryPath ?? LeftSource.MstatPath);

    /// <summary>The comparison side's display name.</summary>
    public string RightName => Path.GetFileName(RightSource.BinaryPath ?? RightSource.MstatPath);

    /// <summary>The currently selected tab index (0 = Summary, 1 = Size Map).</summary>
    public int CurrentTab { get; set; } = 1;

    /// <summary>The active direction filter for the treemap.</summary>
    public SizeDiffFilterMode FilterMode { get; set; } = SizeDiffFilterMode.All;

    /// <summary>Per-tab search state (Summary=0, Size Map=1).</summary>
    public SearchState[] Search { get; } = [.. Enumerable.Range(0, 2).Select(_ => new SearchState())];

    /// <summary>Delegate to navigate to the next search match in the current view.</summary>
    public Action? NavigateNextMatch { get; set; }

    /// <summary>Delegate to navigate to the previous search match in the current view.</summary>
    public Action? NavigatePrevMatch { get; set; }

    // --- Treemap navigation state ---

    /// <summary>The filtered delta tree the treemap is rendering, rebuilt when the filter changes.</summary>
    public SizeDiffNode? FilteredRoot { get; set; }

    /// <summary>The filter <see cref="FilteredRoot"/> was built for, for staleness detection.</summary>
    public SizeDiffFilterMode? FilteredRootMode { get; set; }

    /// <summary>The node whose children the treemap is showing, or null for the filtered root.</summary>
    public SizeDiffNode? TreemapCurrentLevel { get; set; }

    /// <summary>The drill-down trail above <see cref="TreemapCurrentLevel"/>.</summary>
    public Stack<SizeDiffNode> TreemapBreadcrumb { get; } = new();

    /// <summary>The keyboard-selected child index at the current level, or -1.</summary>
    public int TreemapSelectedIndex { get; set; } = -1;

    /// <summary>The active search-match index at the current level, or -1.</summary>
    public int TreemapMatchIndex { get; set; } = -1;

    /// <summary>The node under the mouse, if any.</summary>
    public SizeDiffNode? TreemapHoveredNode { get; set; }

    /// <summary>The detail-bar text for the hovered node, if any.</summary>
    public string? TreemapHoveredItem { get; set; }

    // --- Why-chain popup state ---

    /// <summary>The why-chain popup content, or null when the popup is closed.</summary>
    public string? WhyContent { get; set; }

    /// <summary>Source text used to build <see cref="WhyEditorState"/>, for staleness detection.</summary>
    public string? WhyEditorText { get; set; }

    /// <summary>Read-only editor for the why-chain popup.</summary>
    public EditorState? WhyEditorState { get; set; }

    /// <summary>The node the why popup is explaining, for side-toggle affinity.</summary>
    public SizeDiffNode? WhyTarget { get; set; }

    /// <summary>Whether the why popup is showing the baseline (left) side's chains.</summary>
    public bool WhyShowingLeft { get; set; }

    // --- Native-disassembly popup state ---

    /// <summary>The disassembly popup content, or null when the popup is closed.</summary>
    public string? DisasmContent { get; set; }

    /// <summary>Source text used to build <see cref="DisasmEditorState"/>, for staleness detection.</summary>
    public string? DisasmEditorText { get; set; }

    /// <summary>Read-only editor for the disassembly popup.</summary>
    public EditorState? DisasmEditorState { get; set; }

    /// <summary>The node the disassembly popup is showing, for cycle affinity.</summary>
    public SizeDiffNode? DisasmTarget { get; set; }

    /// <summary>The index into the target's node names the popup is showing (aggregates cycle).</summary>
    public int DisasmSymbolIndex { get; set; }

    // --- Vim / yank state (same contract as DiffState) ---

    /// <summary>Current state of a pending vim text-object sequence (iw, iW, yiw, yiW).</summary>
    public VimMotionState VimPending { get; set; }

    /// <summary>The editor that started the current vim text-object sequence, for affinity checking.</summary>
    public EditorState? VimPendingEditor { get; set; }

    /// <summary>Cursor position when the text-object sequence was armed, for cursor affinity.</summary>
    public int VimPendingCursorOffset { get; set; }

    /// <summary>Timestamp of the latest text-object state transition.</summary>
    public DateTime VimPendingTimestamp { get; set; }

    /// <summary>Delegate to perform a neovim-style editor yank, set by the host app.</summary>
    public Action<Hex1b.Input.InputBindingActionContext, EditorNode>? PerformEditorYank { get; set; }

    /// <summary>Yank notification message shown in the hints bar, auto-clears after 1.5 seconds.</summary>
    public string? YankNotification { get; set; }

    /// <summary>Generation counter for yank notification timer race prevention.</summary>
    public long YankGeneration { get; set; }

    // --- Summary editor state ---

    /// <summary>Whether the one-time initial content focus has been requested.</summary>
    internal bool InitialFocusRequested { get; set; }

    /// <summary>
    /// Requests that the current tab's primary content receives focus after the next render —
    /// the treemap's interactable surface, or the summary's read-only editor — so its
    /// non-global key bindings (arrows, Enter, w, d) work immediately.
    /// </summary>
    public void RequestContentFocus()
    {
        if (CurrentTab == 1)
            App.RequestFocus(node => node is Hex1b.Nodes.InteractableNode);
        else
            App.RequestFocus(node => node is EditorNode);
    }

    /// <summary>Read-only editor for the summary tab.</summary>
    public EditorState? SummaryEditorState { get; set; }

    /// <summary>Source text used to build <see cref="SummaryEditorState"/>, for staleness detection.</summary>
    public string? SummaryEditorText { get; set; }

    /// <summary>
    /// The baseline side's dependency graph, read lazily from the input's DGML sidecar, or
    /// null when none sits beside it.
    /// </summary>
    public DgmlGraph? LeftDgml
    {
        get
        {
            if (!_leftDgmlProbed)
            {
                _leftDgml = LeftSource.DgmlPath is { } path ? DgmlReader.Read(path) : null;
                _leftDgmlProbed = true;
            }

            return _leftDgml;
        }
    }

    /// <summary>The comparison side's dependency graph, or null when none sits beside it.</summary>
    public DgmlGraph? RightDgml
    {
        get
        {
            if (!_rightDgmlProbed)
            {
                _rightDgml = RightSource.DgmlPath is { } path ? DgmlReader.Read(path) : null;
                _rightDgmlProbed = true;
            }

            return _rightDgml;
        }
    }

    /// <summary>
    /// The baseline side's binary analyzer for native disassembly, opened lazily, or null
    /// when the side is a bare <c>.mstat</c> or the binary cannot be opened.
    /// </summary>
    public AssemblyAnalyzer? LeftAnalyzer
    {
        get
        {
            if (!_leftAnalyzerProbed)
            {
                _leftAnalyzer = TryOpen(LeftSource.BinaryPath);
                _leftAnalyzerProbed = true;
            }

            return _leftAnalyzer;
        }
    }

    /// <summary>The comparison side's binary analyzer, or null when the side is a bare <c>.mstat</c>.</summary>
    public AssemblyAnalyzer? RightAnalyzer
    {
        get
        {
            if (!_rightAnalyzerProbed)
            {
                _rightAnalyzer = TryOpen(RightSource.BinaryPath);
                _rightAnalyzerProbed = true;
            }

            return _rightAnalyzer;
        }
    }

    private static AssemblyAnalyzer? TryOpen(string? binaryPath)
    {
        if (binaryPath is null) return null;
        try
        {
            return new AssemblyAnalyzer(binaryPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or BadImageFormatException)
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _leftAnalyzer?.Dispose();
        _rightAnalyzer?.Dispose();
    }
}
