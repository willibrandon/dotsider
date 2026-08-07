namespace Dotsider.Infrastructure;

/// <summary>
/// A filesystem path returned by a CLI command.
/// Preserves the command's public JSON contract with a fixed typed shape.
/// Is registered with source-generated JSON metadata for Native AOT.
/// </summary>
internal sealed record CliPathPayload(string Path);
