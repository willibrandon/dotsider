namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Information about a custom attribute applied to a metadata entity.
/// </summary>
/// <param name="Parent">A description of the entity the attribute is applied to.</param>
/// <param name="Constructor">The fully qualified name of the attribute constructor method.</param>
/// <param name="Value">The decoded attribute value as a display string, or null if decoding failed.</param>
public sealed record CustomAttributeInfo(
    string Parent,
    string Constructor,
    string? Value);
