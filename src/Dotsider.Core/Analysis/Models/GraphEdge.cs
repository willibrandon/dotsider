namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// A directed edge from a referencing assembly to a referenced assembly in the transitive
/// dependency graph. Edges are retained for cycles and diamonds: revisiting an already-seen
/// target identity emits a new edge but does not re-expand the target's subtree.
/// </summary>
/// <param name="SourceId">
/// The <see cref="GraphNode.Id"/> of the referencing assembly.
/// </param>
/// <param name="TargetId">
/// The <see cref="GraphNode.Id"/> of the referenced assembly.
/// </param>
/// <param name="TypeRefCount">
/// The number of TypeRef entries in the referencing assembly whose resolution scope resolves
/// to the exact full identity of the target (not merely its simple name). Zero when the target
/// is referenced by the AssemblyRef table but no TypeRefs are scoped to it.
/// </param>
/// <param name="RequestedIdentity">
/// The identity exactly as it appeared in the referencing assembly's AssemblyRef metadata,
/// before any .NET Framework binding policy was applied. May differ from the target node's
/// identity when the target was keyed on the bound identity (e.g., two AssemblyRefs at
/// different versions both redirected to the same loaded version collapse onto a single
/// target node, but each edge preserves its own pre-redirect requested identity here).
/// <see langword="null"/> when there was no policy rewrite — the requested identity is the
/// same as the target node's identity in that case.
/// </param>
public sealed record GraphEdge(
    string SourceId,
    string TargetId,
    int TypeRefCount,
    AssemblyRefInfo? RequestedIdentity = null);
