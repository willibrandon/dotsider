namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// The result of a <see cref="ReadyToRunCorrelationQuery"/>: an <see cref="Outcome"/> with exactly
/// the payload that outcome carries — a <see cref="Report"/> when resolved, a candidate list when
/// ambiguous, a <see cref="Message"/> explaining a miss or an unavailable image.
/// </summary>
/// <param name="Outcome">How the query resolved.</param>
/// <param name="Report">The resolved correlation, or null unless <see cref="Outcome"/> is <see cref="ReadyToRunQueryOutcome.Resolved"/>.</param>
/// <param name="Candidates">The ambiguous matches, empty unless <see cref="Outcome"/> is <see cref="ReadyToRunQueryOutcome.Ambiguous"/>.</param>
/// <param name="Message">A human-readable explanation for a non-resolved outcome, or null when resolved.</param>
public sealed record ReadyToRunQueryResult(
    ReadyToRunQueryOutcome Outcome,
    ReadyToRunMethodReport? Report,
    IReadOnlyList<CorrelationCandidate> Candidates,
    string? Message)
{
    /// <summary>Creates a resolved result carrying the report.</summary>
    /// <param name="report">The resolved correlation payload.</param>
    public static ReadyToRunQueryResult Resolved(ReadyToRunMethodReport report) =>
        new(ReadyToRunQueryOutcome.Resolved, report, [], null);

    /// <summary>Creates an ambiguous result listing every matched candidate.</summary>
    /// <param name="candidates">The matched candidates.</param>
    /// <param name="message">A summary of the ambiguity.</param>
    public static ReadyToRunQueryResult Ambiguous(
        IReadOnlyList<CorrelationCandidate> candidates, string message) =>
        new(ReadyToRunQueryOutcome.Ambiguous, null, candidates, message);

    /// <summary>Creates a not-found result explaining the miss.</summary>
    /// <param name="message">Why nothing matched.</param>
    public static ReadyToRunQueryResult NotFound(string message) =>
        new(ReadyToRunQueryOutcome.NotFound, null, [], message);

    /// <summary>Creates an unavailable result explaining why correlation could not run.</summary>
    /// <param name="message">Why the image is not usable.</param>
    public static ReadyToRunQueryResult Unavailable(string message) =>
        new(ReadyToRunQueryOutcome.Unavailable, null, [], message);
}
