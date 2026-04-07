namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// The parsed manifest header of a .NET single-file bundle.
/// </summary>
/// <param name="MajorVersion">Bundle format major version (1-6).</param>
/// <param name="MinorVersion">Bundle format minor version.</param>
/// <param name="FileCount">Number of files embedded in the bundle.</param>
/// <param name="BundleId">Unique identifier for this bundle.</param>
/// <param name="Entries">The list of file entries in the bundle.</param>
public sealed record BundleManifest(
    uint MajorVersion,
    uint MinorVersion,
    int FileCount,
    string BundleId,
    IReadOnlyList<BundleEntry> Entries);
