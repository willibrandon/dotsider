namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// The outcome of a <see cref="ReadyToRunCorrelationQuery"/>: how a method-or-address query
/// resolved against a ReadyToRun image.
/// </summary>
public enum ReadyToRunQueryOutcome
{
    /// <summary>The query resolved to exactly one method; the report is populated.</summary>
    Resolved,

    /// <summary>The query matched several methods (overloads, or a token and an address); the candidates are listed.</summary>
    Ambiguous,

    /// <summary>The query matched no method or address in the image.</summary>
    NotFound,

    /// <summary>Correlation could not run — the image is not a usable ReadyToRun image.</summary>
    Unavailable
}
