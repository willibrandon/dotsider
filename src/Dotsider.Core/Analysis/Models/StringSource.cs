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
    RawBinary,

    /// <summary>
    /// UTF-16LE printable character sequences extracted directly from the binary.
    /// This is how managed string literals freeze in Native AOT images.
    /// </summary>
    RawBinaryUtf16,

    /// <summary>
    /// A frozen <see cref="string"/> object recovered from a Native AOT binary's frozen
    /// object region — the AOT counterpart of the #US heap.
    /// </summary>
    FrozenObject
}
