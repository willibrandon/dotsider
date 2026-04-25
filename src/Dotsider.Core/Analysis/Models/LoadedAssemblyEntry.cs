namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Per-loaded-identity entry interned in <c>LoadedAssemblyCache</c>. When two distinct requested
/// identities redirect to the same loaded identity, both <see cref="NetFxBindResult.Loaded"/>
/// values reference-equal this single entry, faithfully modeling the CLR's "already loaded"
/// reuse: only one filesystem read per loaded identity.
/// </summary>
/// <param name="Identity">The bound identity (post-policy) that this entry represents.</param>
/// <param name="Path">The on-disk file path the CLR would load for this identity.</param>
public sealed record LoadedAssemblyEntry(AssemblyRefInfo Identity, string Path);
