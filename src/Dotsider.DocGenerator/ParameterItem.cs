namespace Dotsider.DocGenerator;

/// <summary>
/// Represents a method or constructor parameter.
/// </summary>
public sealed class ParameterItem
{
    /// <summary>
    /// Gets or sets the parameter name.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the parameter type UID.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the XML doc description for this parameter.
    /// </summary>
    public string? Description { get; set; }
}
