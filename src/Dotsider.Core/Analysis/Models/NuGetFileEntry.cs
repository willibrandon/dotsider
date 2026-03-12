namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Represents a file entry within a NuGet package (.nupkg).
/// </summary>
/// <param name="Name">File name without directory path.</param>
/// <param name="FullPath">Full path of the entry within the package archive.</param>
/// <param name="Directory">Directory portion of the entry path.</param>
/// <param name="CompressedSize">Compressed size in bytes inside the .nupkg.</param>
/// <param name="UncompressedSize">Uncompressed size in bytes.</param>
/// <param name="IsDll">Whether the entry is a .NET assembly (.dll).</param>
public sealed record NuGetFileEntry(
    string Name,
    string FullPath,
    string Directory,
    long CompressedSize,
    long UncompressedSize,
    bool IsDll);
