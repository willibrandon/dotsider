using Dotsider.Analysis;
using Dotsider.Analysis.Models;
using Hex1b;
using Hex1b.Documents;
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
    public DotsiderState(Hex1bApp app, string filePath)
    {
        App = app;
        Analyzer = new AssemblyAnalyzer(filePath);
        IlDisassembler = new IlDisassembler(Analyzer);
        StringExtractor = new StringExtractor(Analyzer);
        HexEditorState = new EditorState(new Hex1bDocument(Analyzer.RawBytes.ToArray()));
    }

    /// <summary>
    /// Creates a new application state wrapping an existing analyzer (used by NuGet mode).
    /// </summary>
    public DotsiderState(Hex1bApp app, AssemblyAnalyzer analyzer)
    {
        App = app;
        Analyzer = analyzer;
        IlDisassembler = new IlDisassembler(Analyzer);
        StringExtractor = new StringExtractor(Analyzer);
        HexEditorState = new EditorState(new Hex1bDocument(Analyzer.RawBytes.ToArray()));
    }

    /// <summary>The Hex1b application instance.</summary>
    public Hex1bApp App { get; }

    /// <summary>The core assembly analyzer (current top of navigation stack).</summary>
    public AssemblyAnalyzer Analyzer { get; private set; }

    /// <summary>The IL disassembler for method body inspection.</summary>
    public IlDisassembler IlDisassembler { get; private set; }

    /// <summary>The string extractor for all string sources.</summary>
    public StringExtractor StringExtractor { get; private set; }

    // --- Tab Navigation ---

    /// <summary>The currently selected main tab index.</summary>
    public int CurrentTab { get; set; }

    // --- General Tab State ---

    /// <summary>The focused assembly reference key in the dependency table.</summary>
    public object? GeneralFocusedDep { get; set; }

    /// <summary>Navigation stack of assembly paths for drill-down.</summary>
    public Stack<AssemblyAnalyzer> NavigationStack { get; } = new();

    // --- PE/Metadata Tab State ---

    /// <summary>The selected sub-tab index in the PE/Metadata view (Sections, TypeDef, etc.).</summary>
    public int PeSubTab { get; set; }

    /// <summary>Whether to display sizes in human-readable format.</summary>
    public bool HumanReadableSizes { get; set; } = true;

    /// <summary>The current search query for the PE/Metadata tab, or null if search is inactive.</summary>
    public string? PeSearchQuery { get; set; }

    /// <summary>Whether the PE/Metadata search input is active.</summary>
    public bool PeSearchActive { get; set; }

    /// <summary>The item being shown in the detail popup, or null.</summary>
    public string? PeDetailContent { get; set; }

    /// <summary>The focused row key in the current PE metadata table.</summary>
    public object? PeFocusedKey { get; set; }

    // --- IL Inspector Tab State ---

    /// <summary>The currently selected method for disassembly, or null.</summary>
    public MethodDefInfo? IlSelectedMethod { get; set; }

    /// <summary>The current search query for IL search, or null.</summary>
    public string? IlSearchQuery { get; set; }

    /// <summary>Whether IL search mode is active.</summary>
    public bool IlSearchActive { get; set; }

    // --- Strings Tab State ---

    /// <summary>The minimum string length filter for raw strings.</summary>
    public int StringsMinLength { get; set; } = 4;

    /// <summary>The selected string source tab (0=User, 1=Metadata, 2=Raw).</summary>
    public int StringsSourceTab { get; set; }

    /// <summary>The current search query for strings, or null.</summary>
    public string? StringsSearchQuery { get; set; }

    /// <summary>Whether string search mode is active.</summary>
    public bool StringsSearchActive { get; set; }

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

    // --- Size Treemap Tab State ---

    /// <summary>Cached size tree for treemap visualization.</summary>
    public SizeNode? CachedSizeTree { get; set; }

    /// <summary>The current drill-down level in the treemap.</summary>
    public SizeNode? TreemapCurrentLevel { get; set; }

    /// <summary>Breadcrumb stack for treemap drill-down navigation.</summary>
    public Stack<SizeNode> TreemapBreadcrumb { get; } = new();

    /// <summary>The hovered item description in the treemap.</summary>
    public string? TreemapHoveredItem { get; set; }

    // --- Hex Dump Tab State ---

    /// <summary>The editor state for the hex dump view.</summary>
    public EditorState HexEditorState { get; private set; }

    /// <summary>
    /// Pushes a new assembly onto the navigation stack and makes it the active analyzer.
    /// </summary>
    public void PushAssembly(string filePath)
    {
        NavigationStack.Push(Analyzer);
        Analyzer = new AssemblyAnalyzer(filePath);
        IlDisassembler = new IlDisassembler(Analyzer);
        StringExtractor = new StringExtractor(Analyzer);
        HexEditorState = new EditorState(new Hex1bDocument(Analyzer.RawBytes.ToArray()));
        ResetViewState();
    }

    /// <summary>
    /// Pops the top of the navigation stack and restores the previous analyzer.
    /// </summary>
    public bool PopAssembly()
    {
        if (NavigationStack.Count == 0) return false;
        Analyzer.Dispose();
        Analyzer = NavigationStack.Pop();
        IlDisassembler = new IlDisassembler(Analyzer);
        StringExtractor = new StringExtractor(Analyzer);
        HexEditorState = new EditorState(new Hex1bDocument(Analyzer.RawBytes.ToArray()));
        ResetViewState();
        return true;
    }

    private void ResetViewState()
    {
        GeneralFocusedDep = null;
        PeSubTab = 0;
        PeFocusedKey = null;
        PeDetailContent = null;
        PeSearchQuery = null;
        PeSearchActive = false;
        IlSelectedMethod = null;
        IlSearchQuery = null;
        IlSearchActive = false;
        StringsSourceTab = 0;
        StringsFocusedKey = null;
        StringsDetailContent = null;
        StringsSearchQuery = null;
        StringsSearchActive = false;
        CachedUserStrings = null;
        CachedMetadataStrings = null;
        CachedRawStrings = null;
        CachedGraph = null;
        GraphSelectedNode = null;
        CachedSizeTree = null;
        TreemapCurrentLevel = null;
        TreemapBreadcrumb.Clear();
        TreemapHoveredItem = null;
    }

    /// <summary>
    /// Gets the active string entries based on the current source tab, applying search filter.
    /// </summary>
    /// <returns>The filtered string entries for display.</returns>
    public IReadOnlyList<StringEntry> GetActiveStrings()
    {
        var entries = StringsSourceTab switch
        {
            0 => CachedUserStrings ??= StringExtractor.ExtractUserStrings(),
            1 => CachedMetadataStrings ??= StringExtractor.ExtractMetadataStrings(),
            2 => GetCachedRawStrings(),
            _ => []
        };

        if (!string.IsNullOrEmpty(StringsSearchQuery))
        {
            entries = entries
                .Where(e => e.Value.Contains(StringsSearchQuery, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return entries;
    }

    /// <summary>
    /// Formats a file size in bytes to a human-readable string.
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

    /// <inheritdoc/>
    public void Dispose()
    {
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
