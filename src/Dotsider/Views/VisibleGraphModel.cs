using Dotsider.Core.Analysis.Models;

namespace Dotsider.Views;

/// <summary>
/// A filtered view over the underlying dependency graph, computed once per render of the
/// Dep Graph tab and used as the single source of truth for search, selection, yank, and
/// rendering. When the framework-filter toggle is on, framework-classified nodes and any
/// edges touching them are absent from the visible model; the root is always present.
/// </summary>
/// <param name="Nodes">Visible nodes in render order.</param>
/// <param name="Edges">Visible edges whose source and target are both visible.</param>
/// <param name="IndexById">Map from <see cref="GraphNode.Id"/> to index into <see cref="Nodes"/>.</param>
internal sealed record VisibleGraphModel(
    IReadOnlyList<GraphNode> Nodes,
    IReadOnlyList<GraphEdge> Edges,
    IReadOnlyDictionary<string, int> IndexById);
