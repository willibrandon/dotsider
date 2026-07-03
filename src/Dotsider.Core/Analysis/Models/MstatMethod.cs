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
public sealed record MstatMethod(
    string Name,
    string DeclaringType,
    string Namespace,
    string AssemblyName,
    int Size,
    int GcInfoSize,
    int EhInfoSize,
    string? NodeName);
