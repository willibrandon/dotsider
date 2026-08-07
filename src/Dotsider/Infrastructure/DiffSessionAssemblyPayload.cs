namespace Dotsider.Infrastructure;

/// <summary>
/// Assembly-difference inputs exposed by a live session.
/// Preserves the command's public JSON contract with a fixed typed shape.
/// Is registered with source-generated JSON metadata for Native AOT.
/// </summary>
internal sealed record DiffSessionAssemblyPayload(
    string Mode,
    string FileName,
    DiffSessionSidePayload Left,
    DiffSessionSidePayload Right);
