namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Describes how an assembly in the dependency graph was located — or why it could not be.
/// </summary>
/// <remarks>
/// The order of enum members is not significant; callers should compare by member name.
/// </remarks>
public enum AssemblyProvenance
{
    /// <summary>The analyzed assembly itself (the graph root).</summary>
    Root,

    /// <summary>Resolved from the referencing assembly's directory on disk.</summary>
    AppLocal,

    /// <summary>Resolved from the active .NET runtime directory.</summary>
    RuntimeDirectory,

    /// <summary>
    /// Resolved from the NuGet global packages folder by consulting the referencing
    /// assembly's <c>.deps.json</c> manifest for the exact resolved package version and
    /// runtime asset path.
    /// </summary>
    NuGetPackageCache,

    /// <summary>Resolved through the shared-framework discovery for the target framework.</summary>
    SharedFramework,

    /// <summary>Extracted from the single-file bundle that produced the referencing assembly.</summary>
    SourceBundle,

    /// <summary>Extracted from the host process bundle (when dotsider itself is bundled).</summary>
    HostBundle,

    /// <summary>Extracted from a single-file bundle adjacent to the referencing assembly.</summary>
    AdjacentBundle,

    /// <summary>No probe produced any candidate file for the referenced simple name.</summary>
    Unresolved,

    /// <summary>
    /// A probe produced a file whose simple name matched, but whose manifest identity
    /// (version, culture, or public key token) did not match the requested reference.
    /// The graph does not expand from such candidates — the node is left as an unresolved leaf.
    /// </summary>
    IdentityMismatch,
}
