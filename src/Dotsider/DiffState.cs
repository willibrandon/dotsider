using Dotsider.Analysis;
using Dotsider.Analysis.Models;
using Hex1b;

namespace Dotsider;

/// <summary>
/// State for diff mode — holds two analyzers and the diff result.
/// </summary>
public sealed class DiffState : IDisposable
{
    public DiffState(Hex1bApp app, string leftPath, string rightPath)
    {
        App = app;
        Left = new AssemblyAnalyzer(leftPath);
        Right = new AssemblyAnalyzer(rightPath);
        DiffResult = AssemblyDiffer.Compare(Left, Right);
    }

    public Hex1bApp App { get; }
    public AssemblyAnalyzer Left { get; }
    public AssemblyAnalyzer Right { get; }
    public AssemblyDiffResult DiffResult { get; }

    public int CurrentTab { get; set; }
    public object? DiffFocusedKey { get; set; }
    public DiffFilterMode FilterMode { get; set; } = DiffFilterMode.All;
    public string? DiffSearchQuery { get; set; }
    public bool DiffSearchActive { get; set; }

    public void Dispose()
    {
        Left.Dispose();
        Right.Dispose();
    }
}

public enum DiffFilterMode { All, AddedOnly, RemovedOnly, ChangedOnly }
