namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One frozen object from an ILC size report (format 2.1+) — an object allocated at compile
/// time and baked into the image, most commonly a string literal. For back-compat these bytes
/// are also summed into the <c>ArrayOfFrozenObjects</c> blob entry.
/// </summary>
/// <param name="TypeName">The frozen object's type display name (for example <c>System.String</c>).</param>
/// <param name="AssemblyName">The simple name of the assembly that defines the object's type.</param>
/// <param name="Size">The object size in bytes, including its object header.</param>
/// <param name="NodeName">The compiler's dependency-graph node name; joins to the DGML node <c>Label</c>.</param>
/// <param name="OwningType">
/// The type whose static data serialized this object, or null when the object is not a
/// serialized static (string literals report null).
/// </param>
public sealed record MstatFrozenObject(
    string TypeName,
    string AssemblyName,
    int Size,
    string? NodeName,
    string? OwningType);
