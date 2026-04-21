namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Information about a referenced type from the TypeRef metadata table.
/// </summary>
/// <param name="Token">The metadata token for this type reference.</param>
/// <param name="Namespace">The namespace of the referenced type.</param>
/// <param name="Name">The simple name of the referenced type.</param>
/// <param name="FullName">The fully qualified name (Namespace.Name).</param>
/// <param name="ResolutionScope">
/// The scope in which the type is defined, rendered as a human-readable string — the
/// referenced assembly's simple name, the enclosing type's full name, or the scope kind
/// for module and module-reference scopes.
/// </param>
/// <param name="ResolutionScopeId">
/// The full-identity identifier of the referenced assembly, when the resolution scope ultimately
/// derives from an <c>AssemblyReference</c>. For TypeRefs whose scope is another TypeRef
/// (nested-type scopes) this carries the enclosing type's resolution-scope id by walking the
/// nested chain to its root. Empty for module or module-reference scopes, where no referenced
/// assembly is involved. Used by the dependency-graph builder to group TypeRefs by full
/// identity so per-edge counts are correct even when two references share a simple name.
/// </param>
public sealed record TypeRefInfo(
    int Token,
    string Namespace,
    string Name,
    string FullName,
    string ResolutionScope,
    string ResolutionScopeId);
