namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One named global data region from an ILC size report — embedded metadata, hydration
/// tables, dispatch maps, and the like. Blob names come from the compiler's node type names
/// (for example <c>Metadata</c> or <c>InterfaceDispatchMap</c>), with same-named regions
/// summed into one entry.
/// </summary>
/// <param name="Name">The region name.</param>
/// <param name="Size">The total size in bytes across all regions with this name.</param>
public sealed record MstatBlob(
    string Name,
    int Size);
