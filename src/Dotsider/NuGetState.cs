using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Hex1b;
using Hex1b.Widgets;

namespace Dotsider;

/// <summary>
/// State for NuGet package mode — holds the package analyzer and optional DLL inspector state.
/// </summary>
/// <remarks>
/// Creates a new NuGet state for the specified package file.
/// </remarks>
/// <param name="app">The Hex1b application instance.</param>
/// <param name="nupkgPath">File path to the .nupkg file.</param>
public sealed class NuGetState(Hex1bApp app, string nupkgPath) : IDisposable
{
    /// <summary>The Hex1b application instance.</summary>
    public Hex1bApp App { get; } = app;

    /// <summary>The NuGet package analyzer for the opened .nupkg file.</summary>
    public NuGetPackageAnalyzer Package { get; } = new NuGetPackageAnalyzer(nupkgPath);

    /// <summary>The focused key in the package file tree.</summary>
    public object? FileTreeFocusedKey { get; set; }

    /// <summary>The dotsider state for the currently inspected DLL, or null.</summary>
    public DotsiderState? SelectedDllState { get; set; }

    /// <summary>The NuGet file entry for the currently inspected DLL, or null.</summary>
    public NuGetFileEntry? SelectedDllEntry { get; set; }

    /// <summary>Whether the user is viewing the package file list (true) or a DLL inspector (false).</summary>
    public bool IsBrowsingPackage { get; set; } = true;

    /// <summary>Saved focused key from the DLL file list, restored when returning from DLL inspection.</summary>
    public object? SavedFileTreeFocusedKey { get; set; }

    /// <summary>Search state for the package browser view.</summary>
    public SearchState BrowserSearch { get; } = new();

    /// <summary>The currently selected tab index in the DLL inspector.</summary>
    public int CurrentTab { get; set; }

    // --- Read-Only Editor State ---

    /// <summary>Read-only editor for the Package Info panel.</summary>
    public EditorState? PackageInfoEditorState { get; set; }

    /// <summary>Source text used to build <see cref="PackageInfoEditorState"/>, for staleness detection.</summary>
    public string? PackageInfoEditorText { get; set; }

    /// <summary>Yank flash decoration provider for the Package Info editor.</summary>
    public Views.IlYankDecorationProvider PackageInfoYankProvider { get; } = new();

    /// <summary>Tracks the previous frame's selection anchor for word boundary adjustment in the Package Info editor.</summary>
    internal Hex1b.Documents.DocumentOffset? PackageInfoPrevSelectionAnchor;

    /// <summary>Tracks the previous frame's cursor position for word boundary adjustment in the Package Info editor.</summary>
    internal Hex1b.Documents.DocumentOffset? PackageInfoPrevCursorPosition;

    /// <summary>Whether the focused table row should flash with yank highlight colors. Auto-clears after 150ms.</summary>
    public bool YankFlashRow { get; set; }

    /// <summary>Yank notification message shown in the hints bar, auto-clears after 1.5 seconds.</summary>
    public string? YankNotification { get; set; }

    // --- Vim Text Object State ---

    /// <summary>Current state of a pending vim text-object sequence (iw, iW, yiw, yiW).</summary>
    public VimMotionState VimPending { get; set; }

    /// <summary>The editor that started the current vim text-object sequence, for affinity checking.</summary>
    public EditorState? VimPendingEditor { get; set; }

    /// <summary>Cursor position when the text-object sequence was armed, for cursor affinity.</summary>
    public int VimPendingCursorOffset { get; set; }

    /// <summary>Timestamp when the text-object sequence was armed, for 1-second timeout.</summary>
    public DateTime VimPendingTimestamp { get; set; }

    /// <summary>Delegate to perform a neovim-style editor yank, set by the host app (NuGetApp).</summary>
    public Action<Hex1b.Input.InputBindingActionContext, EditorNode>? PerformEditorYank { get; set; }

    /// <summary>Generation counter for yank notification timer race prevention.</summary>
    public long YankGeneration { get; set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        SelectedDllState?.Dispose();
        Package.Dispose();
    }
}
