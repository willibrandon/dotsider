using Dotsider.Analysis;
using Dotsider.Analysis.Models;
using Hex1b;

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

    /// <summary>The current search query for diff tables, or null if search is inactive.</summary>
    public string? DiffSearchQuery { get; set; }

    /// <summary>Whether the diff search input is active.</summary>
    public bool DiffSearchActive { get; set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        Left.Dispose();
        Right.Dispose();
    }
}

/// <summary>
/// Specifies which diff entries to display.
/// </summary>
public enum DiffFilterMode { All, AddedOnly, RemovedOnly, ChangedOnly }
