using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Dotsider.Views;
using Hex1b;
using Hex1b.Documents;
using Hex1b.Nodes;
using Hex1b.Widgets;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Dotsider;

/// <summary>
/// Holds all mutable UI state for the dotsider application.
/// Rebuilt each frame by the Hex1b render loop.
/// </summary>
public sealed class DotsiderState : IDisposable
{
    private readonly Lock _graphBuildLock = new();
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly List<Task> _renderNudgerTasks = [];
    private DependencyGraphSnapshot? _dependencyGraphSnapshot;
    private readonly Lazy<EmbeddedSourceTempFileStore> _embeddedSourceTempFiles =
        new(static () => new EmbeddedSourceTempFileStore());
    private CancellationTokenSource? _graphBuildCancellation;
    private Task _graphBuildTask = Task.CompletedTask;
    private int _graphBuildGeneration;
    private bool _graphBuildFailed;
    private bool _graphBuildInProgress;
    private string? _graphNavigationError;
    private IReadOnlyDictionary<string, GraphNavigationContext>? _legacyGraphNavigation;
    private bool _disposed;

    /// <summary>
    /// Creates a new application state for the specified assembly file.
    /// </summary>
    public DotsiderState(Hex1bApp app, string filePath,
        ConcurrentQueue<Action<DotsiderState>>? pendingMutations = null)
    {
        App = app;
        PendingMutations = pendingMutations ?? new();

        var openResult = AssemblyLoader.Open(filePath);
        Analyzer = openResult switch
        {
            AssemblyOpenResult.Direct(var a) => a,
            AssemblyOpenResult.NativeAot(var aot) => aot,
            AssemblyOpenResult.ApphostWithCompanion(var host, _) => host,
            AssemblyOpenResult.BundleEntry(var entry, _) => entry,
            _ => throw new InvalidOperationException($"Unknown open result: {openResult.GetType().Name}")
        };

        if (openResult is AssemblyOpenResult.ApphostWithCompanion(_, var companion))
        {
            ApphostCompanionDllPath = companion;
            ApphostDialogOpen = true;
        }

        // AOT binaries offer their pre-ILC build outputs the way apphosts offer their
        // companion dll. mstat/DGML-only discoveries never open the dialog — their
        // fallbacks feed the Size Map and Dep Graph silently.
        if (openResult is AssemblyOpenResult.NativeAot
            && Analyzer.PreIlcSidecars is { HasAttachableCompanion: true })
        {
            PreIlcDialogOpen = true;
        }

        StringExtractor = new StringExtractor(Analyzer);
        if (Analyzer.HasMetadata)
            IlDisassembler = new IlDisassembler(Analyzer);

        RootNetFxBindingContext = NetFxBindingContext.TryBuild(Analyzer);

        var hexDoc = new HexRowDocument(new Hex1bDocument(Analyzer.RawBytes.ToArray()));
        HexRowDoc = hexDoc;
        HexEditorState = new EditorState(hexDoc) { IsReadOnly = true };
        HexCleanVersion = hexDoc.Version;
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
        RootNetFxBindingContext = NetFxBindingContext.TryBuild(Analyzer);
        var hexDoc = new HexRowDocument(new Hex1bDocument(Analyzer.RawBytes.ToArray()));
        HexRowDoc = hexDoc;
        HexEditorState = new EditorState(hexDoc) { IsReadOnly = true };
        HexCleanVersion = hexDoc.Version;
    }

    /// <summary>The Hex1b application instance.</summary>
    public Hex1bApp App { get; }

