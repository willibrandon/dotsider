using System.Collections.Concurrent;
using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Views;
using Hex1b;
using Hex1b.Documents;
using Hex1b.Nodes;
using Hex1b.Widgets;

namespace Dotsider;

/// <summary>
/// Holds all mutable UI state for the dotsider application.
/// Rebuilt each frame by the Hex1b render loop.
/// </summary>
public sealed class DotsiderState : IDisposable
{
    /// <summary>
    /// Creates a new application state for the specified assembly file.
    /// </summary>
    public DotsiderState(Hex1bApp app, string filePath,
        ConcurrentQueue<Action<DotsiderState>>? pendingMutations = null)
    {
        App = app;
        PendingMutations = pendingMutations ?? new();
        Analyzer = new AssemblyAnalyzer(filePath);
        StringExtractor = new StringExtractor(Analyzer);
        if (Analyzer.HasMetadata)
            IlDisassembler = new IlDisassembler(Analyzer);

        var hexDoc = new HexRowDocument(new Hex1bDocument(Analyzer.RawBytes.ToArray()));
        HexRowDoc = hexDoc;
        HexEditorState = new EditorState(hexDoc) { IsReadOnly = true };
        HexCleanVersion = hexDoc.Version;

        // Detect apphost .exe with a companion managed .dll
        if (!Analyzer.HasMetadata)
        {
            var companionDll = ApphostDetector.FindCompanionDll(filePath);
            if (companionDll is not null)
            {
                ApphostCompanionDllPath = companionDll;
                ApphostDialogOpen = true;
            }
        }
    }

    /// <summary>
    /// Creates a new application state wrapping an existing analyzer (used by NuGet mode).
    /// </summary>
    public DotsiderState(Hex1bApp app, AssemblyAnalyzer analyzer)
    {
        App = app;
        PendingMutations = new();
        Analyzer = analyzer;
        StringExtractor = new StringExtractor(Analyzer);
        if (Analyzer.HasMetadata)
            IlDisassembler = new IlDisassembler(Analyzer);
        var hexDoc = new HexRowDocument(new Hex1bDocument(Analyzer.RawBytes.ToArray()));
        HexRowDoc = hexDoc;
        HexEditorState = new EditorState(hexDoc) { IsReadOnly = true };
        HexCleanVersion = hexDoc.Version;
    }

    /// <summary>The Hex1b application instance.</summary>
    public Hex1bApp App { get; }

    /// <summary>
    /// Queue of mutations to apply on the UI thread, drained at the top of each render frame.
    /// Used by the diagnostics socket listener for thread-safe state changes.
    /// </summary>
    public ConcurrentQueue<Action<DotsiderState>> PendingMutations { get; }

    /// <summary>The core assembly analyzer (current top of navigation stack).</summary>
    public AssemblyAnalyzer Analyzer { get; internal set; }

    /// <summary>The IL disassembler for method body inspection. Null for NativeAOT binaries.</summary>
    public IlDisassembler? IlDisassembler { get; internal set; }

    /// <summary>The string extractor for all string sources.</summary>
    public StringExtractor StringExtractor { get; internal set; }

    // --- Tab Navigation ---

    /// <summary>The currently selected main tab index.</summary>
    public int CurrentTab { get; set; }

    // --- General Tab State ---

    /// <summary>The focused assembly reference key in the dependency table.</summary>
    public object? GeneralFocusedDep { get; set; }

    /// <summary>Navigation stack of assembly paths for drill-down.</summary>
    public Stack<AssemblyAnalyzer> NavigationStack { get; } = new();

    /// <summary>Saved focused dep keys matching the navigation stack for restore on back.</summary>
    private readonly Stack<object?> _focusedDepStack = new();

    /// <summary>Saved tab IDs matching the navigation stack for restore on back.</summary>
    private readonly Stack<int> _tabStack = new();

    /// <summary>Saved graph selection indices matching the navigation stack for restore on back.</summary>
    private readonly Stack<int> _graphSelectionStack = new();

    /// <summary>Maximum navigation depth for assembly drill-down.</summary>
    public const int MaxNavigationDepth = 10;

    /// <summary>Error message from the last failed navigation attempt, or null.</summary>
    public string? NavigationError { get; set; }

    // --- Search State (shared across all tabs) ---

    /// <summary>Per-tab search state, indexed by <see cref="TabId"/> constants.</summary>
    public SearchState[] Search { get; } = [.. Enumerable.Range(0, 8).Select(_ => new SearchState())];

    /// <summary>Delegate to navigate to the next search match in the current view.</summary>
    public Action? NavigateNextMatch { get; set; }

    /// <summary>Delegate to navigate to the previous search match in the current view.</summary>
    public Action? NavigatePrevMatch { get; set; }

