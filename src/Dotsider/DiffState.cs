using Dotsider.Core.Analysis;
using Dotsider.Core.Analysis.Models;
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

    /// <summary>Per-tab search state for diff views (Summary=0, Types=1, Methods=2, References=3).</summary>
    public SearchState[] Search { get; } = [.. Enumerable.Range(0, 4).Select(_ => new SearchState())];

    /// <summary>Delegate to navigate to the next search match in the current diff view.</summary>
    public Action? NavigateNextMatch { get; set; }

    /// <summary>Delegate to navigate to the previous search match in the current diff view.</summary>
    public Action? NavigatePrevMatch { get; set; }

    /// <inheritdoc/>
    public void Dispose()
    {
        Left.Dispose();
        Right.Dispose();
    }
}
