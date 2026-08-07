namespace Dotsider.Core.Protocol;

/// <summary>
/// Compact provenance for the managed inputs used to produce a Native AOT binary.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record PreIlcSummary(
    bool HasAttachableCompanion,
    string? RootAssembly,
    string Origin,
    string PdbStatus,
    bool HasMstat,
    bool HasDgml,
    int LocalReferenceCount,
    int PackageReferenceCount,
    int OtherReferenceCount);
