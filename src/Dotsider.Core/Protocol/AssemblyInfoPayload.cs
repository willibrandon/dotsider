using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Protocol;

/// <summary>
/// Assembly identity and analysis capabilities exposed by protocol surfaces.
/// Defines a stable contract for command-line and MCP protocol responses.
/// Uses an explicit shape that source-generated JSON preserves in Native AOT.
/// </summary>
public sealed record AssemblyInfoPayload(
    string? Mode,
    string FilePath,
    string FileName,
    long FileSize,
    string? AssemblyName,
    string? AssemblyVersion,
    string? TargetFramework,
    string? Culture,
    string? PublicKeyToken,
    string Architecture,
    bool HasMetadata,
    BinaryKind BinaryKind,
    NativeAotInfo? NativeAotInfo,
    string DisplayName,
    string? SourceBundlePath,
    bool IsBundleBacked,
    string PreferredRuntimePack,
    string LaunchPath,
    bool CanSaveInPlace,
    PdbProvenance PdbProvenance,
    SourceLinkInfo SourceLink,
    int TypeCount,
    int MethodCount,
    int AssemblyRefCount,
    int ReadyToRunSectionCount,
    int RecoveredTypeCount,
    int FrozenStringCount,
    int NativeSymbolCount,
    NativeSymbolSource? NativeSymbolSource,
    NativeSymbolStatus? NativeSymbolStatus,
    PreIlcSummary? PreIlc,
    ReadyToRunSummary? ReadyToRun,
    WebcilSummary? Webcil,
    WasmSummary? Wasm);
