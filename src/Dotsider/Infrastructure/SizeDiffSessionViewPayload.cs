namespace Dotsider.Infrastructure;

/// <summary>
/// View state exposed by a live size-difference session.
/// Preserves the command's public JSON contract with a fixed typed shape.
/// Is registered with source-generated JSON metadata for Native AOT.
/// </summary>
internal sealed record SizeDiffSessionViewPayload(
    string Mode,
    int Tab,
    SizeDiffFilterMode FilterMode);
