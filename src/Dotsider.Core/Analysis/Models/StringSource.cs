namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Identifies the source from which a string was extracted.
/// </summary>
public enum StringSource
{
    /// <summary>
    /// The #US (User Strings) metadata heap, containing string literals used in IL code.
    /// </summary>
    UserStrings,

    /// <summary>
    /// The #Strings metadata heap, containing identifier names used in metadata tables.
    /// </summary>
    MetadataStrings,

    /// <summary>
    /// Raw printable character sequences extracted directly from the binary.
    /// </summary>
    RawBinary
}
