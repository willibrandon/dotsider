namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// The result of building a transitive assembly dependency graph. Contains the public topology
/// consumed by serializers (<see cref="Nodes"/>, <see cref="Edges"/>) and the internal navigation
/// metadata consumed by the TUI (<see cref="NavigationById"/>).
/// </summary>
/// <param name="Nodes">
/// All nodes in the graph including the root and any unresolved or identity-mismatched leaves,
/// each carrying its computed layout coordinates and depth.
/// </param>
/// <param name="Edges">
/// Directed edges from each referencing assembly to every assembly it references. Edges for
/// cycles and diamonds are preserved; a child identity revisited through a second parent emits
/// a new edge but does not re-expand the subtree.
/// </param>
/// <param name="NavigationById">
/// Per-node navigation metadata keyed by <see cref="GraphNode.Id"/>. Intended for in-process TUI
/// use only — consumers that serialize graph topology (CLI JSON, diagnostics UDS, MCP tools) must
/// ignore this dictionary to avoid leaking machine-local paths through their public contracts.
/// </param>
public sealed record DependencyGraphResult(
    IReadOnlyList<GraphNode> Nodes,
    IReadOnlyList<GraphEdge> Edges,
    IReadOnlyDictionary<string, GraphNavigationContext> NavigationById);
