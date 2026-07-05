namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// How a pre-ILC managed method relates to the native image it was compiled into.
/// </summary>
public enum MethodCorrelationStatus
{
    /// <summary>
    /// The method owns its native evidence outright: one or more native symbols (several
    /// mean generic instantiations) and any matching mstat rows are unambiguously its own.
    /// </summary>
    CorrelatedExact,

    /// <summary>
    /// Native evidence exists but is shared with sibling overloads — ILC's overload
    /// suffixes cannot be assigned back to a specific signature, so no candidate owns it.
    /// </summary>
    CorrelatedAmbiguous,

    /// <summary>
    /// The only evidence is mstat size data: the method was compiled, but no native symbol
    /// is available to disassemble (size only; no native symbol).
    /// </summary>
    CorrelatedByMstatOnly,

    /// <summary>
    /// No native evidence at all — the method was trimmed away, fully inlined, or never
    /// had a body (abstract/extern).
    /// </summary>
    NotInNativeImage,
}
