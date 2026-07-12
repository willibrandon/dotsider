using System.Collections.Immutable;

namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Represents a metadata-bearing sibling module whose bytes were read and authenticated while
/// resolving the manifest assembly's File table entry.
/// </summary>
/// <param name="Bytes">The authenticated module bytes.</param>
/// <param name="Path">The module's same-directory path beside its manifest assembly.</param>
/// <param name="ManifestPath">The manifest assembly path that authenticated the module.</param>
/// <param name="TargetFramework">The manifest assembly's target-framework context.</param>
/// <param name="PreferredRuntimePack">The manifest assembly's preferred runtime-pack context.</param>
public sealed record ResolvedModule(
    ImmutableArray<byte> Bytes,
    string Path,
    string ManifestPath,
    string? TargetFramework,
    string? PreferredRuntimePack) : ResolvedAssembly;
