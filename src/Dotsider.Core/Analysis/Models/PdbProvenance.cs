namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Describes where portable PDB information was found, or why it could not be used.
/// </summary>
/// <param name="Kind">The resolved provenance category.</param>
/// <param name="Path">The sidecar PDB path when one was used or probed.</param>
/// <param name="Details">Additional diagnostic context for display surfaces.</param>
public sealed record PdbProvenance(
    PdbProvenanceKind Kind,
    string? Path = null,
    string? Details = null)
{
    /// <inheritdoc />
    public override string ToString() => Kind switch
    {
        PdbProvenanceKind.Sidecar => Path is null ? "Sidecar" : $"Sidecar({Path})",
        PdbProvenanceKind.Embedded => "Embedded",
        PdbProvenanceKind.NoDebugDirectory => "NoDebugDirectory",
        PdbProvenanceKind.CodeViewSidecarMissing => Details ?? "CodeViewSidecarMissing",
        PdbProvenanceKind.CodeViewSidecarMismatched => Details ?? "CodeViewSidecarMismatched",
        PdbProvenanceKind.UnsupportedWindowsPdb => Details ?? "UnsupportedWindowsPdb",
        PdbProvenanceKind.NativePdb => Details ?? "NativePdb",
        PdbProvenanceKind.BundleSidecarSkipped => Details ?? "BundleSidecarSkipped",
        _ => Kind.ToString()
    };
}