    // --- PE/Metadata Tab State ---

    /// <summary>The selected sub-tab index in the PE/Metadata view (Sections, TypeDef, etc.).</summary>
    public int PeSubTab { get; set; }

    /// <summary>Whether to display sizes in human-readable format.</summary>
    public bool HumanReadableSizes { get; set; } = true;

    /// <summary>The item being shown in the detail popup, or null.</summary>
    public string? PeDetailContent { get; set; }

    /// <summary>The focused row key in the current PE metadata table.</summary>
    public object? PeFocusedKey { get; set; }

    // --- IL Inspector Tab State ---

    /// <summary>The row key of the focused item in the IL tree table, or null.</summary>
    public object? IlFocusedTreeKey { get; set; }

    /// <summary>The currently selected method for disassembly, or null.</summary>
    public MethodDefInfo? IlSelectedMethod { get; set; }

    /// <summary>
    /// Expansion state map for IL Inspector tree nodes (keyed by stable namespace/type keys).
    /// </summary>
    #pragma warning disable IDE0028
    public Dictionary<string, bool> IlTreeExpansionState { get; } = new(StringComparer.Ordinal);
    #pragma warning restore IDE0028

    /// <summary>The editor state for the IL disassembly pane, or null if no method is selected.</summary>
    public EditorState? IlEditorState { get; set; }

    /// <summary>Tracks the previous frame's selection anchor to detect double-click word selection (both anchor and position change in one frame).</summary>
    internal DocumentOffset? IlPrevSelectionAnchor;

    /// <summary>Tracks the previous frame's cursor position to detect double-click word selection.</summary>
    internal DocumentOffset? IlPrevCursorPosition;

    /// <summary>The method currently loaded in the IL editor, used to detect method changes.</summary>
    public MethodDefInfo? IlEditorMethod { get; set; }

    /// <summary>The analyzer instance that built the current IL editor content, used to detect analyzer reloads.</summary>
    public AssemblyAnalyzer? IlEditorAnalyzer { get; set; }

    /// <summary>Syntax highlighting decoration provider for the IL editor.</summary>
    public IlSyntaxDecorationProvider IlSyntaxProvider { get; } = new();

    /// <summary>Search match highlighting decoration provider for the IL editor.</summary>
    public IlSearchDecorationProvider IlSearchProvider { get; } = new();

    /// <summary>Yank flash decoration provider for the IL editor.</summary>
    public IlYankDecorationProvider IlYankProvider { get; } = new();

    /// <summary>All text-level search matches across method disassemblies, computed on search confirm.</summary>
    public List<IlMatch> IlSearchMatches { get; set; } = [];

    /// <summary>Index into <see cref="IlSearchMatches"/> for the currently highlighted match, or -1.</summary>
    public int IlCurrentMatchIndex { get; set; } = -1;

    /// <summary>Last confirmed search query, used to avoid recomputing matches.</summary>
    public string? IlLastSearchQuery { get; set; }

    /// <summary>Pending cursor match to apply on next frame (set by NavigateToMatch, consumed by BuildEditorPane).</summary>
    public IlMatch? IlPendingCursorMatch { get; set; }

    /// <summary>Method tokens whose IL text matches the confirmed search query. Used to broaden tree filtering.</summary>
    public HashSet<int>? IlTextMatchMethodTokens { get; set; }

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

    /// <summary>Delegate to perform a neovim-style editor yank, set by the host app (DotsiderApp/NuGetApp).</summary>
    public Action<Hex1b.Input.InputBindingActionContext, EditorNode>? PerformEditorYank { get; set; }

    // --- Read-Only Editor State (for text selection + yank) ---

    /// <summary>Read-only editor for the General tab Assembly Info panel.</summary>
    public EditorState? GeneralInfoEditorState { get; set; }

    /// <summary>Source text used to build <see cref="GeneralInfoEditorState"/>, for staleness detection.</summary>
    public string? GeneralInfoEditorText { get; set; }

    /// <summary>Yank flash decoration provider for the General tab Assembly Info editor.</summary>
    public IlYankDecorationProvider GeneralInfoYankProvider { get; } = new();

    /// <summary>Tracks the previous frame's selection anchor for double-click word boundary adjustment in the General info editor.</summary>
    internal DocumentOffset? GeneralInfoPrevSelectionAnchor;

    /// <summary>Tracks the previous frame's cursor position for double-click word boundary adjustment in the General info editor.</summary>
    internal DocumentOffset? GeneralInfoPrevCursorPosition;

    /// <summary>Read-only editor for the PE Headers panel.</summary>
    public EditorState? PeHeadersEditorState { get; set; }

    /// <summary>Source text used to build <see cref="PeHeadersEditorState"/>, for staleness detection.</summary>
    public string? PeHeadersEditorText { get; set; }

