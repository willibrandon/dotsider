namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Describes a single file entry within a .NET single-file bundle.
/// </summary>
/// <param name="Offset">Byte offset of the entry within the bundle file.</param>
/// <param name="Size">Uncompressed size in bytes.</param>
/// <param name="CompressedSize">Compressed size in bytes, or 0 if not compressed.</param>
/// <param name="Type">The type of bundled file.</param>
/// <param name="RelativePath">Path of the embedded file, relative to the bundle source directory.</param>
public sealed record BundleEntry(
    long Offset,
    long Size,
    long CompressedSize,
    BundleFileType Type,
    string RelativePath);
