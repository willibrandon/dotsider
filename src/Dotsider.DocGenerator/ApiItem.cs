namespace Dotsider.DocGenerator;

/// <summary>
/// Represents an API item parsed from DocFX YAML metadata.
/// </summary>
public sealed class ApiItem
{
    /// <summary>
    /// Gets or sets the unique identifier for this API item.
    /// </summary>
    public string Uid { get; set; } = "";

    /// <summary>
    /// Gets or sets the short name of this API item.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the name qualified with the containing type.
    /// </summary>
    public string? NameWithType { get; set; }

    /// <summary>
    /// Gets or sets the fully qualified name including namespace.
    /// </summary>
    public string? FullName { get; set; }

    /// <summary>
    /// Gets or sets the kind of API item (Class, Struct, Interface, Method, etc.).
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the namespace this item belongs to.
    /// </summary>
    public string? Namespace { get; set; }

    /// <summary>
    /// Gets or sets the XML doc summary text.
    /// </summary>
    public string? Summary { get; set; }

    /// <summary>
    /// Gets or sets the XML doc remarks text.
    /// </summary>
    public string? Remarks { get; set; }

    /// <summary>
    /// Gets or sets the XML doc example text.
    /// </summary>
    public string? Example { get; set; }

    /// <summary>
    /// Gets or sets the C# declaration syntax.
    /// </summary>
    public string? SyntaxContent { get; set; }

    /// <summary>
    /// Gets or sets the UID of the parent item.
    /// </summary>
    public string? Parent { get; set; }

    /// <summary>
    /// Gets or sets the return type UID.
    /// </summary>
    public string? ReturnType { get; set; }

    /// <summary>
    /// Gets or sets the return value description.
    /// </summary>
    public string? ReturnDescription { get; set; }

    /// <summary>
    /// Gets the UIDs of child members.
    /// </summary>
    public List<string> Children { get; } = [];

    /// <summary>
    /// Gets the UIDs of types in the inheritance chain.
    /// </summary>
    public List<string> Inheritance { get; } = [];

    /// <summary>
    /// Gets the UIDs of implemented interfaces.
    /// </summary>
    public List<string> Implements { get; } = [];

    /// <summary>
    /// Gets the method or constructor parameters.
    /// </summary>
    public List<ParameterItem> Parameters { get; } = [];
}
