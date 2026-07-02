namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// A node in the transitive assembly dependency graph. Topology only — layout coordinates
/// and rendered labels are the responsibility of the view layer, which projects the visible
/// subgraph into a separate render model so filters and viewport changes rebalance without
/// perturbing this record.
/// </summary>
/// <param name="Id">
/// Stable opaque identifier for this node, derived from the full assembly identity
/// (<see cref="Name"/>, <see cref="Version"/>, <see cref="Culture"/>, <see cref="PublicKeyToken"/>)
/// via <see cref="AssemblyIdentityFormat.Format(string, string?, string?, string?)"/>. Two
/// assemblies that share a simple name but differ in any identity field produce distinct ids.
/// </param>
/// <param name="Name">Assembly simple name.</param>
/// <param name="Version">Assembly version string, or <see langword="null"/> when unavailable.</param>
/// <param name="Culture">
/// Assembly culture, or <c>"neutral"</c> for culture-neutral assemblies. Never empty.
/// </param>
/// <param name="PublicKeyToken">Public key token hex string, or <see langword="null"/>.</param>
/// <param name="IsRoot">Whether this is the analyzed assembly (the root of the graph).</param>
/// <param name="Depth">
/// The minimum number of AssemblyRef hops from the root to this node as discovered by BFS.
/// Zero for the root; one for direct references; greater for transitive references.
/// </param>
/// <param name="Unresolved">
/// Whether this node is a leaf that could not be resolved. Includes both the case where no
/// probe produced any candidate and the case where a probe produced a simple-name match whose
/// manifest identity did not match — the latter is further distinguished by the node's
/// navigation-context provenance.
/// </param>
/// <param name="Kind">
/// What the node represents. Defaults to <see cref="GraphNodeKind.Assembly"/> and is omitted
/// from serialized output at that default, so managed graph JSON is unchanged.
/// </param>
public sealed record GraphNode(
    string Id,
    string Name,
    string? Version,
    string Culture,
    string? PublicKeyToken,
    bool IsRoot,
    int Depth,
    bool Unresolved,
    [property: System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    GraphNodeKind Kind = GraphNodeKind.Assembly);
