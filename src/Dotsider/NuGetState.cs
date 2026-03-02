using Dotsider.Analysis;
using Dotsider.Analysis.Models;
using Hex1b;

namespace Dotsider;

/// <summary>
/// State for NuGet package mode — holds the package analyzer and optional DLL inspector state.
/// </summary>
public sealed class NuGetState : IDisposable
{
    public NuGetState(Hex1bApp app, string nupkgPath)
    {
        App = app;
        Package = new NuGetPackageAnalyzer(nupkgPath);
    }

    public Hex1bApp App { get; }
    public NuGetPackageAnalyzer Package { get; }

    public object? FileTreeFocusedKey { get; set; }
    public DotsiderState? SelectedDllState { get; set; }
    public NuGetFileEntry? SelectedDllEntry { get; set; }
    public bool IsBrowsingPackage { get; set; } = true;
    public int CurrentTab { get; set; }

    public void Dispose()
    {
        SelectedDllState?.Dispose();
        Package.Dispose();
    }
}
