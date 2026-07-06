namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One compiled method body from an ILC size report. Sizes are bytes of native artifact, not
/// IL: ILC compiles each body once, so the sum over all methods is the code contribution to
/// the binary.
/// </summary>
/// <param name="Name">The method name, with generic arguments rendered when instantiated.</param>
/// <param name="DeclaringType">The declaring type's display name, including namespace.</param>
/// <param name="Namespace">The declaring type's namespace, or an empty string for the global namespace.</param>
/// <param name="AssemblyName">The simple name of the assembly the method was compiled from.</param>
/// <param name="Size">The native code size in bytes.</param>
/// <param name="GcInfoSize">The GC info size in bytes.</param>
/// <param name="EhInfoSize">The exception-handling info size in bytes, or 0 when the method has none.</param>
/// <param name="NodeName">
/// The compiler's dependency-graph node name (format 2.0+), or null in 1.x reports. The same
/// string appears as the node <c>Label</c> in the DGML graphs <c>IlcGenerateDgmlFile</c> emits,
/// which is how a size entry joins to its dependency chain.
/// </param>
/// <param name="Signature">
/// The rendered parameter-type list of the method's definition (for example
/// <c>(string, int)</c>), or an empty string when the signature could not be decoded.
/// Overloads share a <see cref="Name"/> but never a signature, so
/// (<see cref="AssemblyName"/>, <see cref="DeclaringType"/>, <see cref="Name"/>,
/// <see cref="Signature"/>) identifies a method stably across builds.
/// </param>
public sealed record MstatMethod(
    string Name,
    string DeclaringType,
    string Namespace,
    string AssemblyName,
    int Size,
    int GcInfoSize,
    int EhInfoSize,
    string? NodeName,
    string Signature)
{
    /// <summary>
    /// The pre-signature shape (eight arguments), preserved so existing construction sites keep
    /// compiling. <see cref="Signature"/> defaults to an empty string.
    /// </summary>
    /// <param name="name">The method name.</param>
    /// <param name="declaringType">The declaring type's display name.</param>
    /// <param name="namespace">The declaring type's namespace.</param>
    /// <param name="assemblyName">The simple name of the defining assembly.</param>
    /// <param name="size">The native code size in bytes.</param>
    /// <param name="gcInfoSize">The GC info size in bytes.</param>
    /// <param name="ehInfoSize">The EH info size in bytes.</param>
    /// <param name="nodeName">The compiler's dependency-graph node name, or null.</param>
    public MstatMethod(
        string name, string declaringType, string @namespace, string assemblyName,
        int size, int gcInfoSize, int ehInfoSize, string? nodeName)
        : this(name, declaringType, @namespace, assemblyName, size, gcInfoSize, ehInfoSize, nodeName, "")
    {
    }

    /// <summary>The pre-signature eight-output deconstruction, preserved alongside the generated nine-output one.</summary>
    /// <param name="name">The method name.</param>
    /// <param name="declaringType">The declaring type's display name.</param>
    /// <param name="namespace">The declaring type's namespace.</param>
    /// <param name="assemblyName">The simple name of the defining assembly.</param>
    /// <param name="size">The native code size in bytes.</param>
    /// <param name="gcInfoSize">The GC info size in bytes.</param>
    /// <param name="ehInfoSize">The EH info size in bytes.</param>
    /// <param name="nodeName">The compiler's dependency-graph node name, or null.</param>
    public void Deconstruct(
        out string name, out string declaringType, out string @namespace, out string assemblyName,
        out int size, out int gcInfoSize, out int ehInfoSize, out string? nodeName)
    {
        name = Name;
        declaringType = DeclaringType;
        @namespace = Namespace;
        assemblyName = AssemblyName;
        size = Size;
        gcInfoSize = GcInfoSize;
        ehInfoSize = EhInfoSize;
        nodeName = NodeName;
    }
}
