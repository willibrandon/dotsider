namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// The outcome of probing a PE image for a crossgen2 ReadyToRun header. This is a parse status,
/// not a coverage measure — whether not every method is precompiled is
/// <see cref="ReadyToRunInfo.IsPartialImage"/> (the <c>READYTORUN_FLAG_PARTIAL</c> flag), not a
/// status value here.
/// </summary>
public enum ReadyToRunStatus
{
    /// <summary>No ReadyToRun header was found — a plain managed, Native AOT, or native image.</summary>
    NotReadyToRun,

    /// <summary>A valid ReadyToRun header whose section tables parsed successfully.</summary>
    Valid,

    /// <summary>A recognized ReadyToRun signature whose header or tables are malformed; surfaced, not usable.</summary>
    Corrupt,

    /// <summary>A recognized ReadyToRun signature with a major version outside the supported range.</summary>
    UnsupportedVersion,

    /// <summary>
    /// A managed native header directory is present but does not carry the ReadyToRun signature
    /// (for example a legacy NGen image). The binary stays classified as managed, but the
    /// diagnostic is surfaced rather than hidden.
    /// </summary>
    UnrecognizedNativeHeader,
}
