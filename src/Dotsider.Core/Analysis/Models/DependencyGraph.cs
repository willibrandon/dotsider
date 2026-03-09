namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// A node in the assembly dependency graph.
/// </summary>
public sealed record GraphNode(
    string Name,
    string? Version,
    string? PublicKeyToken,
    bool IsRoot,
    double X,
    double Y);

/// <summary>
/// An edge connecting two nodes in the dependency graph.
/// </summary>
public sealed record GraphEdge(
    string SourceName,
    string TargetName,
    int TypeRefCount);
