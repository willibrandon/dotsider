namespace Dotsider.Infrastructure;

/// <summary>
/// ReadyToRun details written by the CLI.
/// Preserves the command's public JSON contract with a fixed typed shape.
/// Is registered with source-generated JSON metadata for Native AOT.
/// </summary>
internal sealed record CliReadyToRunPayload(
    string Status,
    int MajorVersion,
    int MinorVersion,
    bool IsComposite,
    bool IsComponent,
    bool IsPartialImage,
    string Architecture,
    string? OwnerCompositeExecutable,
    int PrecompiledMethods,
    int InstantiationCount,
    long TotalCodeSize,
    string? Diagnostic = null);
