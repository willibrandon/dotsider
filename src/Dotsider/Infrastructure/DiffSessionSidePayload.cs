namespace Dotsider.Infrastructure;

/// <summary>
/// One assembly used by a live assembly-difference session.
/// Preserves the command's public JSON contract with a fixed typed shape.
/// Is registered with source-generated JSON metadata for Native AOT.
/// </summary>
internal sealed record DiffSessionSidePayload(
    string FilePath,
    string FileName,
    long FileSize,
    string? AssemblyName,
    string? AssemblyVersion,
    string? TargetFramework);
