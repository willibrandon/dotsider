namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Outcome of an identity-based assembly resolution. Carries everything the dependency-graph
/// builder and UI need: the resolved file/bundle (or <see langword="null"/> on failure), the
/// provenance classifying how the file was located, the candidate path of an identity-mismatched
/// simple-name hit, and — for .NET Framework binds — the policy-layer attribution and the
/// effective bound identity.
/// </summary>
/// <param name="Resolved">
/// The file or bundle the binder picked, or <see langword="null"/> when the bind failed
/// (Unresolved, IdentityMismatch, CodeBaseMissing).
/// </param>
/// <param name="Provenance">Classification of how the node was located.</param>
/// <param name="CandidateProbePath">
/// The simple-name match whose identity did not align (IdentityMismatch), or the configured
/// codeBase href that does not exist (CodeBaseMissing). <see langword="null"/> for other outcomes.
/// </param>
/// <param name="AppliedPolicy">
/// Records the requested → bound rewrite when .NET Framework binding policy fired.
/// <see langword="null"/> for non-redirected resolutions and for all .NET Core / .NET 5+ resolutions.
/// </param>
/// <param name="LoadedIdentity">
/// The identity the binder actually loaded after applying policy. May differ from the requested
/// identity for net48 binds when redirects collapsed multiple requested versions onto one loaded
/// version. <see langword="null"/> for non-net48 resolutions and for failures.
/// </param>
public sealed record AssemblyResolution(
    ResolvedAssembly? Resolved,
    AssemblyProvenance Provenance,
    string? CandidateProbePath,
    AppliedPolicy? AppliedPolicy = null,
    AssemblyRefInfo? LoadedIdentity = null);
