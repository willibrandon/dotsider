namespace Dotsider.Core.Analysis;

/// <summary>
/// Describes the display name, namespace, and defining assembly attributed to an mstat type.
/// </summary>
internal readonly record struct TypeAttribution
{
    /// <summary>Initializes a type attribution.</summary>
    /// <param name="display">The rendered type name.</param>
    /// <param name="namespaceName">The outermost named type's namespace.</param>
    /// <param name="assemblyName">The defining assembly name.</param>
    public TypeAttribution(string display, string namespaceName, string assemblyName)
    {
        Display = display;
        Namespace = namespaceName;
        AssemblyName = assemblyName;
    }

    /// <summary>Gets the unknown-type attribution.</summary>
    public static TypeAttribution Unknown { get; } = new("?", string.Empty, string.Empty);

    /// <summary>Gets the rendered type name.</summary>
    public string Display { get; init; }

    /// <summary>Gets the outermost named type's namespace.</summary>
    public string Namespace { get; init; }

    /// <summary>Gets the defining assembly name.</summary>
    public string AssemblyName { get; init; }
}
