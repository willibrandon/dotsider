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
    /// runtime asset path after both paths are contained beneath the selected package.
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

    /// <summary>
    /// Resolved from the .NET Framework Global Assembly Cache at
    /// <c>%WINDIR%\Microsoft.NET\assembly\GAC_*</c>. Only produced for .NET Framework roots.
    /// </summary>
    Gac,

    /// <summary>
    /// Resolved from the .NET Framework runtime directory at
    /// <c>%WINDIR%\Microsoft.NET\Framework[64]\v4.0.30319</c>. Distinct from
    /// <see cref="RuntimeDirectory"/>, which references the active .NET (Core) host directory
    /// the analyzer process is itself running on.
    /// </summary>
    FrameworkRuntimeDirectory,

    /// <summary>
    /// Resolved by following a configured <c>&lt;codeBase href&gt;</c> entry from the .NET
    /// Framework binding policy chain (app config, publisher policy, or machine.config).
    /// </summary>
    CodeBase,

    /// <summary>
    /// A <c>&lt;codeBase&gt;</c> entry for the effective identity was present in the binding
    /// policy chain but its href pointed at a path that does not exist on disk. Reported as
    /// fail-fast (the CLR does not fall back to probing in this case), distinct from generic
    /// <see cref="Unresolved"/> so the UI can surface the configured href to the user.
    /// </summary>
    CodeBaseMissing,

    /// <summary>
    /// Compiled into a Native AOT image: the node comes from the binary's mstat size report
    /// or native import table rather than an on-disk assembly, so there is no file to open.
    /// </summary>
    CompiledIntoNativeImage,
}
