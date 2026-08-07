using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Protocol;

/// <summary>
/// Metadata-backed member-search results.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record MetadataMemberSearchPayload(
    IReadOnlyList<TypeDefInfo> Types,
    IReadOnlyList<MethodDefInfo> Methods,
    IReadOnlyList<MemberRefInfo> MemberRefs);
