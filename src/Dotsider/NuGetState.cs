using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Hex1b;
using Hex1b.Widgets;
using System.Diagnostics;

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
    private bool _disposed;

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

    /// <summary>
    /// Gets the sanitized error from the most recent DLL open attempt, or <see langword="null"/>.
    /// </summary>
    internal string? OpenError { get; private set; }

    /// <summary>
    /// Opens a package DLL and transitions to the DLL inspector when successful.
    /// </summary>
    /// <param name="entry">The package-owned DLL entry to open.</param>
    /// <returns><see langword="true"/> when the DLL inspector was opened; otherwise, <see langword="false"/>.</returns>
    internal bool TryOpenDll(NuGetFileEntry entry)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            var analyzer = Package.OpenDll(entry);
            DotsiderState nextState;
            try
            {
                nextState = new DotsiderState(App, analyzer);
            }
            catch
            {
                analyzer.Dispose();
                throw;
            }

            try
            {
                SelectedDllState?.Dispose();
            }
            catch
            {
                nextState.Dispose();
                throw;
            }

            SavedFileTreeFocusedKey = FileTreeFocusedKey;
            SelectedDllState = nextState;
            SelectedDllEntry = entry;
            IsBrowsingPackage = false;
            OpenError = null;
            App.RequestFocus(node => node.GetType().Name.StartsWith("TableNode", StringComparison.Ordinal));
            App.Invalidate();
            return true;
        }
        catch (UnsafePackageEntryException ex)
        {
            return FailOpen("Cannot open DLL: unsafe package entry path", ex);
        }
        catch (BadImageFormatException ex)
        {
            return FailOpen("Cannot open DLL: invalid .NET assembly", ex);
        }
        catch (IOException ex)
        {
            return FailOpen("Cannot open DLL: extraction failed", ex);
        }
        catch (Exception ex)
        {
            return FailOpen("Cannot open DLL", ex);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        var selectedDllState = SelectedDllState;
        SelectedDllState = null;
        SelectedDllEntry = null;

        try
        {
            selectedDllState?.Dispose();
        }
        finally
        {
            Package.Dispose();
        }
    }

    private bool FailOpen(string message, Exception exception)
    {
        OpenError = message;
        Debug.WriteLine($"Failed to open package DLL: {exception}");
        App.Invalidate();
        return false;
    }
}
