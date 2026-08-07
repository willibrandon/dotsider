using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Protocol;

/// <summary>
/// A possible match for an ambiguous mstat query.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record MstatCandidatePayload(
    MstatSectionKind Section,
    string Key,
    string FullPath,
    string DisplayName,
    long Size,
    int EntryCount,
    IReadOnlyList<string> NodeNames);