    /// <summary>
    /// Gets the private temporary store used for PDB-embedded source documents.
    /// </summary>
    internal EmbeddedSourceTempFileStore EmbeddedSourceTempFiles
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _embeddedSourceTempFiles.Value;
        }
    }

    /// <summary>
    /// Queue of mutations to apply on the UI thread, drained at the top of each render frame.
    /// Used by the diagnostics socket listener for thread-safe state changes.
    /// </summary>
    public ConcurrentQueue<Action<DotsiderState>> PendingMutations { get; }

    /// <summary>The core assembly analyzer (current top of navigation stack).</summary>
    public AssemblyAnalyzer Analyzer { get; internal set; }

    /// <summary>
    /// The .NET Framework binding context for the *root* analyzed assembly, or
    /// <see langword="null"/> for non-net48 roots. Cached so every resolution surface (Dep
    /// Graph, IL navigation, General-tab drill-in, type-forwarder chase) uses the same context
    /// and produces the same bind for any net48 reference.
    /// </summary>
    public NetFxBindingContext? RootNetFxBindingContext { get; private set; }

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

    /// <summary>The why-chain shown in the Size Map popup, or null when the popup is closed.</summary>
    public string? SizeMapWhyContent { get; set; }

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

    /// <summary>Source Link marker decoration provider for the IL editor.</summary>
    public IlSourceLinkDecorationProvider IlSourceLinkProvider { get; } = new();

    /// <summary>All text-level search matches across method disassemblies, computed on search confirm.</summary>
    public List<IlMatch> IlSearchMatches { get; set; } = [];

    /// <summary>Index into <see cref="IlSearchMatches"/> for the currently highlighted match, or -1.</summary>
    public int IlCurrentMatchIndex { get; set; } = -1;

    /// <summary>Last confirmed search query, used to avoid recomputing matches.</summary>
    public string? IlLastSearchQuery { get; set; }

    /// <summary>Pending cursor match to apply on next frame (set by NavigateToMatch, consumed by BuildEditorPane).</summary>
    public IlMatch? IlPendingCursorMatch { get; set; }

    /// <summary>Method tokens whose IL text matches the confirmed search query. Used to broaden tree filtering.</summary>
    public HashSet<(AssemblyAnalyzer? Owner, int Token)>? IlTextMatchMethodTokens { get; set; }

    /// <summary>Back stack for IL go-to-definition navigation. Esc pops and restores.</summary>
    public Stack<IlBackEntry> IlBackStack { get; } = new();

    /// <summary>The instruction list for the currently displayed method.</summary>
    public IReadOnlyList<IlInstruction>? IlInstructions { get; set; }

    /// <summary>The number of header lines in the current disassembly.</summary>
    public int IlHeaderLineCount { get; set; }

    /// <summary>The native symbol selected in the tree for a non-managed binary (native IL-inspector mode).</summary>
    public NativeSymbol? IlSelectedNativeSymbol { get; set; }

    /// <summary>The native symbol currently loaded into the editor pane (for staleness detection).</summary>
    public NativeSymbol? IlEditorNativeSymbol { get; set; }

    /// <summary>Span-driven syntax highlighting for the native disassembly listing.</summary>
    public NativeSyntaxDecorationProvider IlNativeSyntaxProvider { get; } = new();

    /// <summary>Span-driven target underlining for the native disassembly listing.</summary>
    public NativeNavigationDecorationProvider IlNativeNavigationProvider { get; } = new();

    /// <summary>The decoded native instructions of the currently displayed symbol, or null.</summary>
    public IReadOnlyList<NativeInstruction>? IlNativeInstructions { get; set; }

    /// <summary>The number of header lines in the current native disassembly.</summary>
    public int IlNativeHeaderLineCount { get; set; }

    /// <summary>The back-stack of native symbols visited via go-to-definition, for Esc.</summary>
    public Stack<NativeBackEntry> IlNativeBackStack { get; } = new();

    /// <summary>Character offsets of the confirmed search query within the native listing, for n/N.</summary>
    public IReadOnlyList<int> IlNativeSearchOffsets { get; set; } = [];

    /// <summary>The field targeted by the last field go-to-definition, displayed in the right pane.</summary>
    public FieldDefInfo? IlSelectedField { get; set; }

    // --- Pre-ILC side-by-side pane state ---
    // The pair pane renders native code NEXT TO managed IL when a companion set is
    // attached. Its editor state, caches, and decoration providers are deliberately
    // separate from the solo-native pipeline: a VA must never yield the same
    // StatePanelWidget identity in two scopes, and two live editors must never share
    // span-driven providers.

    /// <summary>The analyzer that defines <see cref="IlSelectedMethod"/> when it came from a pre-ILC local-reference row; null means the routed metadata analyzer.</summary>
    public AssemblyAnalyzer? IlSelectedMethodOwner { get; set; }

    /// <summary>The pane that owns search and navigation keys while the pair is visible.</summary>
    public IlPane IlFocusedPane { get; set; }

    /// <summary>The editor state of the native pair pane, or null.</summary>
    public EditorState? IlPairNativeEditorState { get; set; }

    /// <summary>The symbol loaded in the pair pane, for staleness detection by virtual address.</summary>
    public NativeSymbol? IlPairNativeSymbol { get; set; }

    /// <summary>
    /// Cursor offsets pushed before an intra-listing local-label jump in the pair pane, so Esc
    /// returns to the departure instruction — the pair-pane mirror of the solo native back stack.
    /// </summary>
    public Stack<int> IlPairNativeBackStack { get; } = new();

    /// <summary>
    /// Whether the current pair disassembly was rendered with the correlation index available.
    /// A symbol selected before the index finished building carries reduced target names; once
    /// the index arrives this flag drives a one-time rebuild so companion names appear.
    /// </summary>
    public bool IlPairNativeBuiltWithIndex { get; set; }

    /// <summary>The decoded instructions of the pair pane's listing, or null.</summary>
    public IReadOnlyList<NativeInstruction>? IlPairNativeInstructions { get; set; }

    /// <summary>The number of header lines in the pair pane's listing.</summary>
    public int IlPairNativeHeaderLineCount { get; set; }

    /// <summary>Span-driven syntax highlighting for the pair pane.</summary>
    public NativeSyntaxDecorationProvider IlPairNativeSyntaxProvider { get; } = new();

    /// <summary>Span-driven target underlining for the pair pane.</summary>
    public NativeNavigationDecorationProvider IlPairNativeNavigationProvider { get; } = new();

    /// <summary>Search match highlighting for the pair pane.</summary>
    public IlSearchDecorationProvider IlPairSearchProvider { get; } = new();

    /// <summary>Yank flash decoration provider for the pair pane.</summary>
    public IlYankDecorationProvider IlPairYankProvider { get; } = new();

    /// <summary>Character offsets of the confirmed search query within the pair listing, for n/N.</summary>
    public IReadOnlyList<int> IlPairSearchOffsets { get; set; } = [];

    /// <summary>Whether the confirmed IL search was computed for a native listing — recomputed when the focused pane changes scope.</summary>
    internal bool IlSearchScopeNative { get; set; }

    /// <summary>Identity key for the pair pane's current StatePanelWidget.</summary>
    internal object? IlPairEditorKey { get; set; }

    /// <summary>Stable parent StatePanelWidget key for the pair editor scope.</summary>
    internal object IlPairEditorScopeKey { get; } = new object();

    /// <summary>Pair-pane editor identity keys by (analyzer, virtual address) — separate from <see cref="IlNativeEditorKeyCache"/>.</summary>
    internal Dictionary<(AssemblyAnalyzer, ulong), object> IlPairNativeEditorKeyCache { get; } = [];

    /// <summary>Cached pair-pane editor states for symbols not currently visible.</summary>
    internal Dictionary<object, EditorState> IlPairCachedEditors { get; } = new(ReferenceEqualityComparer.Instance);

    /// <summary>Last measured width of the IL Inspector's right pane area; zero until first measurement.</summary>
    internal int IlRightPaneWidth { get; set; }

    /// <summary>Returns the stable pair-pane editor identity key for a symbol.</summary>
    internal object GetOrCreatePairNativeEditorKey(AssemblyAnalyzer analyzer, ulong address)
    {
        var cacheKey = (analyzer, address);
        if (!IlPairNativeEditorKeyCache.TryGetValue(cacheKey, out var key))
        {
            key = new object();
            IlPairNativeEditorKeyCache[cacheKey] = key;
        }

        return key;
    }

    /// <summary>Clears the pair pane: editor, caches, providers, and measurements.</summary>
    internal void ClearPairPaneState()
    {
        IlSelectedMethodOwner = null;
        IlFocusedPane = IlPane.Tree;
        IlPairNativeEditorState = null;
        IlPairNativeSymbol = null;
        IlPairNativeBackStack.Clear();
        IlPairNativeBuiltWithIndex = false;
        IlPairNativeInstructions = null;
        IlPairNativeHeaderLineCount = 0;
        IlPairNativeSyntaxProvider.Instructions = null;
        IlPairNativeNavigationProvider.Instructions = null;
        IlPairSearchProvider.Query = null;
        IlPairSearchProvider.CurrentMatchStart = null;
        IlPairSearchProvider.CurrentMatchLength = 0;
        IlPairYankProvider.HighlightRange = null;
        IlPairSearchOffsets = [];
        IlSearchScopeNative = false;
        IlPairEditorKey = null;
        IlPairNativeEditorKeyCache.Clear();
        IlPairCachedEditors.Clear();
        IlRightPaneWidth = 0;
    }

    /// <summary>Navigation decoration provider that underlines navigable IL operands.</summary>
    public IlNavigationDecorationProvider IlNavigationProvider { get; } = new();

    /// <summary>Identity key for the current editor's StatePanelWidget (per-method/field, reference-equal).</summary>
    internal object? IlEditorKey { get; set; }

    /// <summary>Stable parent StatePanelWidget key for the editor scope.</summary>
    internal object IlEditorScopeKey { get; } = new object();

    /// <summary>Maps (analyzer, token) to stable key objects for StatePanelWidget identity.</summary>
    internal Dictionary<(AssemblyAnalyzer, int), object> IlEditorKeyCache { get; } = [];

    /// <summary>
    /// Native-mode editor identity keys, keyed by the full 64-bit virtual address. Kept separate from
    /// <see cref="IlEditorKeyCache"/> so a VA never collides with a managed token (nor with another VA
    /// that shares its low 32 bits) — <see cref="Hex1b.Widgets.StatePanelWidget"/> matches by reference.
    /// </summary>
    internal Dictionary<(AssemblyAnalyzer, ulong), object> IlNativeEditorKeyCache { get; } = [];

    /// <summary>Cached editor states for editors not currently visible (analogous to old SavedEditors).</summary>
    internal Dictionary<object, EditorState> IlCachedEditors { get; } = new(ReferenceEqualityComparer.Instance);

    /// <summary>The field currently loaded in the editor, for staleness detection.</summary>
    internal FieldDefInfo? IlEditorField { get; set; }

    /// <summary>Cached scroll-panel node hosting the IL tree. Captured each render via
    /// the <see cref="Hex1bApp.Focusables"/> scan in <see cref="Views.IlInspectorView"/>.
    /// The panel's child is a viewport-sized window of rows, so the node itself never
    /// scrolls; it serves as the focusable input host and the viewport-height source.</summary>
    internal ScrollPanelNode? IlScrollPanelNode { get; set; }

    /// <summary>
    /// First visible row index of the IL tree — the tree's scroll offset, owned by state
    /// rather than the <see cref="ScrollPanelNode"/>. Hex1b clamps a scroll child's render
    /// surface to 10,000 rows, so trees past that (fully expanded native trees) go blank
    /// when the panel itself translates; instead the view renders only the visible window
    /// of rows and this offset selects it. Clamped to the row count during each build.
    /// </summary>
    internal int IlTreeScrollOffset { get; set; }

    /// <summary>
    /// Monotonic count of widget builds, advanced at the top of
    /// <see cref="DotsiderApp.Build"/>. The deferred nudger behind
    /// <see cref="RequestExtraFrame"/> watches it to know a new build has run and stop.
    /// </summary>
    internal int BuildGeneration { get; set; }

    /// <summary>
    /// Whether a deferred extra-frame nudger is in flight. Cleared at the top of each
    /// build; prevents one build's multiple requests from stacking duplicate nudgers.
    /// </summary>
    internal bool ExtraFrameArmed { get; set; }

    /// <summary>
    /// The viewport height the current tree window was built against, recorded by
    /// <see cref="Views.IlTreeList.Build"/>. The tree's layout observer compares it to
    /// the actual height assigned during arrange and requests a follow-up build when
    /// they differ.
    /// </summary>
    internal int IlTreeWindowViewport { get; set; }

    /// <summary>One-shot flag set by <see cref="SetIlFocusedTreeKey"/> so the next IL
    /// Inspector render scrolls the focused row into view. Cleared by the consumer in
    /// <see cref="Views.IlInspectorView.Build"/> once the panel is captured and the layout
    /// reflects the current rows. Internal tree key bindings assign
    /// <see cref="IlFocusedTreeKey"/> directly and never set the flag, so mouse-wheel
    /// scrolls that push the selection offscreen are not snapped back.</summary>
    internal bool IlScrollSelectionIntoViewPending { get; set; }

    /// <summary>Whether the first key of a gd chord has been pressed.</summary>
    public bool IlGdPending { get; set; }

    /// <summary>Timestamp when the gd chord was armed.</summary>
    public DateTime IlGdTimestamp { get; set; }

    /// <summary>Transient notice in the hints bar. Auto-clears after 3 seconds.</summary>
    public string? TransientNotice { get; set; }

    /// <summary>Generation counter for transient notice timer race prevention.</summary>
    public long TransientNoticeGeneration { get; set; }

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

    /// <summary>Timestamp of the latest text-object state transition.</summary>
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

    /// <summary>Read-only editor for the Size Map why-chain popup overlay.</summary>
    public EditorState? SizeMapWhyEditorState { get; set; }

    /// <summary>Source text used to build <see cref="SizeMapWhyEditorState"/>, for staleness detection.</summary>
    public string? SizeMapWhyEditorText { get; set; }

    /// <summary>Yank flash decoration provider for the why-chain popup editor.</summary>
    public IlYankDecorationProvider SizeMapWhyYankProvider { get; } = new();

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

    /// <summary>The selected string source tab (0=User, 1=Metadata, 2=Raw, 3=Raw UTF-16).</summary>
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

    /// <summary>Cached raw UTF-16 strings, invalidated when min length changes.</summary>
    public IReadOnlyList<StringEntry>? CachedRawUtf16Strings { get; set; }

    /// <summary>The min length used for the cached raw UTF-16 strings.</summary>
    public int CachedRawUtf16StringsMinLength { get; set; } = -1;

    /// <summary>Cached frozen string literals from a Native AOT binary, loaded lazily.</summary>
    public IReadOnlyList<StringEntry>? CachedFrozenStrings { get; set; }

    // --- Dependency Graph Tab State ---

    /// <summary>Gets or sets the cached dependency graph topology for the current analyzer.</summary>
    public (IReadOnlyList<GraphNode> Nodes, IReadOnlyList<GraphEdge> Edges)? CachedGraph
    {
        get
        {
            var snapshot = GraphSnapshot;
            return snapshot is null ? null : (snapshot.Nodes, snapshot.Edges);
        }
        set
        {
            lock (_graphBuildLock)
            {
                if (value is not { } graph)
                {
                    Volatile.Write(ref _dependencyGraphSnapshot, null);
                    return;
                }

                Volatile.Write(
                    ref _dependencyGraphSnapshot,
                    new DependencyGraphSnapshot(
                        graph.Nodes,
                        graph.Edges,
                        _legacyGraphNavigation));
            }
        }
    }

    /// <summary>
    /// Gets or sets per-node navigation metadata for the cached graph, keyed by
    /// <see cref="GraphNode.Id"/>. This data is never serialized and is used only by the TUI.
    /// </summary>
    public IReadOnlyDictionary<string, GraphNavigationContext>? GraphNavigation
    {
        get
        {
            var snapshot = GraphSnapshot;
            return snapshot?.NavigationById ?? Volatile.Read(ref _legacyGraphNavigation);
        }
        set
        {
            lock (_graphBuildLock)
            {
                Volatile.Write(ref _legacyGraphNavigation, value);
                var snapshot = GraphSnapshot;
                if (snapshot is not null)
                {
                    Volatile.Write(
                        ref _dependencyGraphSnapshot,
                        new DependencyGraphSnapshot(
                            snapshot.Nodes,
                            snapshot.Edges,
                            value));
                }
            }
        }
    }

    /// <summary>
    /// Gets the immutable dependency graph snapshot consumed by production readers.
    /// </summary>
    internal DependencyGraphSnapshot? GraphSnapshot =>
        Volatile.Read(ref _dependencyGraphSnapshot);

    /// <summary>
    /// Whether the Dep Graph view currently hides framework assemblies. Toggled by the
    /// <c>f</c> key. Applies to rendering, search, selection, yank, and Enter — not to
    /// the underlying cached graph.
    /// </summary>
    public bool DepGraphHideFramework { get; set; }

    /// <summary>
    /// The dependency-scope narrowing currently applied to the Dep Graph view. Toggled by the
    /// <c>d</c> key, separate from the framework filter. In <see cref="DependencyGraphScope.DirectOnly"/>
    /// the view shows only the root and its depth-1 references; in <see cref="DependencyGraphScope.All"/>
    /// the full transitive closure is visible.
    /// </summary>
    public DependencyGraphScope DepGraphScope { get; set; } = DependencyGraphScope.All;

    /// <summary>
    /// Cached render layout for the Dep Graph view, populated by the view each frame when
    /// the underlying inputs are unchanged. Internal — view-layer geometry must not leak
    /// across the public graph contract.
    /// </summary>
    internal GraphRenderLayout? CachedGraphRenderLayout { get; set; }

    /// <summary>
    /// Invalidation key for <see cref="CachedGraphRenderLayout"/>. When this key differs
    /// from the current frame's computed key the layout is rebuilt; mouse moves alone do
    /// not invalidate.
    /// </summary>
    internal GraphRenderLayoutKey? CachedGraphRenderLayoutKey { get; set; }

    /// <summary>
    /// True while the transitive dependency-graph build is in flight on a background task.
    /// The view shows a status message while this is set.
    /// </summary>
    public bool GraphBuildInProgress
    {
        get => Volatile.Read(ref _graphBuildInProgress);
        set => Volatile.Write(ref _graphBuildInProgress, value);
    }

    /// <summary>
    /// Gets the current dependency-graph build task. Tests and internal coordinators can await the
    /// real operation instead of polling rendered output.
    /// </summary>
    internal Task GraphBuildTask
    {
        get
        {
            lock (_graphBuildLock)
                return _graphBuildTask;
        }
    }

    /// <summary>
    /// Gets a task that completes when every currently-owned render nudger completes.
    /// </summary>
    internal Task RenderNudgerTask
    {
        get
        {
            lock (_graphBuildLock)
                return Task.WhenAll([.. _renderNudgerTasks]);
        }
    }

    /// <summary>
    /// Gets or sets the dependency-graph builder used by the owned background operation.
    /// </summary>
    internal Func<AssemblyAnalyzer, CancellationToken, DependencyGraphResult> GraphBuilder { get; set; } =
        DependencyGraphBuilder.BuildWithCancellation;

    /// <summary>
    /// Error message produced by the dependency-graph build or the most recent Enter-to-open
    /// attempt, or <see langword="null"/> when no error has occurred.
    /// </summary>
    public string? GraphNavigationError
    {
        get => Volatile.Read(ref _graphNavigationError);
        set => Volatile.Write(ref _graphNavigationError, value);
    }

    /// <summary>The currently hovered/selected node name in the graph view.</summary>
    public string? GraphSelectedNode { get; set; }

    /// <summary>Stable match index for dependency graph search navigation.</summary>
    public int GraphMatchIndex { get; set; } = -1;

    /// <summary>Keyboard-selected node index in the dependency graph, or -1 for none.</summary>
    public int GraphSelectedIndex { get; set; } = -1;

    /// <summary>
    /// Vertical scroll offset of the Dep Graph viewport, in character rows. The graph is
    /// rendered with every Y shifted by this amount. Clamped each frame to the current
    /// render-layout content height minus viewport height, so resizing or filter changes
    /// can only shrink the valid scroll range, never strand the viewport off-content.
    /// </summary>
    public int DepGraphScrollY { get; set; }

    /// <summary>
    /// Snapshot of (Width, Height, ContentHeight) the Dep Graph scrollbar widget was last
    /// constructed against. <see cref="Views.DependencyGraphView"/> writes this every frame
    /// from the current cached layout before constructing the scrollbar; <c>DrawGraph</c>
    /// compares the post-rebuild layout against this snapshot and calls
    /// <see cref="Hex1bApp.Invalidate"/> exactly once on change so the next frame renders the
    /// scrollbar against the freshly-cached layout. The per-frame builder write is the
    /// reset — no explicit clearing needed.
    /// </summary>
    internal (int Width, int Height, int ContentHeight)? DepGraphScrollbarSnapshot { get; set; }

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

    // --- Pre-ILC Sidecar State ---

    /// <summary>Whether the pre-ILC sidecar offer dialog is currently shown.</summary>
    public bool PreIlcDialogOpen { get; set; }

    /// <summary>Whether any modal companion dialog is open — every dialog guard site checks this.</summary>
    public bool ModalDialogOpen => ApphostDialogOpen || PreIlcDialogOpen;

    /// <summary>
    /// The analyzer that answers metadata questions: the attached pre-ILC root when the
    /// current binary has one, otherwise the current analyzer. Metadata-driven views route
    /// through this; binary views stay on <see cref="Analyzer"/>.
    /// </summary>
    public AssemblyAnalyzer MetadataAnalyzer => Analyzer.PreIlcCompanions?.Root ?? Analyzer;

    /// <summary>The IL disassembler for <see cref="MetadataAnalyzer"/>.</summary>
    public IlDisassembler? MetadataIlDisassembler => GetMetadataIlDisassembler(MetadataAnalyzer);

    /// <summary>The string extractor for <see cref="MetadataAnalyzer"/> — the companion's when attached.</summary>
    public StringExtractor MetadataStringExtractor
    {
        get
        {
            if (Analyzer.PreIlcCompanions is null) return StringExtractor;
            EnsureCompanionUi();
            return _companionStringExtractor ?? StringExtractor;
        }
    }

    /// <summary>
    /// The managed↔native correlation index for the current analyzer, published by
    /// <see cref="EnsureManagedNativeIndexAsync"/>; null before the background build lands.
    /// </summary>
    public ManagedNativeIndex? PreIlcIndex { get; internal set; }

    /// <summary>Whether a correlation index build is currently running in the background.</summary>
    public bool PreIlcIndexBuildInProgress { get; internal set; }

    /// <summary>Whether the IL Inspector tree shows the native-symbol view while companions are attached.</summary>
    public bool IlAotTreeNativeView { get; set; }

    private readonly Dictionary<AssemblyAnalyzer, IlDisassembler> _companionDisassemblers =
        new(ReferenceEqualityComparer.Instance);
    private StringExtractor? _companionStringExtractor;
    private PreIlcCompanionSet? _companionUiFor;

    /// <summary>
    /// The IL disassembler for a specific member of the companion set (multi-assembly
    /// trees select methods from local references too), or the current analyzer's own.
    /// Null when <paramref name="owner"/> is neither.
    /// </summary>
    /// <param name="owner">The analyzer that defines the method being disassembled.</param>
    public IlDisassembler? GetMetadataIlDisassembler(AssemblyAnalyzer owner)
    {
        if (ReferenceEquals(owner, Analyzer)) return IlDisassembler;

        EnsureCompanionUi();
        var set = _companionUiFor;
        if (set is null || !set.All.Contains(owner)) return null;

        if (!_companionDisassemblers.TryGetValue(owner, out var disassembler))
        {
            disassembler = new IlDisassembler(owner);
            _companionDisassemblers[owner] = disassembler;
        }

        return disassembler;
    }

    /// <summary>
    /// Attaches the probed pre-ILC companions to the current analyzer and starts the
    /// correlation index build. Unlike the apphost accept, this never replaces the
    /// analyzer — the native binary stays current and metadata routing takes over.
    /// </summary>
    /// <returns>Whether the companions attached.</returns>
    public bool AttachPreIlc()
    {
        PreIlcCompanionSet? set;
        try
        {
            set = Analyzer.AttachPreIlcCompanions();
        }
        catch (ObjectDisposedException)
        {
            set = null;
        }

        if (set is null)
        {
            ShowTransientNotice("Cannot open pre-ILC sidecar assembly");
            return false;
        }

        IlAotTreeNativeView = false;
        InvalidateMetadataRoutedCaches();
        EnsureManagedNativeIndexAsync();
        return true;
    }

    /// <summary>
    /// Detaches the pre-ILC companions, restoring native-only routing and clearing every
    /// piece of IL state that referenced a companion analyzer.
    /// </summary>
    public void DetachPreIlc()
    {
        if (Analyzer.PreIlcCompanions is null) return;

        Analyzer.DetachPreIlcCompanions();
        PreIlcIndex = null;
        ClearCompanionIlState();
        InvalidateMetadataRoutedCaches();
        ShowTransientNotice("Detached pre-ILC sidecars");
    }

    /// <summary>
    /// Kicks off a background build of the managed↔native correlation index for the
    /// current analyzer's attached companions. No-op while a build is in flight or after
    /// the index landed; a result for a stale analyzer is discarded. The analyzer-level
    /// generation guard makes a build racing detach or dispose harmless.
    /// </summary>
    public void EnsureManagedNativeIndexAsync()
    {
        if (PreIlcIndex is not null || PreIlcIndexBuildInProgress) return;
        var capturedAnalyzer = Analyzer;
        if (capturedAnalyzer.PreIlcCompanions is null) return;

        PreIlcIndexBuildInProgress = true;
        _ = QueueDedicatedBackgroundWork(() =>
        {
            try
            {
                ManagedNativeIndex? index;
                try
                {
                    index = capturedAnalyzer.ManagedNativeIndex;
                }
                catch (ObjectDisposedException)
                {
                    index = null;
                }

                if (!ReferenceEquals(Analyzer, capturedAnalyzer))
                    return;

                PreIlcIndex = index;
            }
            finally
            {
                PreIlcIndexBuildInProgress = false;
                App.Invalidate();
                RequestExtraFrame();
            }
        });
    }

    /// <summary>
    /// Re-opens the companion offer dialogs after popping back to an analyzer that still
    /// has an unaccepted offer — shared by the Esc back handler and the diagnostics
    /// listener so both navigation paths behave identically.
    /// </summary>
    internal void ReofferCompanionDialogsAfterPop()
    {
        if (ApphostCompanionDllPath is not null && !Analyzer.HasMetadata)
            ApphostDialogOpen = true;
        if (Analyzer.PreIlcSidecars is { HasAttachableCompanion: true } && Analyzer.PreIlcCompanions is null)
            PreIlcDialogOpen = true;
    }

    /// <summary>Drops caches whose contents depend on which analyzer answers metadata questions.</summary>
    private void InvalidateMetadataRoutedCaches()
    {
        CachedUserStrings = null;
        CachedMetadataStrings = null;
        GeneralInfoEditorState = null;
        GeneralInfoEditorText = null;
        ClrHeaderEditorState = null;
        ClrHeaderEditorText = null;
        PeFocusedKey = null;
        GeneralFocusedDep = MetadataAnalyzer.AssemblyRefs.Count > 0
            ? MetadataAnalyzer.AssemblyRefs[0].Name
            : null;
        Search[TabId.IlInspector].Reset();
        IlSearchMatches = [];
        IlCurrentMatchIndex = -1;
        IlLastSearchQuery = null;
        IlPendingCursorMatch = null;
        IlTextMatchMethodTokens = null;
        IlNativeSearchOffsets = [];
    }

    /// <summary>Clears IL Inspector state that references companion analyzers.</summary>
    private void ClearCompanionIlState()
    {
        ClearPairPaneState();
        SetIlFocusedTreeKey(null);
        IlSelectedMethod = null;
        IlSelectedField = null;
        IlEditorState = null;
        IlEditorMethod = null;
        IlEditorAnalyzer = null;
        IlEditorKey = null;
        IlEditorField = null;
        IlEditorKeyCache.Clear();
        IlCachedEditors.Clear();
        IlBackStack.Clear();
        IlInstructions = null;
        IlHeaderLineCount = 0;
        IlNavigationProvider.Instructions = null;
        IlSourceLinkProvider.Instructions = null;
        _companionDisassemblers.Clear();
        _companionStringExtractor = null;
        _companionUiFor = null;
    }

    /// <summary>Rebuilds the companion-scoped UI helpers when the attached set changes.</summary>
    private void EnsureCompanionUi()
    {
        var set = Analyzer.PreIlcCompanions;
        if (ReferenceEquals(_companionUiFor, set)) return;

        _companionDisassemblers.Clear();
        _companionStringExtractor = set is null ? null : new StringExtractor(set.Root);
        _companionUiFor = set;
    }

    /// <summary>
    /// The pre-ILC local reference that owns <paramref name="analyzer"/>, or null when it
    /// is the root, the current analyzer, or not a member of the attached set at all.
    /// </summary>
    private AssemblyAnalyzer? OwnerOf(AssemblyAnalyzer analyzer) =>
        Analyzer.PreIlcCompanions is { } set
        && !ReferenceEquals(analyzer, set.Root)
        && set.All.Contains(analyzer)
            ? analyzer
            : null;

    /// <summary>
    /// Clears pre-ILC view state that follows the current analyzer: the built index
    /// reference, the tree-source toggle, and the companion UI helpers. The attachment
    /// itself lives on the analyzer — it survives pushes and reactivates on pop-back.
    /// </summary>
    internal void ResetPreIlcViewState()
    {
        IlAotTreeNativeView = false;
        PreIlcIndex = null;
        PreIlcIndexBuildInProgress = false;
        _companionDisassemblers.Clear();
        _companionStringExtractor = null;
        _companionUiFor = null;
        ClearPairPaneState();
    }

    // --- Dynamic Analysis Tab State ---

    /// <summary>Whether the assembly has a CLR entry point (executable, not library).</summary>
    public bool HasEntryPoint => Analyzer.ClrHeader is { EntryPointToken: > 0 };

    /// <summary>
    /// Whether the binary is Native AOT compiled .NET — a validated ReadyToRun
    /// header with no CLR metadata.
    /// </summary>
    public bool IsNativeAot => Analyzer.BinaryKind == BinaryKind.NativeAot;

    /// <summary>Whether the binary has no CLR metadata at all (Native AOT or unknown native).</summary>
    public bool IsNativeBinary => !Analyzer.HasMetadata || Analyzer.ClrHeader is null;

    /// <summary>
    /// Whether the assembly targets .NET Framework (not .NET Core / .NET 5+). True when the
    /// <c>TargetFrameworkAttribute</c> says so OR the binder built a
    /// <see cref="NetFxBindingContext"/> for it (catches CLR 2 roots that carry no TFA).
    /// </summary>
    public bool IsNetFramework =>
        Analyzer.TargetFramework?.StartsWith(".NETFramework", StringComparison.OrdinalIgnoreCase) == true
        || RootNetFxBindingContext is not null;

    /// <summary>
    /// Human display string for the General-tab "Target Framework" line and the Dynamic-tab
    /// "Detected target" message. Falls back through:
    /// <list type="bullet">
    ///   <item>The real <c>TargetFrameworkAttribute</c> value if present.</item>
    ///   <item>An inferred-CLR2 label when the binder detected CLR 2 from the mscorlib reference
    ///     and the assembly carries no TFA.</item>
    ///   <item><c>"(unknown)"</c> otherwise.</item>
    /// </list>
    /// </summary>
    public string EffectiveTargetFrameworkDisplay =>
        Analyzer.TargetFramework
        ?? (RootNetFxBindingContext is { IsRuntimeVersionInferred: true, RuntimeVersion: NetFxRuntimeVersion.Clr2 }
            ? "CLR v2.0 (.NET Framework 2.0–3.5, inferred from mscorlib reference)"
            : "(unknown)");

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
    /// Stack of cross-view back targets, top-first. Pushed by
    /// <see cref="NavigateToIlMethod"/> / <see cref="NavigateToHexOffset"/>, popped by
    /// <see cref="NavigateBack"/>, cleared by <c>ResetViewState</c>. Each frame stores
    /// the originating <c>(Tab, SubTab)</c> so chained jumps unwind one step at a time.
    /// </summary>
    public Stack<(int Tab, int SubTab)> CrossViewBackStack { get; } = new();

    /// <summary>
    /// Top of <see cref="CrossViewBackStack"/>, or null if the stack is empty. Used by
    /// the unified Esc binding gate, the hint bar, and existing tests as the "current
    /// back target" projection.
    /// </summary>
    public (int Tab, int SubTab)? CrossViewBackTarget =>
        CrossViewBackStack.Count > 0 ? CrossViewBackStack.Peek() : null;

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
        CrossViewBackStack.Push((CurrentTab, PeSubTab));

        // Expand namespace and type in the IL tree (routed: PE tables answer from the
        // pre-ILC root when attached, so the method belongs to the metadata analyzer).
        ExpandIlTreeForMethod(method, owner: null);

        IlSelectedMethod = method;
        IlSelectedMethodOwner = null;
        SetIlFocusedTreeKey($"method:{method.Token}");

        NavigateToTab(TabId.IlInspector);
        var ilSearch = Search[TabId.IlInspector];
        ilSearch.Reset();
        IlFocusedPane = IlPane.Tree;
        App.RequestFocus(node => node is ScrollPanelNode);
        App.Invalidate();
        RequestExtraFrame();
    }

    /// <summary>
    /// Navigates to the Hex Dump tab, jumping to the file offset corresponding to the given RVA.
    /// </summary>
    public void NavigateToHexOffset(int rva)
    {
        var fileOffset = RvaToFileOffset(rva);
        if (fileOffset < 0) return;

        CrossViewBackStack.Push((CurrentTab, PeSubTab));

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
        RequestExtraFrame();
    }

    /// <summary>Navigates to the Hex Dump tab, jumping directly to a file offset (native mode).</summary>
    /// <param name="fileOffset">The file offset to jump to.</param>
    public void NavigateToHexFileOffset(long fileOffset)
    {
        if (fileOffset < 0) return;

        CrossViewBackStack.Push((CurrentTab, PeSubTab));

        var doc = HexEditorState.Document;
        if (fileOffset < doc.ByteCount)
        {
            var byteMap = doc.GetByteMap();
            var (charIdx, _) = byteMap.ByteToChar((int)fileOffset);
            HexEditorState.SetCursorPosition(new Hex1b.Documents.DocumentOffset(charIdx));
            HexEditorState.ByteCursorOffset = (int)fileOffset;
            HexScrollTarget = fileOffset;
        }

        NavigateToTab(TabId.HexDump);
        App.RequestFocus(node => node is EditorNode);
        App.Invalidate();
        RequestExtraFrame();
    }

    /// <summary>
    /// Selects a native symbol in the IL-inspector native mode, pushing the current view onto the
    /// native back stack (for Esc) and expanding the tree path so the target is visible. This is the
    /// go-to-definition landing for a resolved call/branch target.
    /// </summary>
    /// <param name="symbol">The native symbol to navigate to.</param>
    public void NavigateToNativeSymbol(NativeSymbol symbol)
    {
        // Capture the outgoing editor instance (with its cursor and scroll) so Esc lands back exactly
        // where the jump departed from — the same model as the managed IlBackEntry.
        if (IlSelectedNativeSymbol is { } current && IlEditorState is { } editor
            && IlEditorNativeSymbol?.VirtualAddress == current.VirtualAddress)
        {
            IlNativeBackStack.Push(new NativeBackEntry(
                current, editor, IlEditorKey, IlNativeInstructions, IlNativeHeaderLineCount,
                IlFocusedTreeKey, new Dictionary<string, bool>(IlTreeExpansionState),
                editor.Cursor.Position.Value));
        }

        ExpandNativeTreePath(symbol);
        IlSelectedNativeSymbol = symbol;
        SetIlFocusedTreeKey($"nfunc:{symbol.VirtualAddress:x}");
        App.Invalidate();
    }

    /// <summary>Restores the native IL-inspector view from a back-stack entry on Esc.</summary>
    /// <param name="entry">The entry to restore.</param>
    public void RestoreFromNativeBackEntry(NativeBackEntry entry)
    {
        // Cache the outgoing editor before swapping it out (mirrors managed RestoreFromIlBackEntry).
        if (IlEditorKey is not null && IlEditorState is not null)
            IlCachedEditors[IlEditorKey] = IlEditorState;

        IlSelectedNativeSymbol = entry.Symbol;
        SetIlFocusedTreeKey(entry.FocusedTreeKey);
        IlTreeExpansionState.Clear();
        foreach (var (k, v) in entry.TreeExpansionState)
            IlTreeExpansionState[k] = v;

        // Restore the exact editor instance and mark it current so BuildNativeEditorPane reuses it
        // instead of rebuilding — this is what preserves cursor and scroll across the round-trip.
        IlEditorState = entry.EditorState;
        IlEditorNativeSymbol = entry.Symbol;
        IlEditorMethod = null;
        IlEditorField = null;
        IlEditorAnalyzer = Analyzer;
        IlEditorKey = entry.EditorKey;
        if (entry.EditorKey is not null)
        {
            IlNativeEditorKeyCache[(Analyzer, entry.Symbol.VirtualAddress)] = entry.EditorKey;
            IlCachedEditors.Remove(entry.EditorKey);
        }

        IlNativeInstructions = entry.Instructions;
        IlNativeHeaderLineCount = entry.HeaderLineCount;
        IlNativeSyntaxProvider.Instructions = entry.Instructions;
        IlNativeNavigationProvider.Instructions = entry.Instructions;

        IlEditorState.SetCursorPosition(new Hex1b.Documents.DocumentOffset(entry.CursorOffset));

        App.RequestFocus(node => node is EditorNode);
        App.Invalidate();
    }

    private void ExpandNativeTreePath(NativeSymbol symbol)
    {
        string ns, type;
        if (symbol.Kind == NativeSymbolKind.Function && symbol.ManagedName is { } managed)
        {
            var parsed = Core.Analysis.Disasm.NativeSymbolName.Parse(managed);
            ns = parsed.Namespace.Length == 0 ? "(global)" : parsed.Namespace;
            type = parsed.TypeName.Length == 0 ? "(functions)" : parsed.TypeName;
        }
        else
        {
            ns = type = symbol.Kind == NativeSymbolKind.Stub ? "(stubs)"
                : symbol.Kind == NativeSymbolKind.Function ? "(runtime)" : "(functions)";
        }

        IlTreeExpansionState[$"nns:{ns}"] = true;
        IlTreeExpansionState[$"ntype:{ns}/{type}"] = true;
    }

    /// <summary>
    /// Navigates to the definition of the IL instruction's metadata token.
    /// </summary>
    /// <param name="token">The metadata token from the IL instruction.</param>
    /// <returns>True if navigation occurred.</returns>
    public bool NavigateToIlDefinition(int token)
    {
        // IlEditorMethod reflects the currently-open method body; fall back to the
        // list selection when the editor hasn't loaded yet. Tokens resolve in the
        // analyzer that produced the open listing — the routed metadata analyzer, or the
        // pre-ILC local reference that owns the method.
        var sourceAnalyzer = IlEditorAnalyzer ?? MetadataAnalyzer;
        var owner = OwnerOf(sourceAnalyzer);
        var prefix = owner is null ? "" : $"{owner.AssemblyName ?? owner.FileName}|";
        var target = IlNavigationResolver.Resolve(
            sourceAnalyzer, token, IlEditorMethod ?? IlSelectedMethod);
        switch (target)
        {
            case IlNavigationTarget.LocalMethod(var method):
                if (method.Token == IlSelectedMethod?.Token
                    && ReferenceEquals(owner, IlSelectedMethodOwner))
                {
                    return false;
                }

                PushIlBackEntry(false);
                IlSelectedMethod = method;
                IlSelectedMethodOwner = owner;
                IlSelectedField = null;
                ExpandIlTreeForMethod(method, owner);
                SetIlFocusedTreeKey($"method:{prefix}{method.Token}");
                App.RequestFocus(node => node is EditorNode);
                App.Invalidate();
                return true;

            case IlNavigationTarget.LocalType(var type):
                PushIlBackEntry(false);
                // Clear the method/editor selection so the right pane stops showing
                // the IL we just left — otherwise tree focus moves but the editor
                // still renders the previous method and the nav looks half-applied.
                IlSelectedMethod = null;
                IlSelectedMethodOwner = null;
                IlSelectedField = null;
                IlEditorState = null;
                IlEditorMethod = null;
                IlEditorAnalyzer = null;
                IlEditorField = null;
                IlTreeExpansionState[$"ns:{prefix}{(!string.IsNullOrEmpty(type.Namespace) ? type.Namespace : "(global)")}"] = true;
                SetIlFocusedTreeKey($"type:{prefix}{type.FullName}");
                App.RequestFocus(node => node is ScrollPanelNode);
                App.Invalidate();
                return true;

            case IlNavigationTarget.LocalField(var field, var dt):
                PushIlBackEntry(false);
                IlSelectedMethod = null;
                IlSelectedMethodOwner = null;
                IlSelectedField = field;
                IlEditorState = null;
                IlEditorMethod = null;
                IlEditorAnalyzer = null;
                IlTreeExpansionState[$"ns:{prefix}{(!string.IsNullOrEmpty(dt.Namespace) ? dt.Namespace : "(global)")}"] = true;
                IlTreeExpansionState[$"type:{prefix}{dt.FullName}"] = true;
                SetIlFocusedTreeKey($"type:{prefix}{dt.FullName}");
                App.RequestFocus(node => node is ScrollPanelNode);
                App.Invalidate();
                return true;

            case IlNavigationTarget.ExternalMethod(var memberName, var extDeclType, var signature, var assemblyName):
                return NavigateToExternalMethod(sourceAnalyzer, assemblyName, memberName, signature, extDeclType);

            case IlNavigationTarget.ExternalType(var typeRef, var assemblyName):
                return NavigateToExternalType(sourceAnalyzer, assemblyName, typeRef);

            case IlNavigationTarget.ExternalField(var fieldName, var extFieldDeclType, var assemblyName):
                return NavigateToExternalField(sourceAnalyzer, assemblyName, fieldName, extFieldDeclType);

            case IlNavigationTarget.GenericInstantiation(_, var reason):
                ShowTransientNotice($"Cannot decode generic instantiation: {reason}");
                return false;

            case IlNavigationTarget.Unsupported(_, var reason):
                ShowTransientNotice(reason);
                return false;

            case IlNavigationTarget.Unresolved(_, var reason):
                ShowTransientNotice(reason);
                return false;

            default:
                return false;
        }
    }

    /// <summary>
    /// Restores IL inspector state from a back entry, forcing the editor to recreate.
    /// </summary>
    /// <param name="entry">The back entry to restore from.</param>
    internal void RestoreFromIlBackEntry(IlBackEntry entry)
    {
        // Save outgoing editor to cache before PopAssembly may clear it
        if (IlEditorKey is not null && IlEditorState is not null)
        {
            IlCachedEditors[IlEditorKey] = IlEditorState;
        }

        if (entry.CrossAssembly && NavigationStack.Count > 0)
        {
            PopAssembly(); // Calls ResetViewState → clears IlEditor*Cache + CrossViewBackStack + Treemap* + Search
            CrossViewBackStack.Clear();
            for (var i = entry.PreviousCrossViewBackStack.Count - 1; i >= 0; i--)
                CrossViewBackStack.Push(entry.PreviousCrossViewBackStack[i]);
            if (entry.PreviousTreemapState is { } tm)
            {
                CachedSizeTree = tm.CachedTree;
                TreemapCurrentLevel = tm.CurrentLevel;
                TreemapBreadcrumb.Clear();
                for (var i = tm.BreadcrumbTopFirst.Count - 1; i >= 0; i--)
                    TreemapBreadcrumb.Push(tm.BreadcrumbTopFirst[i]);
                TreemapSelectedIndex = tm.SelectedIndex;
                TreemapMatchIndex = tm.MatchIndex;
                Search[TabId.SizeMap].RestoreFrom(
                    tm.SearchQuery, tm.SearchIsActive, tm.SearchIsConfirmed, tm.SearchMatchCount);
            }
        }

        IlSelectedMethod = entry.Method;
        // The entry's analyzer identifies the owner: a pre-ILC local reference restores
        // as such; the root (or a plain analyzer) restores as the metadata default.
        IlSelectedMethodOwner = OwnerOf(entry.EditorAnalyzer);
        IlSelectedField = null;
        IlEditorState = entry.EditorState;
        IlEditorMethod = entry.EditorMethod;
        IlEditorAnalyzer = entry.EditorAnalyzer;
        IlEditorField = null;
        SetIlFocusedTreeKey(entry.FocusedTreeKey);
        IlTreeExpansionState.Clear();
        foreach (var (k, v) in entry.TreeExpansionState)
            IlTreeExpansionState[k] = v;

        // Restore editor identity key and reseed the cache so future
        // GetOrCreateEditorKey calls return the same key object.
        IlEditorKey = entry.EditorKey;
        if (entry.EditorKey is not null)
        {
            IlEditorKeyCache[(entry.EditorAnalyzer, entry.EditorMethod.Token)] = entry.EditorKey;
            IlCachedEditors.Remove(entry.EditorKey);
        }

        // Restore instruction list for navigation decorations — from the analyzer that
        // owns the restored method, which may be a pre-ILC companion.
        if (GetMetadataIlDisassembler(entry.EditorAnalyzer) is { } restoredDisassembler)
        {
            var r = restoredDisassembler.DisassembleWithText(entry.Method);
            IlInstructions = r?.Instructions;
            IlHeaderLineCount = r?.HeaderLineCount ?? 0;
            IlNavigationProvider.Instructions = IlInstructions;
            IlNavigationProvider.HeaderLineCount = IlHeaderLineCount;
            IlSourceLinkProvider.Instructions = IlInstructions;
        }

        App.RequestFocus(node => node is EditorNode);
        App.Invalidate();
    }

    /// <summary>
    /// Sets <see cref="IlFocusedTreeKey"/> programmatically, arms a one-shot
    /// scroll-into-view for the next IL Inspector render, and wakes the render loop
    /// so the consumer in <see cref="Views.IlInspectorView.Build"/> runs. Use this
    /// at every non-user-driven mutation site (cross-view jumps, search match
    /// navigation, navigation back). Keyboard handlers inside the tree assign
    /// <see cref="IlFocusedTreeKey"/> directly so wheel-scrolled selections do not
    /// snap back into the viewport on repaint.
    /// </summary>
    internal void SetIlFocusedTreeKey(object? key)
    {
        IlFocusedTreeKey = key;
        IlScrollSelectionIntoViewPending = true;
        App.Invalidate();
        // The immediate invalidate can race an in-flight frame and be drained by the
        // Hex1b main loop without producing a build; the nudger guarantees one runs.
        RequestExtraFrame();
    }

    /// <summary>
    /// Records the start of a widget build and releases the extra-frame request slot for work
    /// produced by that build.
    /// </summary>
    internal void NotifyBuildStarted()
    {
        lock (_graphBuildLock)
        {
            if (_disposed)
                return;

            unchecked { BuildGeneration++; }
            ExtraFrameArmed = false;
        }
    }

    /// <summary>
    /// Guarantees a follow-up widget build. An <see cref="Hex1bApp.Invalidate"/> that
    /// lands while a frame is still rendering is drained by the Hex1b main loop's
    /// frame-rate guard without producing another frame — and the frame in flight can
    /// be arbitrarily slow (a first IL render disassembles the selected method). The
    /// lifetime-owned nudger keeps invalidating on a short period until it observes
    /// <see cref="BuildGeneration"/> advance (a new build ran), its attempt budget
    /// runs out, or this state is disposed. Each build re-arms eligibility.
    /// </summary>
    internal void RequestExtraFrame()
    {
        lock (_graphBuildLock)
            RequestExtraFrameUnderLock();
    }

    private void RequestExtraFrameUnderLock()
    {
        if (_disposed || ExtraFrameArmed)
            return;

        ExtraFrameArmed = true;
        RemoveCompletedRenderNudgers();

        var generation = BuildGeneration;
        var cancellationToken = _lifetimeCancellation.Token;
        _renderNudgerTasks.Add(Task.Run(
            () => NudgeExtraFramesAsync(generation, cancellationToken),
            CancellationToken.None));
    }

    private async Task NudgeExtraFramesAsync(int generation, CancellationToken cancellationToken)
    {
        try
        {
            for (var attempt = 0; attempt < 50; attempt++)
            {
                await Task.Delay(16, cancellationToken).ConfigureAwait(false);

                lock (_graphBuildLock)
                {
                    if (_disposed || cancellationToken.IsCancellationRequested)
                        return;

                    // The first nudge is unconditional: a caller racing the top of a root
                    // build can capture that build's generation, and a build already in
                    // flight when the request was made cannot have honored it. One extra
                    // frame at worst; later ticks stop as soon as a new build runs.
                    if (attempt > 0 && BuildGeneration != generation)
                        return;
                }

                if (cancellationToken.IsCancellationRequested)
                    return;

                App.Invalidate();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void RemoveCompletedRenderNudgers() =>
        _renderNudgerTasks.RemoveAll(static task => task.IsCompleted);

    /// <summary>
    /// Returns a stable identity key for the given method/field token within an analyzer.
    /// Same (analyzer, token) pair always returns the same reference, which is required
    /// by <see cref="StatePanelWidget"/> reference-equality matching.
    /// </summary>
    internal object GetOrCreateEditorKey(AssemblyAnalyzer analyzer, int token)
    {
        var cacheKey = (analyzer, token);
        if (!IlEditorKeyCache.TryGetValue(cacheKey, out var key))
        {
            key = new object();
            IlEditorKeyCache[cacheKey] = key;
        }

        return key;
    }

    /// <summary>
    /// Returns a stable identity key for a native symbol's editor within an analyzer, keyed by the
    /// full 64-bit virtual address (never truncated). Same (analyzer, address) always returns the same
    /// reference, as <see cref="StatePanelWidget"/> reference-equality matching requires.
    /// </summary>
    internal object GetOrCreateNativeEditorKey(AssemblyAnalyzer analyzer, ulong address)
    {
        var cacheKey = (analyzer, address);
        if (!IlNativeEditorKeyCache.TryGetValue(cacheKey, out var key))
        {
            key = new object();
            IlNativeEditorKeyCache[cacheKey] = key;
        }

        return key;
    }

    /// <summary>
    /// Shows a transient notice in the hints bar that auto-clears after 3 seconds.
    /// </summary>
    /// <param name="message">The notice message to display.</param>
    public void ShowTransientNotice(string message)
    {
        TransientNotice = message;
        var gen = ++TransientNoticeGeneration;
        App.Invalidate();
        _ = Task.Delay(TimeSpan.FromSeconds(3)).ContinueWith(_ =>
        {
            if (TransientNoticeGeneration == gen)
            {
                TransientNotice = null;
                App.Invalidate();
            }
        }, TaskScheduler.Default);
    }

    private void PushIlBackEntry(bool crossAssembly)
    {
        if (IlSelectedMethod is null || IlEditorState is null
            || IlEditorMethod is null || IlEditorAnalyzer is null)
        {
            return;
        }

        TreemapBackState? treemapSnapshot = null;
        if (crossAssembly)
        {
            var smSearch = Search[TabId.SizeMap];
            treemapSnapshot = new TreemapBackState(
                TreemapCurrentLevel,
                [.. TreemapBreadcrumb],
                TreemapSelectedIndex,
                TreemapMatchIndex,
                CachedSizeTree,
                smSearch.Query,
                smSearch.IsActive,
                smSearch.IsConfirmed,
                smSearch.MatchCount);
        }

        IlBackStack.Push(new IlBackEntry(
            IlSelectedMethod, IlEditorState, IlEditorMethod, IlEditorAnalyzer,
            IlFocusedTreeKey, new Dictionary<string, bool>(IlTreeExpansionState), crossAssembly,
            IlEditorKey,
            [.. CrossViewBackStack],
            treemapSnapshot));
    }

    private void ExpandIlTreeForMethod(MethodDefInfo method) =>
        ExpandIlTreeForMethod(method, owner: null);

    private void ExpandIlTreeForMethod(MethodDefInfo method, AssemblyAnalyzer? owner)
    {
        var source = owner ?? MetadataAnalyzer;
        var prefix = owner is null ? "" : $"{owner.AssemblyName ?? owner.FileName}|";
        var typeDef = source.TypeDefs.FirstOrDefault(t => t.FullName == method.DeclaringType);
        var ns = typeDef is not null && !string.IsNullOrEmpty(typeDef.Namespace)
            ? typeDef.Namespace : "(global)";

        // Multi-assembly pre-ILC trees group under assembly rows — expand the owner's.
        if (Analyzer.PreIlcCompanions is { LocalReferences.Count: > 0 } set)
        {
            var member = owner ?? set.Root;
            IlTreeExpansionState[$"asm:{member.AssemblyName ?? member.FileName}"] = true;
        }

        IlTreeExpansionState[$"ns:{prefix}{ns}"] = true;
        IlTreeExpansionState[$"type:{prefix}{method.DeclaringType}"] = true;
    }

    /// <summary>
    /// Selects a pre-ILC method from the native pair pane's go-to-definition, pushing a
    /// managed back entry so Esc returns to the departure method, and expanding the tree
    /// path (assembly-prefixed for local references).
    /// </summary>
    /// <param name="method">The correlated managed method to select.</param>
    /// <param name="owner">The defining analyzer when it is a local reference; null for the root.</param>
    public void NavigateToPreIlcMethod(MethodDefInfo method, AssemblyAnalyzer? owner)
    {
        PushIlBackEntry(crossAssembly: false);
        IlSelectedMethod = method;
        IlSelectedMethodOwner = owner;
        ExpandIlTreeForMethod(method, owner);
        var prefix = owner is null ? "" : $"{owner.AssemblyName ?? owner.FileName}|";
        SetIlFocusedTreeKey($"method:{prefix}{method.Token}");
        App.Invalidate();
    }

    private bool NavigateToExternalMethod(AssemblyAnalyzer sourceAnalyzer, string assemblyName,
        string memberName, string signature, string? declaringType = null)
    {
        // Resolve from the assembly that owns the open IL — a local-reference companion in a
        // multi-assembly set has its own directory, TFM, runtime pack, and bundle context.
        var resolved = ImplementationAssemblyResolver.Resolve(
            sourceAnalyzer.FilePath, assemblyName, declaringType,
            sourceAnalyzer.TargetFramework, sourceAnalyzer.PreferredRuntimePack, sourceAnalyzer.SourceBundlePath,
            RootNetFxBindingContext, sourceAnalyzer);
        if (resolved is null)
        {
            ShowTransientNotice($"Cannot resolve assembly: {assemblyName}");
            return false;
        }

        AssemblyAnalyzer probe;
        try
        {
            probe = resolved switch
            {
                ResolvedAssembly.FromFile(var p) => new AssemblyAnalyzer(p),
                ResolvedAssembly.FromBundle(var b, var n, var bp) => new AssemblyAnalyzer(b, n, sourceBundlePath: bp),
                ResolvedModule module => new AssemblyAnalyzer(
                    [.. module.Bytes],
                    module.Path,
                    sourceBundlePath: null,
                    displayName: Path.GetFileName(module.Path),
                    targetFrameworkOverride: module.TargetFramework,
                    preferredRuntimePackOverride: module.PreferredRuntimePack),
                _ => throw new InvalidOperationException()
            };
        }
        catch
        {
            ShowTransientNotice($"Cannot open resolved assembly for {assemblyName}");
            return false;
        }

        // Filter by declaring type first to avoid cross-type name collisions.
        // Always scope to declaring type when available — don't fall back to unscoped.
        List<MethodDefInfo> candidates;
        if (declaringType is not null)
        {
            candidates = [.. probe.MethodDefs.Where(m =>
                m.Name == memberName && m.DeclaringType == declaringType)];
        }
        else
        {
            candidates = [.. probe.MethodDefs.Where(m => m.Name == memberName)];
        }

        MethodDefInfo? methodTarget = candidates.Count == 1 ? candidates[0]
            : candidates.Count > 1 && !string.IsNullOrEmpty(signature)
                ? (candidates.FirstOrDefault(m => m.Signature == signature) ?? candidates[0])
            : candidates.Count > 0 ? candidates[0] : null;
        if (methodTarget is null)
        {
            probe.Dispose();
            ShowTransientNotice($"Method {memberName} not found in {assemblyName}");
            return false;
        }

        PushIlBackEntry(true);
        PushAssemblyDirect(probe);
        IlSelectedMethod = methodTarget;
        ExpandIlTreeForMethod(methodTarget);
        SetIlFocusedTreeKey($"method:{methodTarget.Token}");
        NavigateToTab(TabId.IlInspector);
        App.RequestFocus(node => node is EditorNode);
        App.Invalidate();
        return true;
    }

    private bool NavigateToExternalType(AssemblyAnalyzer sourceAnalyzer, string assemblyName, TypeRefInfo typeRef)
    {
        var resolved = ImplementationAssemblyResolver.Resolve(
            sourceAnalyzer.FilePath, assemblyName, typeRef.FullName,
            sourceAnalyzer.TargetFramework, sourceAnalyzer.PreferredRuntimePack, sourceAnalyzer.SourceBundlePath,
            RootNetFxBindingContext, sourceAnalyzer);
        if (resolved is null)
        {
            ShowTransientNotice($"Cannot resolve assembly: {assemblyName}");
            return false;
        }

        AssemblyAnalyzer probe;
        try
        {
            probe = resolved switch
            {
                ResolvedAssembly.FromFile(var p) => new AssemblyAnalyzer(p),
                ResolvedAssembly.FromBundle(var b, var n, var bp) => new AssemblyAnalyzer(b, n, sourceBundlePath: bp),
                ResolvedModule module => new AssemblyAnalyzer(
                    [.. module.Bytes],
                    module.Path,
                    sourceBundlePath: null,
                    displayName: Path.GetFileName(module.Path),
                    targetFrameworkOverride: module.TargetFramework,
                    preferredRuntimePackOverride: module.PreferredRuntimePack),
                _ => throw new InvalidOperationException()
            };
        }
        catch
        {
            ShowTransientNotice($"Cannot open resolved assembly for {assemblyName}");
            return false;
        }

        var typeTarget = probe.TypeDefs.FirstOrDefault(t => t.FullName == typeRef.FullName);
        if (typeTarget is null)
        {
            probe.Dispose();
            ShowTransientNotice($"Type {typeRef.Name} not found");
            return false;
        }

        PushIlBackEntry(true);
        PushAssemblyDirect(probe);
        IlTreeExpansionState[$"ns:{(!string.IsNullOrEmpty(typeTarget.Namespace) ? typeTarget.Namespace : "(global)")}"] = true;
        SetIlFocusedTreeKey($"type:{typeTarget.FullName}");
        NavigateToTab(TabId.IlInspector);
        App.RequestFocus(node => node is ScrollPanelNode);
        App.Invalidate();
        return true;
    }

    private bool NavigateToExternalField(AssemblyAnalyzer sourceAnalyzer, string assemblyName,
        string fieldName, string? declaringType = null)
    {
        var resolved = ImplementationAssemblyResolver.Resolve(
            sourceAnalyzer.FilePath, assemblyName, declaringType,
            sourceAnalyzer.TargetFramework, sourceAnalyzer.PreferredRuntimePack, sourceAnalyzer.SourceBundlePath,
            RootNetFxBindingContext, sourceAnalyzer);
        if (resolved is null)
        {
            ShowTransientNotice($"Cannot resolve assembly: {assemblyName}");
            return false;
        }

        AssemblyAnalyzer probe;
        try
        {
            probe = resolved switch
            {
                ResolvedAssembly.FromFile(var p) => new AssemblyAnalyzer(p),
                ResolvedAssembly.FromBundle(var b, var n, var bp) => new AssemblyAnalyzer(b, n, sourceBundlePath: bp),
                ResolvedModule module => new AssemblyAnalyzer(
                    [.. module.Bytes],
                    module.Path,
                    sourceBundlePath: null,
                    displayName: Path.GetFileName(module.Path),
                    targetFrameworkOverride: module.TargetFramework,
                    preferredRuntimePackOverride: module.PreferredRuntimePack),
                _ => throw new InvalidOperationException()
            };
        }
        catch
        {
            ShowTransientNotice($"Cannot open resolved assembly for {assemblyName}");
            return false;
        }

        // Scope field lookup by declaring type when available
        FieldDefInfo? fieldTarget = null;
        if (declaringType is not null)
        {
            fieldTarget = probe.FieldDefs.FirstOrDefault(f =>
                f.Name == fieldName && f.DeclaringType == declaringType);
        }
        else
        {
            fieldTarget = probe.FieldDefs.FirstOrDefault(f => f.Name == fieldName);
        }

        if (fieldTarget is null)
        {
            probe.Dispose();
            ShowTransientNotice($"Field {fieldName} not found");
            return false;
        }

        var dt = probe.TypeDefs.FirstOrDefault(t => t.FullName == fieldTarget.DeclaringType);
        if (dt is null)
        {
            probe.Dispose();
            return false;
        }

        PushIlBackEntry(true);
        PushAssemblyDirect(probe);
        IlSelectedField = fieldTarget;
        IlTreeExpansionState[$"ns:{(!string.IsNullOrEmpty(dt.Namespace) ? dt.Namespace : "(global)")}"] = true;
        IlTreeExpansionState[$"type:{dt.FullName}"] = true;
        SetIlFocusedTreeKey($"type:{dt.FullName}");
        NavigateToTab(TabId.IlInspector);
        App.RequestFocus(node => node is ScrollPanelNode);
        App.Invalidate();
        return true;
    }

    /// <summary>
    /// Pushes a pre-constructed analyzer onto the navigation stack.
    /// </summary>
    /// <param name="analyzer">The analyzer to push.</param>
    internal void PushAssemblyDirect(AssemblyAnalyzer analyzer)
    {
        NavigationError = null;
        _focusedDepStack.Push(GeneralFocusedDep);
        _tabStack.Push(CurrentTab);
        _graphSelectionStack.Push(GraphSelectedIndex);
        ResetDependencyGraphForAnalyzerReplacement();
        NavigationStack.Push(Analyzer);
        Analyzer = analyzer;
        StringExtractor = new StringExtractor(Analyzer);
        IlDisassembler = Analyzer.HasMetadata ? new IlDisassembler(Analyzer) : null;
        var hexDoc = new HexRowDocument(new Hex1bDocument(Analyzer.RawBytes.ToArray()));
        HexRowDoc = hexDoc;
        HexEditorState = new EditorState(hexDoc) { IsReadOnly = true };
        HexCleanVersion = hexDoc.Version;
        ResetViewState();
    }

    /// <summary>
    /// Returns to the tab saved by the last cross-view navigation.
    /// </summary>
    public void NavigateBack()
    {
        if (CrossViewBackStack.Count == 0) return;

        var (Tab, SubTab) = CrossViewBackStack.Pop();
        NavigateToTab(Tab);
        if (Tab == TabId.PeMetadata)
            PeSubTab = SubTab;
        // Route through RequestContentFocus so each tab's content focusable stays the
        // single source of truth — IL → ScrollPanelNode, Hex → EditorNode, etc.
        RequestContentFocus();
        App.Invalidate();
        RequestExtraFrame();
    }

    /// <summary>
    /// Requests that the primary content widget (table, editor, tree, etc.) receives focus
    /// after the next render. IL tab targets the ListNode tree; all other tabs target any
    /// content node including TableNode.
    /// </summary>
    public void RequestContentFocus()
    {
        if (CurrentTab == TabId.IlInspector)
            App.RequestFocus(node => node is ScrollPanelNode);
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
        ResetDependencyGraphForAnalyzerReplacement();
        NavigationStack.Push(Analyzer);
        Analyzer = newAnalyzer;
        StringExtractor = new StringExtractor(Analyzer);
        IlDisassembler = Analyzer.HasMetadata ? new IlDisassembler(Analyzer) : null;
        var hexDoc = new HexRowDocument(new Hex1bDocument(Analyzer.RawBytes.ToArray()));
        HexRowDoc = hexDoc;
        HexEditorState = new EditorState(hexDoc) { IsReadOnly = true };
        HexCleanVersion = hexDoc.Version;
        ResetViewState();
        // Normal assembly push (dependency navigation) invalidates any IL back entries
        // because they reference the old analyzer's methods/editor state.
        IlBackStack.Clear();
        return true;
    }

    /// <summary>
    /// Pushes a resolved assembly or authenticated sibling module onto the navigation stack.
    /// </summary>
    /// <param name="resolved">The resolved assembly or module to push.</param>
    /// <returns>True if the target was pushed successfully; false on error or depth limit.</returns>
    public bool PushAssembly(ResolvedAssembly resolved)
    {
        if (NavigationStack.Count >= MaxNavigationDepth)
        {
            NavigationError = $"Navigation depth limit reached ({MaxNavigationDepth})";
            return false;
        }

        AssemblyAnalyzer newAnalyzer;
        try
        {
            newAnalyzer = resolved switch
            {
                ResolvedAssembly.FromFile(var path) => new AssemblyAnalyzer(path),
                ResolvedAssembly.FromBundle(var bytes, var name, var bundle) =>
                    new AssemblyAnalyzer(bytes, name, sourceBundlePath: bundle),
                ResolvedModule module => new AssemblyAnalyzer(
                    [.. module.Bytes],
                    module.Path,
                    sourceBundlePath: null,
                    displayName: Path.GetFileName(module.Path),
                    targetFrameworkOverride: module.TargetFramework,
                    preferredRuntimePackOverride: module.PreferredRuntimePack),
                _ => throw new ArgumentException("Unknown resolution type")
            };
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
        ResetDependencyGraphForAnalyzerReplacement();
        NavigationStack.Push(Analyzer);
        Analyzer = newAnalyzer;
        StringExtractor = new StringExtractor(Analyzer);
        IlDisassembler = Analyzer.HasMetadata ? new IlDisassembler(Analyzer) : null;
        var hexDoc = new HexRowDocument(new Hex1bDocument(Analyzer.RawBytes.ToArray()));
        HexRowDoc = hexDoc;
        HexEditorState = new EditorState(hexDoc) { IsReadOnly = true };
        HexCleanVersion = hexDoc.Version;
        ResetViewState();
        IlBackStack.Clear();
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
        ResetDependencyGraphForAnalyzerReplacement();
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
        CrossViewBackStack.Clear();
        GeneralFocusedDep = Analyzer.AssemblyRefs.Count > 0 ? Analyzer.AssemblyRefs[0].Name : null;
        PeSubTab = 0;
        PeFocusedKey = null;
        PeDetailContent = null;
        SizeMapWhyContent = null;
        SetIlFocusedTreeKey(null);
        IlSelectedMethod = null;
        IlSelectedField = null;
        IlTreeExpansionState.Clear();
        IlEditorState = null;
        IlEditorMethod = null;
        IlEditorAnalyzer = null;
        IlEditorKey = null;
        IlEditorField = null;
        IlScrollPanelNode = null;
        IlScrollSelectionIntoViewPending = false;
        IlTreeScrollOffset = 0;
        IlTreeWindowViewport = 0;
        IlEditorKeyCache.Clear();
        IlNativeEditorKeyCache.Clear();
        IlCachedEditors.Clear();
        IlPrevSelectionAnchor = null;
        IlPrevCursorPosition = null;
        IlSearchMatches = [];
        IlCurrentMatchIndex = -1;
        IlLastSearchQuery = null;
        IlPendingCursorMatch = null;
        IlTextMatchMethodTokens = null;
        // IlBackStack is NOT cleared here — cross-assembly navigation pushes
        // a back entry BEFORE calling PushAssemblyDirect which calls ResetViewState.
        // Clearing it would wipe the entry needed for Esc back.
        IlInstructions = null;
        IlHeaderLineCount = 0;
        IlNavigationProvider.Instructions = null;
        IlSourceLinkProvider.Instructions = null;
        // Native IL-inspector mode mirrors the managed fields above; clear it too so a new binary
        // does not inherit the previous one's selected symbol, decoded listing, or back stack.
        IlSelectedNativeSymbol = null;
        IlEditorNativeSymbol = null;
        IlNativeInstructions = null;
        IlNativeHeaderLineCount = 0;
        IlNativeSyntaxProvider.Instructions = null;
        IlNativeNavigationProvider.Instructions = null;
        IlNativeBackStack.Clear();
        IlGdPending = false;
        ResetPreIlcViewState();
        TransientNotice = null;
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
        SizeMapWhyEditorState = null;
        SizeMapWhyEditorText = null;
        StringsDetailEditorState = null;
        StringsDetailEditorText = null;
        StringsSourceTab = 0;
        StringsFocusedKey = null;
        StringsDetailContent = null;
        CachedUserStrings = null;
        CachedMetadataStrings = null;
        CachedRawStrings = null;
        CachedRawUtf16Strings = null;
        CachedFrozenStrings = null;
        DepGraphScope = DependencyGraphScope.All;
        DepGraphHideFramework = false;
        DepGraphScrollY = 0;
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
            // #US and #Strings heaps are metadata: they answer from the pre-ILC companion
            // when one is attached (the native AOT image has neither).
            StringsSubTabId.UserStrings => CachedUserStrings ??= MetadataStringExtractor.ExtractUserStrings(),
            StringsSubTabId.Metadata => CachedMetadataStrings ??= MetadataStringExtractor.ExtractMetadataStrings(),
            StringsSubTabId.RawBinary => GetCachedRawStrings(),
            StringsSubTabId.RawBinaryUtf16 => GetCachedRawUtf16Strings(),
            StringsSubTabId.FrozenObject => CachedFrozenStrings ??= Analyzer.FrozenStrings,
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
        lock (_graphBuildLock)
        {
            if (_disposed)
                return;

            _disposed = true;
            _lifetimeCancellation.Cancel();
        }

        CancelAndDrainGraphBuild();
        CancelAndDrainRenderNudgers();
        if (_embeddedSourceTempFiles.IsValueCreated)
            _embeddedSourceTempFiles.Value.Dispose();
        _lifetimeCancellation.Dispose();
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

    private IReadOnlyList<StringEntry> GetCachedRawUtf16Strings()
    {
        if (CachedRawUtf16Strings is null || CachedRawUtf16StringsMinLength != StringsMinLength)
        {
            CachedRawUtf16Strings = StringExtractor.ExtractRawUtf16Strings(StringsMinLength);
            CachedRawUtf16StringsMinLength = StringsMinLength;
        }

        return CachedRawUtf16Strings;
    }

    /// <summary>
    /// Kicks off a background build of the transitive dependency graph when the Dep Graph
    /// view is first rendered for the current analyzer. While the build runs, the view
    /// displays a placeholder message; when the build completes, the result is published
    /// to <see cref="CachedGraph"/> and the UI is invalidated to trigger a re-render. Calls made
    /// while a build is already in flight or after the graph is cached are no-ops. If the analyzer
    /// changes before the build completes, the stale result is discarded.
    /// </summary>
    public void EnsureCachedGraphAsync()
    {
        lock (_graphBuildLock)
        {
            if (_disposed || GraphSnapshot is not null || GraphBuildInProgress ||
                _graphBuildFailed)
                return;

            _graphBuildCancellation?.Dispose();
            _graphBuildCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeCancellation.Token);
            GraphBuildInProgress = true;
            var capturedAnalyzer = Analyzer;
            var capturedGeneration = _graphBuildGeneration;
            var cancellation = _graphBuildCancellation;
            var graphBuilder = GraphBuilder;

            _graphBuildTask = Task.Factory.StartNew(
                () => BuildAndPublishGraph(
                    capturedAnalyzer,
                    capturedGeneration,
                    cancellation,
                    graphBuilder),
                CancellationToken.None,
                TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }
    }

    private void BuildAndPublishGraph(
        AssemblyAnalyzer capturedAnalyzer,
        int capturedGeneration,
        CancellationTokenSource cancellation,
        Func<AssemblyAnalyzer, CancellationToken, DependencyGraphResult> graphBuilder)
    {
        DependencyGraphResult? result = null;
        Exception? error = null;
        try
        {
            result = graphBuilder(capturedAnalyzer, cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            error = exception;
        }

        var snapshot = result is null
            ? null
            : new DependencyGraphSnapshot(result.Nodes, result.Edges, result.NavigationById);

        bool invalidate;
        lock (_graphBuildLock)
        {
            var ownsCurrentBuild = ReferenceEquals(_graphBuildCancellation, cancellation);
            var canPublish = ownsCurrentBuild &&
                capturedGeneration == _graphBuildGeneration &&
                ReferenceEquals(Analyzer, capturedAnalyzer) &&
                !_disposed &&
                !cancellation.IsCancellationRequested;

            if (canPublish && snapshot is not null)
            {
                Volatile.Write(ref _legacyGraphNavigation, snapshot.NavigationById);
                Volatile.Write(ref _dependencyGraphSnapshot, snapshot);
            }
            else if (canPublish && error is not null)
            {
                _graphBuildFailed = true;
                GraphNavigationError = "Cannot build dependency graph";
            }

            if (ownsCurrentBuild)
                GraphBuildInProgress = false;

            invalidate = canPublish;
        }

        if (error is not null)
            Debug.WriteLine($"Failed to build dependency graph: {error}");

        if (invalidate)
            InvalidateGraphCompletion();
    }

    private void InvalidateGraphCompletion()
    {
        lock (_graphBuildLock)
        {
            if (_disposed || _lifetimeCancellation.IsCancellationRequested)
                return;
        }

        App.Invalidate();
        RequestExtraFrame();
    }

    private void CancelAndDrainGraphBuild()
    {
        Task graphBuildTask;
        CancellationTokenSource? cancellation;
        lock (_graphBuildLock)
        {
            _graphBuildGeneration++;
            cancellation = _graphBuildCancellation;
            cancellation?.Cancel();
            graphBuildTask = _graphBuildTask;
        }

        graphBuildTask.GetAwaiter().GetResult();

        lock (_graphBuildLock)
        {
            if (ReferenceEquals(_graphBuildCancellation, cancellation))
            {
                _graphBuildCancellation = null;
                _graphBuildTask = Task.CompletedTask;
                GraphBuildInProgress = false;
            }
        }

        cancellation?.Dispose();
    }

    private void CancelAndDrainRenderNudgers()
    {
        Task[] tasks;
        lock (_graphBuildLock)
            tasks = [.. _renderNudgerTasks];

        Task.WhenAll(tasks).GetAwaiter().GetResult();

        lock (_graphBuildLock)
            _renderNudgerTasks.Clear();
    }

    /// <summary>
    /// Cancels and drains dependency-graph work before the active analyzer is replaced,
    /// then clears every graph result and view cache owned by that analyzer.
    /// </summary>
    internal void ResetDependencyGraphForAnalyzerReplacement()
    {
        CancelAndDrainGraphBuild();

        lock (_graphBuildLock)
        {
            Volatile.Write(ref _dependencyGraphSnapshot, null);
            Volatile.Write(ref _legacyGraphNavigation, null);
            GraphBuildInProgress = false;
            GraphNavigationError = null;
            _graphBuildFailed = false;
        }

        GraphSelectedNode = null;
        GraphMatchIndex = -1;
        GraphSelectedIndex = -1;
        CachedGraphRenderLayout = null;
        CachedGraphRenderLayoutKey = null;
    }

    private static Task QueueDedicatedBackgroundWork(Action work) =>
        Task.Factory.StartNew(
            work,
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach | TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
}