    /// <summary>Yank flash decoration provider for the PE Headers editor.</summary>
    public IlYankDecorationProvider PeHeadersYankProvider { get; } = new();

    /// <summary>Tracks the previous frame's selection anchor for word boundary adjustment in the PE Headers editor.</summary>
    internal DocumentOffset? PeHeadersPrevSelectionAnchor;

    /// <summary>Tracks the previous frame's cursor position for word boundary adjustment in the PE Headers editor.</summary>
    internal DocumentOffset? PeHeadersPrevCursorPosition;

    /// <summary>Read-only editor for the CLR Header panel.</summary>
    public EditorState? ClrHeaderEditorState { get; set; }

    /// <summary>Source text used to build <see cref="ClrHeaderEditorState"/>, for staleness detection.</summary>
    public string? ClrHeaderEditorText { get; set; }

    /// <summary>Yank flash decoration provider for the CLR Header editor.</summary>
    public IlYankDecorationProvider ClrHeaderYankProvider { get; } = new();

    /// <summary>Tracks the previous frame's selection anchor for word boundary adjustment in the CLR Header editor.</summary>
    internal DocumentOffset? ClrHeaderPrevSelectionAnchor;

    /// <summary>Tracks the previous frame's cursor position for word boundary adjustment in the CLR Header editor.</summary>
    internal DocumentOffset? ClrHeaderPrevCursorPosition;

    /// <summary>Read-only editor for the Data Interpretation panel on the Hex Dump tab.</summary>
    public EditorState? DataInterpEditorState { get; set; }

    /// <summary>Source text used to build <see cref="DataInterpEditorState"/>, for staleness detection.</summary>
    public string? DataInterpEditorText { get; set; }

    /// <summary>Yank flash decoration provider for the Data Interpretation editor.</summary>
    public IlYankDecorationProvider DataInterpYankProvider { get; } = new();

    /// <summary>Tracks the previous frame's selection anchor for word boundary adjustment in the Data Interpretation editor.</summary>
    internal DocumentOffset? DataInterpPrevSelectionAnchor;

    /// <summary>Tracks the previous frame's cursor position for word boundary adjustment in the Data Interpretation editor.</summary>
    internal DocumentOffset? DataInterpPrevCursorPosition;

    /// <summary>Read-only editor for the PE detail popup overlay.</summary>
    public EditorState? PeDetailEditorState { get; set; }

    /// <summary>Source text used to build <see cref="PeDetailEditorState"/>, for staleness detection.</summary>
    public string? PeDetailEditorText { get; set; }

    /// <summary>Yank flash decoration provider for the PE detail popup editor.</summary>
    public IlYankDecorationProvider PeDetailYankProvider { get; } = new();

    /// <summary>Read-only editor for the Strings detail popup overlay.</summary>
    public EditorState? StringsDetailEditorState { get; set; }

    /// <summary>Source text used to build <see cref="StringsDetailEditorState"/>, for staleness detection.</summary>
    public string? StringsDetailEditorText { get; set; }

    /// <summary>Yank flash decoration provider for the Strings detail popup editor.</summary>
    public IlYankDecorationProvider StringsDetailYankProvider { get; } = new();

    // --- Dynamic Readonly Editors ---

    /// <summary>Read-only editor for the Dynamic Counters CPU section.</summary>
    public EditorState? DynamicCpuEditorState { get; set; }

    /// <summary>Source text used to build <see cref="DynamicCpuEditorState"/>, for staleness detection.</summary>
    public string? DynamicCpuEditorText { get; set; }

    /// <summary>Yank flash decoration provider for the Dynamic Counters CPU editor.</summary>
    public IlYankDecorationProvider DynamicCpuYankProvider { get; } = new();

    /// <summary>Tracks the previous frame's selection anchor for word boundary adjustment in the Dynamic CPU editor.</summary>
    internal DocumentOffset? DynamicCpuPrevSelectionAnchor;

    /// <summary>Tracks the previous frame's cursor position for word boundary adjustment in the Dynamic CPU editor.</summary>
    internal DocumentOffset? DynamicCpuPrevCursorPosition;

    /// <summary>Read-only editor for the Dynamic Counters Memory section.</summary>
    public EditorState? DynamicMemoryEditorState { get; set; }

    /// <summary>Source text used to build <see cref="DynamicMemoryEditorState"/>, for staleness detection.</summary>
    public string? DynamicMemoryEditorText { get; set; }

    /// <summary>Yank flash decoration provider for the Dynamic Counters Memory editor.</summary>
    public IlYankDecorationProvider DynamicMemoryYankProvider { get; } = new();

    /// <summary>Tracks the previous frame's selection anchor for word boundary adjustment in the Dynamic Memory editor.</summary>
    internal DocumentOffset? DynamicMemoryPrevSelectionAnchor;

