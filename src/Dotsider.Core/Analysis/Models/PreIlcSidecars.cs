namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// The pre-ILC build outputs found for a Native AOT binary: the managed input assembly
/// ILC compiled, its portable PDB, and any mstat/DGML sidecars discovered in the build's
/// intermediate tree. A result exists whenever <em>anything</em> was found — mstat/DGML-only
/// results feed silent fallbacks, while the attach/offer flow gates on
/// <see cref="HasAttachableCompanion"/>.
/// </summary>
/// <param name="ManagedAssemblyPath">The validated pre-ILC managed assembly, or null when none was found.</param>
/// <param name="Origin">How <paramref name="ManagedAssemblyPath"/> was located.</param>
/// <param name="ManagedPdbPath">The sidecar portable PDB probed beside the managed assembly, when one exists (kept even when mismatched, for diagnostics).</param>
/// <param name="PdbStatus">The portable-PDB situation of the managed assembly.</param>
/// <param name="MstatPath">An mstat sidecar found in the intermediate tree, or null.</param>
/// <param name="CodegenDgmlPath">The codegen dependency graph found in the intermediate tree, or null. Its node names match the mstat's exactly.</param>
/// <param name="ScanDgmlPath">The scan dependency graph found in the intermediate tree, or null.</param>
/// <param name="IlcResponseFilePath">The ILC response file that was parsed, or null.</param>
/// <param name="LocalReferencePaths">Reference assemblies with positive local/project evidence (under the project tree or a build-output-shaped path outside any package store), metadata-validated.</param>
/// <param name="PackageReferenceCount">References resolved from a package store (runtime pack, NuGet cache, SDK packs) — summarized, never enumerated.</param>
/// <param name="OtherReferenceCount">References that exist but carry no positive local evidence — summarized and listed in <paramref name="Details"/>, never classified local.</param>
/// <param name="UnresolvedReferencePaths">Reference paths that do not exist locally (copied build trees, foreign machines) — recorded verbatim, never treated as local.</param>
/// <param name="Details">Diagnostic notes: skipped candidates, fall-through reasons, staleness, unclassified references.</param>
public sealed record PreIlcSidecars(
    string? ManagedAssemblyPath,
    PreIlcAssemblyOrigin Origin,
    string? ManagedPdbPath,
    PreIlcPdbStatus PdbStatus,
    string? MstatPath,
    string? CodegenDgmlPath,
    string? ScanDgmlPath,
    string? IlcResponseFilePath,
    IReadOnlyList<string> LocalReferencePaths,
    int PackageReferenceCount,
    int OtherReferenceCount,
    IReadOnlyList<string> UnresolvedReferencePaths,
    string? Details)
{
    /// <summary>Whether a validated managed input exists to offer as an attachable companion.</summary>
    public bool HasAttachableCompanion => ManagedAssemblyPath is not null;
}
