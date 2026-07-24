namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Portable PDB discovery outcomes that are meaningful to .NET developers.
/// </summary>
public enum PdbProvenanceKind
{
    /// <summary>The PE has no debug directory.</summary>
    NoDebugDirectory,

    /// <summary>A portable CodeView entry points at a sidecar PDB that was not found.</summary>
    CodeViewSidecarMissing,

    /// <summary>A portable sidecar PDB was found, but its ID does not match the PE CodeView entry.</summary>
    CodeViewSidecarMismatched,

    /// <summary>A matching portable sidecar PDB was opened.</summary>
    Sidecar,

    /// <summary>An embedded portable PDB was opened.</summary>
    Embedded,

    /// <summary>A CodeView entry was present, but it identifies a Windows PDB or another non-portable PDB.</summary>
    UnsupportedWindowsPdb,

    /// <summary>A Windows native PDB was found beside the binary and its GUID and age match the CodeView entry.</summary>
    NativePdb,

    /// <summary>The assembly came from a single-file bundle, so sidecar probing was intentionally skipped.</summary>
    BundleSidecarSkipped,

    /// <summary>An embedded portable PDB was present, but it was malformed or exceeded a safety limit.</summary>
    InvalidEmbeddedPdb
}
