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
/// <param name="OwningAssemblyName">
/// The simple name of the assembly defining <see cref="OwningType"/>, or null when the object
/// has no owner. This — not <see cref="AssemblyName"/> — is the assembly whose code caused the
/// bytes: a frozen string's <see cref="AssemblyName"/> is always the core library.
/// </param>
/// <param name="OwningNamespace">
/// The namespace of <see cref="OwningType"/>, or null when the object has no owner.
/// </param>
public sealed record MstatFrozenObject(
    string TypeName,
    string AssemblyName,
    int Size,
    string? NodeName,
    string? OwningType,
    string? OwningAssemblyName,
    string? OwningNamespace)
{
    /// <summary>
    /// The pre-owner-attribution shape (five arguments), preserved so existing construction
    /// sites keep compiling. The owner attribution fields default to null.
    /// </summary>
    /// <param name="typeName">The frozen object's type display name.</param>
    /// <param name="assemblyName">The simple name of the assembly defining the object's type.</param>
    /// <param name="size">The object size in bytes.</param>
    /// <param name="nodeName">The compiler's dependency-graph node name.</param>
    /// <param name="owningType">The owning type's display name, or null.</param>
    public MstatFrozenObject(
        string typeName, string assemblyName, int size, string? nodeName, string? owningType)
        : this(typeName, assemblyName, size, nodeName, owningType, null, null)
    {
    }

    /// <summary>The pre-owner-attribution five-output deconstruction, preserved alongside the generated seven-output one.</summary>
    /// <param name="typeName">The frozen object's type display name.</param>
    /// <param name="assemblyName">The simple name of the assembly defining the object's type.</param>
    /// <param name="size">The object size in bytes.</param>
    /// <param name="nodeName">The compiler's dependency-graph node name.</param>
    /// <param name="owningType">The owning type's display name, or null.</param>
    public void Deconstruct(
        out string typeName, out string assemblyName, out int size,
        out string? nodeName, out string? owningType)
    {
        typeName = TypeName;
        assemblyName = AssemblyName;
        size = Size;
        nodeName = NodeName;
        owningType = OwningType;
    }
}
