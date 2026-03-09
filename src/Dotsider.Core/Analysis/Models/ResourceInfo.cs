namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Information about a managed resource embedded in the assembly.
/// </summary>
/// <param name="Name">The name of the resource.</param>
/// <param name="Visibility">Whether the resource is public or private.</param>
/// <param name="Offset">The byte offset of the resource data within the resources section.</param>
/// <param name="Size">The size of the resource data in bytes, or -1 if unknown.</param>
/// <param name="IsLinked">Whether this is a linked (external) resource rather than embedded.</param>
public sealed record ResourceInfo(
    string Name,
    string Visibility,
    int Offset,
    long Size,
    bool IsLinked);
