using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Protocol;

/// <summary>
/// Recovered Native AOT member-search results.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record RecoveredMemberSearchPayload(
    IReadOnlyList<RecoveredTypePayload> Types,
    IReadOnlyList<RecoveredMethodPayload> Methods,
    IReadOnlyList<MemberRefInfo> MemberRefs);
