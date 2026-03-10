namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// An edge connecting two nodes in the dependency graph.
/// </summary>
public sealed record GraphEdge(
    string SourceName,
    string TargetName,
    int TypeRefCount);