    /// <summary>Tracks the previous frame's cursor position for word boundary adjustment in the Dynamic Memory editor.</summary>
    internal DocumentOffset? DynamicMemoryPrevCursorPosition;

    /// <summary>Read-only editor for the Dynamic Counters GC Collections section.</summary>
    public EditorState? DynamicGcEditorState { get; set; }

    /// <summary>Source text used to build <see cref="DynamicGcEditorState"/>, for staleness detection.</summary>
    public string? DynamicGcEditorText { get; set; }

    /// <summary>Yank flash decoration provider for the Dynamic Counters GC editor.</summary>
    public IlYankDecorationProvider DynamicGcYankProvider { get; } = new();

    /// <summary>Tracks the previous frame's selection anchor for word boundary adjustment in the Dynamic GC editor.</summary>
    internal DocumentOffset? DynamicGcPrevSelectionAnchor;

    /// <summary>Tracks the previous frame's cursor position for word boundary adjustment in the Dynamic GC editor.</summary>
    internal DocumentOffset? DynamicGcPrevCursorPosition;

    /// <summary>Read-only editor for the Dynamic Counters Threading section.</summary>
    public EditorState? DynamicThreadingEditorState { get; set; }

    /// <summary>Source text used to build <see cref="DynamicThreadingEditorState"/>, for staleness detection.</summary>
    public string? DynamicThreadingEditorText { get; set; }

    /// <summary>Yank flash decoration provider for the Dynamic Counters Threading editor.</summary>
    public IlYankDecorationProvider DynamicThreadingYankProvider { get; } = new();

    /// <summary>Tracks the previous frame's selection anchor for word boundary adjustment in the Dynamic Threading editor.</summary>
    internal DocumentOffset? DynamicThreadingPrevSelectionAnchor;

    /// <summary>Tracks the previous frame's cursor position for word boundary adjustment in the Dynamic Threading editor.</summary>
    internal DocumentOffset? DynamicThreadingPrevCursorPosition;

    /// <summary>Read-only editor for the Dynamic Summary Trace Summary section.</summary>
    public EditorState? DynamicSummaryEditorState { get; set; }

    /// <summary>Source text used to build <see cref="DynamicSummaryEditorState"/>, for staleness detection.</summary>
    public string? DynamicSummaryEditorText { get; set; }

    /// <summary>Yank flash decoration provider for the Dynamic Summary editor.</summary>
    public IlYankDecorationProvider DynamicSummaryYankProvider { get; } = new();

    /// <summary>Tracks the previous frame's selection anchor for word boundary adjustment in the Dynamic Summary editor.</summary>
    internal DocumentOffset? DynamicSummaryPrevSelectionAnchor;

    /// <summary>Tracks the previous frame's cursor position for word boundary adjustment in the Dynamic Summary editor.</summary>
    internal DocumentOffset? DynamicSummaryPrevCursorPosition;

    // --- Strings Tab State ---

    /// <summary>The minimum string length filter for raw strings.</summary>
    public int StringsMinLength { get; set; } = 4;

    /// <summary>The selected string source tab (0=User, 1=Metadata, 2=Raw).</summary>
    public int StringsSourceTab { get; set; }

    /// <summary>The focused string entry key in the strings table.</summary>
    public object? StringsFocusedKey { get; set; }

    /// <summary>The string detail popup content, or null.</summary>
    public string? StringsDetailContent { get; set; }

    /// <summary>Cached user strings, loaded lazily.</summary>
    public IReadOnlyList<StringEntry>? CachedUserStrings { get; set; }

    /// <summary>Cached metadata strings, loaded lazily.</summary>
    public IReadOnlyList<StringEntry>? CachedMetadataStrings { get; set; }

    /// <summary>Cached raw strings, invalidated when min length changes.</summary>
    public IReadOnlyList<StringEntry>? CachedRawStrings { get; set; }

    /// <summary>The min length used for the cached raw strings.</summary>
    public int CachedRawStringsMinLength { get; set; } = -1;

    // --- Dependency Graph Tab State ---

    /// <summary>Cached dependency graph (nodes + edges).</summary>
    public (IReadOnlyList<GraphNode> Nodes, IReadOnlyList<GraphEdge> Edges)? CachedGraph { get; set; }

    /// <summary>The currently hovered/selected node name in the graph view.</summary>
    public string? GraphSelectedNode { get; set; }

    /// <summary>Stable match index for dependency graph search navigation.</summary>
    public int GraphMatchIndex { get; set; } = -1;

    /// <summary>Keyboard-selected node index in the dependency graph, or -1 for none.</summary>
    public int GraphSelectedIndex { get; set; } = -1;

    // --- Size Treemap Tab State ---

