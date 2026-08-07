using Dotsider.Core.Analysis.Models;

namespace Dotsider.Infrastructure;

/// <summary>
/// Candidates returned for an ambiguous correlation query.
/// Preserves the command's public JSON contract with a fixed typed shape.
/// Is registered with source-generated JSON metadata for Native AOT.
/// </summary>
internal sealed record CliAmbiguityPayload(
    bool Ambiguous,
    string? Message,
    IReadOnlyList<CorrelationCandidate> Candidates);
