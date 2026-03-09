using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
using Hex1b;

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

    /// <summary>Search state for the package browser view.</summary>
    public SearchState BrowserSearch { get; } = new();

    /// <summary>The currently selected tab index in the DLL inspector.</summary>
    public int CurrentTab { get; set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        SelectedDllState?.Dispose();
        Package.Dispose();
    }
}
