namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Internal per-node metadata describing how a dependency graph node was resolved and the
/// context under which it was reached. Used by the TUI for Enter-to-open navigation and
/// framework filtering. Never serialized — this data must not leak through CLI, diagnostics,
/// or MCP surfaces that publish graph topology.
/// </summary>
/// <param name="Resolved">
/// The resolved assembly location, or <see langword="null"/> when the node is unresolved or
/// the provenance is <see cref="AssemblyProvenance.IdentityMismatch"/>.
/// </param>
/// <param name="ReferencingFilePath">
/// The file path of the analyzer that first caused this node to be visited.
/// </param>
/// <param name="ReferencingBundlePath">
/// The bundle path associated with the referencing analyzer, when applicable.
/// </param>
/// <param name="ReferencingTargetFramework">
/// The target framework of the referencing analyzer, used for shared-framework probing.
/// </param>
/// <param name="ReferencingPreferredRuntimePack">
/// The preferred runtime pack of the referencing analyzer.
/// </param>
/// <param name="Provenance">Classification of how the node was located.</param>
/// <param name="IsFrameworkAssembly">
/// Whether the node represents a .NET framework assembly, classified independently of its
/// provenance so that framework assemblies shipped inside a self-contained publish or single-file
/// bundle are still identified correctly.
/// </param>
/// <param name="CandidateProbePath">
/// The file path of a simple-name match whose identity did not match the requested reference,
/// populated only when <see cref="Provenance"/> is <see cref="AssemblyProvenance.IdentityMismatch"/>.
/// </param>
public sealed record GraphNavigationContext(
    ResolvedAssembly? Resolved,
    string? ReferencingFilePath,
    string? ReferencingBundlePath,
    string? ReferencingTargetFramework,
    string? ReferencingPreferredRuntimePack,
    AssemblyProvenance Provenance,
    bool IsFrameworkAssembly,
    string? CandidateProbePath);
