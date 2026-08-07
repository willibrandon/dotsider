using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Protocol;

/// <summary>
/// Native AOT identity and sidecar facts.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record NativeAotInfoPayload(
    string FilePath,
    string FileName,
    long FileSize,
    string Architecture,
    BinaryKind BinaryKind,
    NativeAotInfo? NativeAotInfo,
    int ReadyToRunSections,
    int RecoveredTypes,
    int RecoveredMethods,
    int FrozenStrings,
    int NativeSymbolCount,
    NativeSymbolSource? NativeSymbolSource,
    NativeSymbolStatus? NativeSymbolStatus,
    string? MstatPath,
    bool HasMstat,
    string? MstatFormat,
    string? DgmlPath,
    bool HasDgml,
    PreIlcSummary? PreIlc);
