using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Protocol;

/// <summary>
/// All string categories extracted from a binary.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record StringsPayload(
    IReadOnlyList<StringEntry> UserStrings,
    IReadOnlyList<StringEntry> MetadataStrings,
    IReadOnlyList<StringEntry> RawStrings,
    IReadOnlyList<StringEntry> RawUtf16Strings,
    IReadOnlyList<StringEntry> FrozenStrings);
