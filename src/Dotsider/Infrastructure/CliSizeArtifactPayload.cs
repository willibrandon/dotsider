namespace Dotsider.Infrastructure;

/// <summary>
/// Resolved files used to produce one side of a size report.
/// </summary>
internal sealed record CliSizeArtifactPayload(
    string InputPath,
    string MstatPath,
    string? BinaryPath,
    string? DgmlPath);
