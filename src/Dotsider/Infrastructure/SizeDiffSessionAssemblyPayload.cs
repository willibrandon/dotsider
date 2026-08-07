namespace Dotsider.Infrastructure;

/// <summary>
/// Size-difference inputs exposed by a live session.
/// Preserves the command's public JSON contract with a fixed typed shape.
/// Is registered with source-generated JSON metadata for Native AOT.
/// </summary>
internal sealed record SizeDiffSessionAssemblyPayload(
    string Mode,
    string FileName,
    SizeDiffSessionSourcePayload Left,
    SizeDiffSessionSourcePayload Right,
    long Delta);
