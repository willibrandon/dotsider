namespace Dotsider.Infrastructure;

/// <summary>
/// View state exposed by a live assembly-difference session.
/// Preserves the command's public JSON contract with a fixed typed shape.
/// Is registered with source-generated JSON metadata for Native AOT.
/// </summary>
internal sealed record DiffSessionViewPayload(
    string Mode,
    int Tab,
    DiffFilterMode FilterMode);
