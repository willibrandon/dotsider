namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Information about a referenced type from the TypeRef metadata table.
/// </summary>
/// <param name="Token">The metadata token for this type reference.</param>
/// <param name="Namespace">The namespace of the referenced type.</param>
/// <param name="Name">The simple name of the referenced type.</param>
/// <param name="FullName">The fully qualified name (Namespace.Name).</param>
/// <param name="ResolutionScope">The scope in which the type is defined (assembly name or module).</param>
public sealed record TypeRefInfo(
    int Token,
    string Namespace,
    string Name,
    string FullName,
    string ResolutionScope);
