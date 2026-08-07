namespace Dotsider.DeployHost;

/// <summary>
/// Represents one relative sample file and its expected SHA-256 digest.
/// Relative paths remain rooted beneath the fixed sample directory.
/// Entries are ordered deterministically before the manifest is written.
/// </summary>
internal sealed record SampleManifestEntry(string RelativePath, string Sha256);
