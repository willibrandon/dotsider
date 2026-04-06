using System.Reflection;

namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Information about a field defined in the assembly's FieldDef metadata table.
/// </summary>
/// <param name="Token">The metadata token for this field definition.</param>
/// <param name="DeclaringType">The fully qualified name of the type that declares this field.</param>
/// <param name="Name">The name of the field.</param>
/// <param name="Attributes">The field attribute flags (access, static, literal, etc.).</param>
/// <param name="Signature">The decoded field type signature string.</param>
public sealed record FieldDefInfo(
    int Token,
    string DeclaringType,
    string Name,
    FieldAttributes Attributes,
    string Signature);
