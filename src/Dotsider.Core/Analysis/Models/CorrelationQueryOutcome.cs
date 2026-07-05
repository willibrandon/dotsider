namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// The outcome of a <see cref="CorrelationQuery"/>: how a method-or-address query resolved
/// against an attached companion set's correlation index.
/// </summary>
public enum CorrelationQueryOutcome
{
    /// <summary>The query resolved to exactly one method; the report is populated.</summary>
    Resolved,

    /// <summary>The query matched several methods (overloads); the candidates are listed and the caller must disambiguate.</summary>
    Ambiguous,

    /// <summary>The query matched no method or address in the companion set.</summary>
    NotFound,

    /// <summary>Correlation could not run — no attachable companion, or the index could not be built.</summary>
    Unavailable
}
