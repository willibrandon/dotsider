using Dotsider.Core.Analysis.Models;

namespace Dotsider.Infrastructure;

/// <summary>
/// One contributor to a binary size difference.
/// Preserves the command's public JSON contract with a fixed typed shape.
/// Is registered with source-generated JSON metadata for Native AOT.
/// </summary>
internal sealed record CliSizeContributorPayload(
    string Name,
    string FullPath,
    SizeNodeKind Kind,
    DiffKind Diff,
    long LeftSize,
    long RightSize,
    long Delta,
    string AssemblyName,
    string Namespace,
    int LeftEntryCount,
    int RightEntryCount,
    IReadOnlyList<string> LeftNodeNames,
    IReadOnlyList<string> RightNodeNames,
    IReadOnlyList<DgmlPathStep>? WhyPath);
