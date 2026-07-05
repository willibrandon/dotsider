namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// The portable-PDB situation of a located pre-ILC managed assembly.
/// </summary>
public enum PreIlcPdbStatus
{
    /// <summary>No managed assembly was located, so no PDB question arises.</summary>
    NotApplicable,

    /// <summary>A sidecar portable PDB exists and its ID matches the assembly's debug directory.</summary>
    Matched,

    /// <summary>No sidecar PDB, but the assembly embeds a portable PDB — source and sequence points still work.</summary>
    Embedded,

    /// <summary>The assembly references a portable PDB but neither a sidecar nor an embedded copy was found.</summary>
    Missing,

    /// <summary>A sidecar PDB exists but its ID does not match the assembly — it belongs to a different build.</summary>
    Mismatched,
}
