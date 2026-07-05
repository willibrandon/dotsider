namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// The result of a <see cref="CorrelationQuery"/>: an <see cref="Outcome"/> with exactly the
/// payload that outcome carries — a <see cref="Report"/> when resolved, a candidate list when
/// ambiguous, a <see cref="Message"/> explaining a miss or an unavailable index.
/// </summary>
/// <param name="Outcome">How the query resolved.</param>
/// <param name="Report">The resolved correlation, or null unless <see cref="Outcome"/> is <see cref="CorrelationQueryOutcome.Resolved"/>.</param>
/// <param name="Candidates">The ambiguous matches, empty unless <see cref="Outcome"/> is <see cref="CorrelationQueryOutcome.Ambiguous"/>.</param>
/// <param name="Message">A human-readable explanation for a non-resolved outcome, or null when resolved.</param>
public sealed record CorrelationQueryResult(
    CorrelationQueryOutcome Outcome,
    CorrelationReport? Report,
    IReadOnlyList<CorrelationCandidate> Candidates,
    string? Message)
{
    /// <summary>Creates a resolved result carrying the correlation report.</summary>
    /// <param name="report">The resolved correlation payload.</param>
    public static CorrelationQueryResult Resolved(CorrelationReport report) =>
        new(CorrelationQueryOutcome.Resolved, report, [], null);

    /// <summary>Creates an ambiguous result listing every matched candidate.</summary>
    /// <param name="candidates">The matched candidates.</param>
    /// <param name="message">A summary of the ambiguity.</param>
    public static CorrelationQueryResult Ambiguous(
        IReadOnlyList<CorrelationCandidate> candidates, string message) =>
        new(CorrelationQueryOutcome.Ambiguous, null, candidates, message);

    /// <summary>Creates a not-found result explaining the miss.</summary>
    /// <param name="message">Why nothing matched.</param>
    public static CorrelationQueryResult NotFound(string message) =>
        new(CorrelationQueryOutcome.NotFound, null, [], message);

    /// <summary>Creates an unavailable result explaining why correlation could not run.</summary>
    /// <param name="message">Why the index was unavailable.</param>
    public static CorrelationQueryResult Unavailable(string message) =>
        new(CorrelationQueryOutcome.Unavailable, null, [], message);
}
