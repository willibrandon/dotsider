using Dotsider.Core.Analysis.Models;

namespace Dotsider.Infrastructure;

/// <summary>
/// A dependency path explaining why a node is present.
/// Preserves the command's public JSON contract with a fixed typed shape.
/// Is registered with source-generated JSON metadata for Native AOT.
/// </summary>
internal sealed record CliWhyPayload(
    string Target,
    string NodeName,
    IReadOnlyList<DgmlPathStep> Chain);
