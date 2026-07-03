namespace Dotsider.Core.Analysis;

/// <summary>
/// How a candidate ELF debug sidecar's identity relates to its stripped image. Every identity
/// signal present on the image must match — one failing signal rejects the sidecar even when
/// another matches.
/// </summary>
internal enum ElfSidecarMatch
{
    /// <summary>At least one identity signal was present and every present signal matched.</summary>
    Matched,

    /// <summary>
    /// The image carried no identity signal; the sidecar passed only the loose checks
    /// (same <c>e_machine</c>, has <c>.debug_info</c>), which a diagnostic should note.
    /// </summary>
    LooseMatch,

    /// <summary>
    /// A present signal disagreed — build id differs or absent from the sidecar, or the
    /// debuglink CRC failed — or no signal was present and the loose checks failed too.
    /// </summary>
    Mismatched,
}
