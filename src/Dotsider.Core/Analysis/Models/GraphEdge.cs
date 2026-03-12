namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// An edge connecting two nodes in the dependency graph.
/// </summary>
/// <param name="SourceName">Name of the assembly that holds the reference.</param>
/// <param name="TargetName">Name of the referenced assembly.</param>
/// <param name="TypeRefCount">Number of type references from source to target.</param>
public sealed record GraphEdge(
    string SourceName,
    string TargetName,
    int TypeRefCount);
