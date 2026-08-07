namespace Dotsider.Infrastructure;

/// <summary>
/// One source used by a live size-difference session.
/// Preserves the command's public JSON contract with a fixed typed shape.
/// Is registered with source-generated JSON metadata for Native AOT.
/// </summary>
internal sealed record SizeDiffSessionSourcePayload(
    string MstatPath,
    string? BinaryPath,
    long? BinaryFileSize,
    long MstatTotal);
