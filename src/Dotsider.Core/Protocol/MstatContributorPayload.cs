using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Protocol;

/// <summary>
/// One Native AOT size contributor.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record MstatContributorPayload(
    MstatSectionKind Section,
    string Key,
    string AssemblyName,
    string Namespace,
    string TypeName,
    string LeafName,
    string DisplayName,
    string FullPath,
    long Size,
    int EntryCount,
    IReadOnlyList<string> NodeNames,
    IReadOnlyList<MstatWhyChainPayload>? WhyChains);
