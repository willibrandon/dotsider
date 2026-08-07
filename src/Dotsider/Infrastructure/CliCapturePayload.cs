namespace Dotsider.Infrastructure;

/// <summary>
/// Text captured from a live dotsider session.
/// Preserves the command's public JSON contract with a fixed typed shape.
/// Is registered with source-generated JSON metadata for Native AOT.
/// </summary>
internal sealed record CliCapturePayload(string Content);
