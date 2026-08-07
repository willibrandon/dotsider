using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Protocol;

/// <summary>
/// One dependency chain explaining a Native AOT size contributor.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record MstatWhyChainPayload(
    string NodeName,
    bool Found,
    IReadOnlyList<DgmlPathStep> Steps);
