namespace Dotsider.Infrastructure;

/// <summary>
/// Summary counts for managed-to-native correlation.
/// Preserves the command's public JSON contract with a fixed typed shape.
/// Is registered with source-generated JSON metadata for Native AOT.
/// </summary>
internal sealed record CliCorrelationSummaryPayload(
    string? RootAssembly,
    int LocalReferenceCount,
    int ExactCount,
    int AmbiguousCount,
    int MstatOnlyCount,
    int NotInImageCount,
    int TotalMethods,
    long TotalCorrelatedSize);
