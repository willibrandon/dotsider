namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One field-RVA data entry from an ILC size report (format 2.1+) — the initial data of a
/// field mapped directly into the image, typically compiler-generated arrays behind
/// collection expressions and <c>ReadOnlySpan</c> literals. For back-compat these bytes are
/// also summed into the <c>FieldRvaData</c> blob entry.
/// </summary>
/// <param name="Name">The field's display name, including its declaring type (<c>Type::Field</c>).</param>
/// <param name="AssemblyName">The simple name of the assembly that defines the field.</param>
/// <param name="Size">The RVA data size in bytes.</param>
/// <param name="NodeName">The compiler's dependency-graph node name; joins to the DGML node <c>Label</c>.</param>
/// <param name="Namespace">The declaring type's namespace, or an empty string for the global namespace.</param>
public sealed record MstatRvaField(
    string Name,
    string AssemblyName,
    int Size,
    string? NodeName,
    string Namespace)
{
    /// <summary>
    /// The pre-namespace shape (four arguments), preserved so existing construction sites keep
    /// compiling. <see cref="Namespace"/> defaults to an empty string.
    /// </summary>
    /// <param name="name">The field's display name, including its declaring type.</param>
    /// <param name="assemblyName">The simple name of the defining assembly.</param>
    /// <param name="size">The RVA data size in bytes.</param>
    /// <param name="nodeName">The compiler's dependency-graph node name.</param>
    public MstatRvaField(string name, string assemblyName, int size, string? nodeName)
        : this(name, assemblyName, size, nodeName, "")
    {
    }

    /// <summary>The pre-namespace four-output deconstruction, preserved alongside the generated five-output one.</summary>
    /// <param name="name">The field's display name, including its declaring type.</param>
    /// <param name="assemblyName">The simple name of the defining assembly.</param>
    /// <param name="size">The RVA data size in bytes.</param>
    /// <param name="nodeName">The compiler's dependency-graph node name.</param>
    public void Deconstruct(out string name, out string assemblyName, out int size, out string? nodeName)
    {
        name = Name;
        assemblyName = AssemblyName;
        size = Size;
        nodeName = NodeName;
    }
}
