namespace Dotsider.Infrastructure;

/// <summary>
/// Pre-ILC sidecar details written by the CLI.
/// Preserves the command's public JSON contract with a fixed typed shape.
/// Is registered with source-generated JSON metadata for Native AOT.
/// </summary>
internal sealed record CliPreIlcPayload(
    string? ManagedAssemblyPath,
    string Origin,
    string? ManagedPdbPath,
    string PdbStatus,
    string? MstatPath,
    string? CodegenDgmlPath,
    string? ScanDgmlPath,
    string? IlcResponseFilePath,
    IReadOnlyList<string> LocalReferencePaths,
    int PackageReferenceCount,
    int OtherReferenceCount,
    IReadOnlyList<string> UnresolvedReferencePaths,
    bool HasAttachableCompanion,
    string? Details);
