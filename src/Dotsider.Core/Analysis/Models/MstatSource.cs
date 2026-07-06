namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// A resolved mstat input: the decoded report plus where it came from. Produced by
/// <see cref="Dotsider.Core.Analysis.MstatLocator"/> from either a bare <c>.mstat</c> file or
/// a Native AOT binary with a size-report sidecar.
/// </summary>
/// <param name="Data">The decoded size report.</param>
/// <param name="MstatPath">The path of the <c>.mstat</c> file the report was read from.</param>
/// <param name="BinaryPath">The Native AOT binary the report describes, or null when the input was a bare <c>.mstat</c>.</param>
/// <param name="BinaryFileSize">The binary's size on disk in bytes, or null when the input was a bare <c>.mstat</c>.</param>
/// <param name="DgmlPath">The ILC dependency graph (DGML) found beside the input, or null when none exists — "why is this in my binary" needs it.</param>
public sealed record MstatSource(
    MstatData Data,
    string MstatPath,
    string? BinaryPath,
    long? BinaryFileSize,
    string? DgmlPath);
