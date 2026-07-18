namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Represents a file entry within a NuGet package (.nupkg).
/// </summary>
/// <param name="Name">
/// Raw, untrusted file-name portion of the archive entry path. This value is not display-safe.
/// </param>
/// <param name="FullPath">
/// Raw, untrusted path of the entry within the package archive. This value is not a filesystem
/// path and must be validated before extraction.
/// </param>
/// <param name="Directory">
/// Raw, untrusted directory portion of the archive entry path, normalized only to use forward
/// slashes. This value is not a filesystem-safe path or display-safe text.
/// </param>
/// <param name="CompressedSize">Compressed size in bytes inside the .nupkg.</param>
/// <param name="UncompressedSize">Uncompressed size in bytes.</param>
/// <param name="IsDll">Whether the archive entry name has a .dll file extension.</param>
public sealed record NuGetFileEntry(
    string Name,
    string FullPath,
    string Directory,
    long CompressedSize,
    long UncompressedSize,
    bool IsDll);
