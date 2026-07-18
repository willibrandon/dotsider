namespace Dotsider.DocGenerator;

/// <summary>
/// Represents an exception documented for an API item.
/// </summary>
public sealed class ExceptionItem
{
    /// <summary>
    /// Gets or sets the XML doc description for this exception.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the exception type UID.
    /// </summary>
    public string? Type { get; set; }
}
