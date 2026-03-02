using System.Reflection;

namespace Dotsider.Analysis.Models;

/// <summary>
/// Information about a type defined in the assembly's TypeDef metadata table.
/// </summary>
/// <param name="Token">The metadata token for this type definition.</param>
/// <param name="Namespace">The namespace of the type, or empty string for global types.</param>
/// <param name="Name">The simple name of the type.</param>
/// <param name="FullName">The fully qualified name (Namespace.Name).</param>
/// <param name="Attributes">The type attribute flags (visibility, layout, semantics).</param>
/// <param name="BaseType">The fully qualified name of the base type, or null for interfaces/System.Object.</param>
/// <param name="MethodCount">Number of methods defined on this type.</param>
/// <param name="FieldCount">Number of fields defined on this type.</param>
public sealed record TypeDefInfo(
    int Token,
    string Namespace,
    string Name,
    string FullName,
    TypeAttributes Attributes,
    string? BaseType,
    int MethodCount,
    int FieldCount);