    /// <summary>Cached size tree for treemap visualization.</summary>
    public SizeNode? CachedSizeTree { get; set; }

    /// <summary>The current drill-down level in the treemap.</summary>
    public SizeNode? TreemapCurrentLevel { get; set; }

    /// <summary>Breadcrumb stack for treemap drill-down navigation.</summary>
    public Stack<SizeNode> TreemapBreadcrumb { get; } = new();

    /// <summary>The hovered item description in the treemap.</summary>
    public string? TreemapHoveredItem { get; set; }

    /// <summary>The hovered SizeNode in the treemap, used for click/Enter drill-down.</summary>
    public SizeNode? TreemapHoveredNode { get; set; }

    /// <summary>Stable match index for treemap search navigation.</summary>
    public int TreemapMatchIndex { get; set; } = -1;

    /// <summary>Keyboard-selected child index in the treemap, or -1 for none.</summary>
    public int TreemapSelectedIndex { get; set; } = -1;

    // --- Hex Dump Tab State ---

    /// <summary>The editor state for the hex dump view.</summary>
    public EditorState HexEditorState { get; internal set; }

    /// <summary>The hex-row-aware document wrapper. Used to keep BytesPerRow in sync with the renderer.</summary>
    public HexRowDocument HexRowDoc { get; internal set; }

    /// <summary>Byte offsets of hex search matches in the current assembly.</summary>
    public List<long> HexMatchOffsets { get; set; } = [];

    /// <summary>Index into <see cref="HexMatchOffsets"/> for the currently highlighted match, or -1.</summary>
    public int HexCurrentMatchIndex { get; set; } = -1;

    /// <summary>Byte count of the search pattern, used for match range highlighting.</summary>
    public int HexMatchPatternLength { get; set; }

    /// <summary>Last search query, used to detect query changes for live search.</summary>
    public string? HexLastSearchQuery { get; set; }

    /// <summary>Endianness for the data interpretation panel.</summary>
    public HexEndianness HexEndianness { get; set; } = HexEndianness.Little;

    /// <summary>Status notification message (save result, errors). Auto-clears after 3 seconds.</summary>
    public string? HexNotification { get; set; }

    /// <summary>Whether the jump-to-byte dialog is open.</summary>
    public bool HexJumpDialogOpen { get; set; }

    /// <summary>Text input for the jump-to-byte dialog.</summary>
    public string HexJumpInput { get; set; } = "";

    /// <summary>Search mode: false = ASCII text, true = hex bytes.</summary>
    public bool HexSearchModeHex { get; set; }

    /// <summary>Adaptive throttle flag: set on first slow search to degrade to Enter-only.</summary>
    public bool HexLiveSearchTooSlow { get; set; }

    /// <summary>Target byte offset for renderer scroll override. Set by NavigateToOffset, cleared after EditorNode catches up.</summary>
    public long? HexScrollTarget { get; set; }

    /// <summary>Tracks the EditorNode's raw scroll offset to detect when it catches up after programmatic navigation.</summary>
    public int HexLastEditorScrollOffset { get; set; }

    /// <summary>Vi-style editing mode: Normal (read-only navigation) or Insert (byte editing).</summary>
    public HexEditMode HexMode { get; set; } = HexEditMode.Normal;

    /// <summary>Document version at last save, used to detect dirty state.</summary>
    public long HexCleanVersion { get; set; }

    /// <summary>Whether the hex document has unsaved edits.</summary>
    public bool HexIsDirty => HexEditorState.Document.Version != HexCleanVersion;

    // --- Apphost Detection State ---

    /// <summary>Whether the apphost companion DLL dialog is currently shown.</summary>
    public bool ApphostDialogOpen { get; set; }

    /// <summary>The path to the companion managed .dll, or null if not detected.</summary>
    public string? ApphostCompanionDllPath { get; set; }

    // --- Dynamic Analysis Tab State ---

    /// <summary>Whether the assembly has a CLR entry point (executable, not library).</summary>
    public bool HasEntryPoint => Analyzer.ClrHeader is { EntryPointToken: > 0 };

    /// <summary>Whether the assembly appears to be NativeAOT (no CLR metadata).</summary>
    public bool IsNativeAot => !Analyzer.HasMetadata || Analyzer.ClrHeader is null;

