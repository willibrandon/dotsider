namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Result of a single .NET Framework bind. Carries the requested identity, the effective identity
/// after policy was applied, the loaded identity (when binding succeeded), the file path the CLR
/// would load, the provenance classification, the policy-layer attribution, and (when binding
/// failed) a human-readable reason for UI surfacing.
/// </summary>
/// <param name="Requested">Identity exactly as named by the metadata reference.</param>
/// <param name="EffectiveAfterPolicy">Identity after framework unification + machine + publisher + app.</param>
/// <param name="Loaded">Identity of the file the binder actually opened, or <see langword="null"/> on failure.</param>
/// <param name="LoadedPath">Path the binder would hand to the CLR loader, or <see langword="null"/> on failure.</param>
/// <param name="Provenance">Classification of how the node was located.</param>
/// <param name="AppliedPolicy">Records the requested → bound rewrite when policy fired.</param>
/// <param name="FailureReason">Human-readable explanation for non-success outcomes.</param>
/// <param name="CandidateProbePath">
/// For <see cref="AssemblyProvenance.IdentityMismatch"/>, the simple-name match whose identity
/// did not align. <see langword="null"/> for other outcomes.
/// </param>
public sealed record NetFxBindResult(
    AssemblyRefInfo Requested,
    AssemblyRefInfo EffectiveAfterPolicy,
    AssemblyRefInfo? Loaded,
    string? LoadedPath,
    AssemblyProvenance Provenance,
    AppliedPolicy? AppliedPolicy,
    string? FailureReason,
    string? CandidateProbePath);
