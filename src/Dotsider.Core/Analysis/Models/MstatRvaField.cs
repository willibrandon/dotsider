namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One field-RVA data entry from an ILC size report (format 2.1+) — the initial data of a
/// field mapped directly into the image, typically compiler-generated arrays behind
/// collection expressions and <c>ReadOnlySpan</c> literals. For back-compat these bytes are
/// also summed into the <c>FieldRvaData</c> blob entry.
/// </summary>
/// <param name="Name">The field's display name, including its declaring type.</param>
/// <param name="AssemblyName">The simple name of the assembly that defines the field.</param>
/// <param name="Size">The RVA data size in bytes.</param>
/// <param name="NodeName">The compiler's dependency-graph node name; joins to the DGML node <c>Label</c>.</param>
public sealed record MstatRvaField(
    string Name,
    string AssemblyName,
    int Size,
    string? NodeName);