    /// <summary>Whether the assembly targets .NET Framework (not .NET Core / .NET 5+).</summary>
    public bool IsNetFramework => Analyzer.TargetFramework?.StartsWith(".NETFramework", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>The runtime tracer instance, created on first launch.</summary>
    public RuntimeTracer? Tracer { get; set; }

    /// <summary>The selected sub-tab in the Dynamic view (0=Events, 1=Counters, 2=Output, 3=Summary).</summary>
    public int DynamicSubTab { get; set; }

    /// <summary>The focused event row key in the events table.</summary>
    public object? DynamicEventsFocusedKey { get; set; }

    /// <summary>Whether the events table auto-scrolls to the bottom.</summary>
    public bool DynamicAutoScroll { get; set; } = true;

    /// <summary>Whether the focused JIT event can be navigated to the IL Inspector.</summary>
    public bool CanNavigateJitEvent { get; set; }

    /// <summary>Event category filter, or null for all.</summary>
    public TraceEventCategory? DynamicCategoryFilter { get; set; }

    /// <summary>Command-line arguments to pass to the traced process.</summary>
    public string DynamicArguments { get; set; } = "";

    /// <summary>Whether the args editing mode is active.</summary>
    public bool DynamicEditingArgs { get; set; }

    /// <summary>Focused key in the output table.</summary>
    public object? DynamicOutputFocusedKey { get; set; }

    // --- Cross-View Navigation ---

    /// <summary>
    /// Saved source tab for cross-view back navigation, or null if no cross-view jump has occurred.
    /// </summary>
    public (int Tab, int SubTab)? CrossViewBackTarget { get; set; }

    /// <summary>
    /// Switches to the specified tab, finalizing any in-progress search on the current tab.
    /// </summary>
    public void NavigateToTab(int tabIndex)
    {
        if (tabIndex is < 0 or > 7 || CurrentTab == tabIndex) return;

        var previousSearch = Search[CurrentTab];
        if (previousSearch.IsActive && !previousSearch.IsConfirmed)
        {
            if (string.IsNullOrEmpty(previousSearch.Query))
                previousSearch.Dismiss();
            else
                previousSearch.Confirm();
        }

        CurrentTab = tabIndex;
    }

    /// <summary>
    /// Navigates to the IL Inspector and selects the specified method,
    /// expanding the namespace and type tree nodes.
    /// </summary>
    public void NavigateToIlMethod(MethodDefInfo method)
    {
        CrossViewBackTarget = (CurrentTab, PeSubTab);

        // Expand namespace and type in the IL tree
        var typeDef = Analyzer.TypeDefs.FirstOrDefault(t => t.FullName == method.DeclaringType);
        var ns = typeDef is not null && !string.IsNullOrEmpty(typeDef.Namespace)
            ? typeDef.Namespace : "(global)";
        IlTreeExpansionState[$"ns:{ns}"] = true;
        IlTreeExpansionState[$"type:{method.DeclaringType}"] = true;

        IlSelectedMethod = method;
        IlFocusedTreeKey = $"method:{method.Token}";

        NavigateToTab(TabId.IlInspector);
        var ilSearch = Search[TabId.IlInspector];
        ilSearch.Reset();
        App.RequestFocus(node => node is ListNode);
        App.Invalidate();
    }

    /// <summary>
    /// Navigates to the Hex Dump tab, jumping to the file offset corresponding to the given RVA.
    /// </summary>
    public void NavigateToHexOffset(int rva)
    {
        var fileOffset = RvaToFileOffset(rva);
        if (fileOffset < 0) return;

        CrossViewBackTarget = (CurrentTab, PeSubTab);

        // Set cursor position + scroll target (mirrors HexDumpView.NavigateToOffset)
        var doc = HexEditorState.Document;
        if (fileOffset < doc.ByteCount)
        {
            var byteMap = doc.GetByteMap();
            var (charIdx, _) = byteMap.ByteToChar((int)fileOffset);
            HexEditorState.SetCursorPosition(
                new Hex1b.Documents.DocumentOffset(charIdx));
            HexEditorState.ByteCursorOffset = (int)fileOffset;
            HexScrollTarget = fileOffset;
        }

        NavigateToTab(TabId.HexDump);
        App.RequestFocus(node => node is EditorNode);
        App.Invalidate();
    }

    /// <summary>
    /// Returns to the tab saved by the last cross-view navigation.
    /// </summary>
    public void NavigateBack()
    {
        if (CrossViewBackTarget is not { } back) return;

        CrossViewBackTarget = null;
        NavigateToTab(back.Tab);
        if (back.Tab == TabId.PeMetadata)
            PeSubTab = back.SubTab;
        App.RequestFocus(node =>
            node is ListNode or TreeNode or InteractableNode
            || node.GetType().Name.StartsWith("TableNode"));
        App.Invalidate();
    }

    /// <summary>
    /// Requests that the primary content widget (table, editor, tree, etc.) receives focus
    /// after the next render. IL tab targets the ListNode tree; all other tabs target any
    /// content node including TableNode.
    /// </summary>
    public void RequestContentFocus()
    {
        if (CurrentTab == TabId.IlInspector)
            App.RequestFocus(node => node is ListNode);
        else if (CurrentTab == TabId.HexDump)
            App.RequestFocus(node => node is EditorNode e && e.State == HexEditorState);
        else if (CurrentTab == TabId.Dynamic
                 && DynamicSubTab is DynamicSubTabId.Counters or DynamicSubTabId.Summary)
            App.RequestFocus(node => node is EditorNode);
        else
            App.RequestFocus(node =>
                node is TreeNode or ListNode or InteractableNode
                || node.GetType().Name.StartsWith("TableNode"));
    }

    /// <summary>
    /// Converts a relative virtual address (RVA) to a raw file offset using the section table.
    /// Returns -1 if the RVA does not fall within any section.
    /// </summary>
    public long RvaToFileOffset(int rva)
    {
        foreach (var section in Analyzer.Sections)
        {
            if (rva >= section.VirtualAddress && rva < section.VirtualAddress + section.VirtualSize)
                return rva - section.VirtualAddress + section.RawDataOffset;
        }
        return -1;
    }

    /// <summary>
    /// Pushes a new assembly onto the navigation stack and makes it the active analyzer.
    /// Returns false if the assembly could not be loaded or the depth limit is reached.
    /// </summary>
    public bool PushAssembly(string filePath)
    {
        if (NavigationStack.Count >= MaxNavigationDepth)
        {
            NavigationError = $"Navigation depth limit reached ({MaxNavigationDepth})";
            return false;
        }

        AssemblyAnalyzer newAnalyzer;
        try
        {
            newAnalyzer = new AssemblyAnalyzer(filePath);
        }
        catch (Exception ex) when (ex is BadImageFormatException or FileNotFoundException or IOException or UnauthorizedAccessException or OverflowException)
        {
            NavigationError = $"Cannot open assembly: {ex.Message}";
            return false;
        }

        NavigationError = null;
        _focusedDepStack.Push(GeneralFocusedDep);
        _tabStack.Push(CurrentTab);
        _graphSelectionStack.Push(GraphSelectedIndex);
        NavigationStack.Push(Analyzer);
        Analyzer = newAnalyzer;
        StringExtractor = new StringExtractor(Analyzer);
        IlDisassembler = Analyzer.HasMetadata ? new IlDisassembler(Analyzer) : null;
        var hexDoc = new HexRowDocument(new Hex1bDocument(Analyzer.RawBytes.ToArray()));
        HexRowDoc = hexDoc;
        HexEditorState = new EditorState(hexDoc) { IsReadOnly = true };
        HexCleanVersion = hexDoc.Version;
        ResetViewState();
        return true;
    }

    /// <summary>
    /// Pops the top of the navigation stack and restores the previous analyzer.
    /// Returns the tab that was active when the assembly was pushed, or
    /// <see cref="TabId.General"/> if nothing was saved.
    /// </summary>
    public int PopAssembly()
    {
        if (NavigationStack.Count == 0) return TabId.General;
        NavigationError = null;
        Analyzer.Dispose();
        Analyzer = NavigationStack.Pop();
        StringExtractor = new StringExtractor(Analyzer);
        IlDisassembler = Analyzer.HasMetadata ? new IlDisassembler(Analyzer) : null;
        var hexDoc = new HexRowDocument(new Hex1bDocument(Analyzer.RawBytes.ToArray()));
        HexRowDoc = hexDoc;
        HexEditorState = new EditorState(hexDoc) { IsReadOnly = true };
        HexCleanVersion = hexDoc.Version;
        var savedTab = _tabStack.Count > 0 ? _tabStack.Pop() : TabId.General;
        var savedGraphSelection = _graphSelectionStack.Count > 0 ? _graphSelectionStack.Pop() : -1;
        var savedFocus = _focusedDepStack.Count > 0 ? _focusedDepStack.Pop() : null;
        ResetViewState();
        GeneralFocusedDep = savedFocus;
        GraphSelectedIndex = savedGraphSelection;
        return savedTab;
    }

    private void ResetViewState()
    {
        foreach (var s in Search) s.Reset();
        NavigateNextMatch = null;
        NavigatePrevMatch = null;
        CrossViewBackTarget = null;
        GeneralFocusedDep = Analyzer.AssemblyRefs.Count > 0 ? Analyzer.AssemblyRefs[0].Name : null;
        PeSubTab = 0;
        PeFocusedKey = null;
        PeDetailContent = null;
        IlFocusedTreeKey = null;
        IlSelectedMethod = null;
        IlTreeExpansionState.Clear();
        IlEditorState = null;
        IlEditorMethod = null;
        IlEditorAnalyzer = null;
        IlPrevSelectionAnchor = null;
        IlPrevCursorPosition = null;
        IlSearchMatches = [];
        IlCurrentMatchIndex = -1;
        IlLastSearchQuery = null;
        IlPendingCursorMatch = null;
        IlTextMatchMethodTokens = null;
        IlYankProvider.HighlightRange = null;
        YankNotification = null;
        VimPending = VimMotionState.Idle;
        VimPendingEditor = null;
        VimPendingCursorOffset = 0;
        VimPendingTimestamp = default;
        GeneralInfoEditorState = null;
        GeneralInfoEditorText = null;
        PeHeadersEditorState = null;
        PeHeadersEditorText = null;
        ClrHeaderEditorState = null;
        ClrHeaderEditorText = null;
        DataInterpEditorState = null;
        DataInterpEditorText = null;
        PeDetailEditorState = null;
        PeDetailEditorText = null;
        StringsDetailEditorState = null;
        StringsDetailEditorText = null;
        StringsSourceTab = 0;
        StringsFocusedKey = null;
        StringsDetailContent = null;
        CachedUserStrings = null;
        CachedMetadataStrings = null;
        CachedRawStrings = null;
        CachedGraph = null;
        GraphSelectedNode = null;
        GraphMatchIndex = -1;
        GraphSelectedIndex = -1;
        CachedSizeTree = null;
        TreemapCurrentLevel = null;
        TreemapBreadcrumb.Clear();
        TreemapHoveredItem = null;
        TreemapHoveredNode = null;
        TreemapMatchIndex = -1;
        TreemapSelectedIndex = -1;
        HexMatchOffsets = [];
        HexCurrentMatchIndex = -1;
        HexMatchPatternLength = 0;
        HexLastSearchQuery = null;
        HexNotification = null;
        HexJumpDialogOpen = false;
        HexJumpInput = "";
        HexSearchModeHex = false;
        HexLiveSearchTooSlow = false;
        HexScrollTarget = null;
        HexLastEditorScrollOffset = 0;
        HexMode = HexEditMode.Normal;
        // Note: HexEndianness intentionally NOT reset (user preference)
        DynamicSubTab = 0;
        DynamicEventsFocusedKey = null;
        DynamicAutoScroll = true;
        DynamicCategoryFilter = null;
        DynamicEditingArgs = false;
        DynamicOutputFocusedKey = null;
        DynamicCpuEditorState = null;
        DynamicCpuEditorText = null;
        DynamicMemoryEditorState = null;
        DynamicMemoryEditorText = null;
        DynamicGcEditorState = null;
        DynamicGcEditorText = null;
        DynamicThreadingEditorState = null;
        DynamicThreadingEditorText = null;
        DynamicSummaryEditorState = null;
        DynamicSummaryEditorText = null;
        // Note: Tracer and DynamicArguments intentionally NOT reset
    }

    /// <summary>
    /// Gets the active string entries based on the current source tab, applying search filter.
    /// </summary>
    /// <returns>The filtered string entries for display.</returns>
    public IReadOnlyList<StringEntry> GetActiveStrings()
    {
        var entries = StringsSourceTab switch
        {
            StringsSubTabId.UserStrings => CachedUserStrings ??= StringExtractor.ExtractUserStrings(),
            StringsSubTabId.Metadata => CachedMetadataStrings ??= StringExtractor.ExtractMetadataStrings(),
            StringsSubTabId.RawBinary => GetCachedRawStrings(),
            _ => []
        };

        var query = Search[TabId.Strings].Query;
        if (!string.IsNullOrEmpty(query))
        {
            entries = [.. entries.Where(e => e.Value.Contains(query, StringComparison.OrdinalIgnoreCase))];
        }

        return entries;
    }

    /// <summary>
    /// Formats a file size in bytes to a human-readable string (always human-readable).
    /// </summary>
    /// <param name="bytes">The size in bytes.</param>
    /// <returns>A formatted string like "1.5 KB" or "3.2 MB".</returns>
    public static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB"
    };

    /// <summary>
    /// Formats a size respecting the current <see cref="HumanReadableSizes"/> toggle.
    /// Returns human-readable (e.g. "1.5 KB") or hex (e.g. "0x600").
    /// </summary>
    public string FormatSizeToggleable(long bytes) =>
        HumanReadableSizes ? FormatSize(bytes) : $"0x{bytes:X}";

    /// <inheritdoc/>
    public void Dispose()
    {
        Tracer?.Dispose();
        foreach (var analyzer in NavigationStack)
            analyzer.Dispose();
        Analyzer.Dispose();
    }

    private IReadOnlyList<StringEntry> GetCachedRawStrings()
    {
        if (CachedRawStrings is null || CachedRawStringsMinLength != StringsMinLength)
        {
            CachedRawStrings = StringExtractor.ExtractRawStrings(StringsMinLength);
            CachedRawStringsMinLength = StringsMinLength;
        }

        return CachedRawStrings;
    }
}
