namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Represents a file entry within a NuGet package (.nupkg).
/// </summary>
public sealed record NuGetFileEntry(
    string Name,
    string FullPath,
    string Directory,
    long CompressedSize,
    long UncompressedSize,
    bool IsDll);
