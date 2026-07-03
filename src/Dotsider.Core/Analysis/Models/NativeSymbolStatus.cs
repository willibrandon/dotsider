namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// The outcome of probing a binary for native symbols. When no symbols are returned, the status
/// distinguishes the reasons — missing, mismatched, corrupt, ambiguous, or fallback-only — so
/// callers can explain the result instead of showing an empty table with no cause.
/// </summary>
public enum NativeSymbolStatus
{
    /// <summary>Named symbols loaded from a primary source.</summary>
    Loaded,

    /// <summary>No symbol file was found beside the binary and no fallback applied.</summary>
    NoSymbolFile,

    /// <summary>A symbol file was found but its identity did not match the binary.</summary>
    IdMismatch,

    /// <summary>A symbol file was found and matched, but could not be parsed.</summary>
    CorruptSymbolFile,

    /// <summary>Only nameless function boundaries were recovered from unwind data.</summary>
    FallbackOnly,

    /// <summary>A fat/universal binary offered no slice that could be disambiguated.</summary>
    AmbiguousImage,

    /// <summary>The binary is managed or otherwise has no native symbols to read.</summary>
    NotApplicable
}
