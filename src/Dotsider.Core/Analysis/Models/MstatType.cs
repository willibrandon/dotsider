namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One constructed type from an ILC size report. The size is the type's MethodTable data —
/// the runtime type structure — not the code of its methods, which is reported per method.
/// </summary>
/// <param name="Name">The type's display name, with generic arguments rendered when instantiated.</param>
/// <param name="Namespace">The type's namespace, or an empty string for the global namespace.</param>
/// <param name="AssemblyName">The simple name of the assembly that defines the type.</param>
/// <param name="Size">The MethodTable size in bytes.</param>
/// <param name="NodeName">
/// The compiler's dependency-graph node name (format 2.0+), or null in 1.x reports; joins to
/// the DGML node <c>Label</c>.
/// </param>
public sealed record MstatType(
    string Name,
    string Namespace,
    string AssemblyName,
    int Size,
    string? NodeName);
