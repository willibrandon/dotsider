namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// One component assembly of a composite ReadyToRun image, from the <c>ComponentAssemblies</c>
/// section joined to the manifest and its MVIDs. Its native code lives in the composite; its
/// metadata is resolved from a sibling assembly matched by name and MVID.
/// </summary>
/// <param name="AssemblyName">The component's simple assembly name from the manifest.</param>
/// <param name="Mvid">The component's module version id, used to validate the resolved sibling's identity.</param>
/// <param name="CorHeaderRva">The RVA of the component's embedded COR header, or 0 when not embedded.</param>
/// <param name="CoreHeaderRva">The RVA of the component's per-assembly ReadyToRun core header.</param>
/// <param name="ResolvedPath">The sibling assembly file whose MVID matched, or null when unresolved.</param>
/// <param name="MetadataAvailable">Whether the component's metadata was resolved (name + MVID matched a sibling).</param>
public sealed record ReadyToRunComponent(
    string AssemblyName,
    Guid Mvid,
    int CorHeaderRva,
    int CoreHeaderRva,
    string? ResolvedPath,
    bool MetadataAvailable);
