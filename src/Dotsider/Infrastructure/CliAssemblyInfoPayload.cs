using Dotsider.Core.Analysis.Models;
using Dotsider.Core.Protocol;

namespace Dotsider.Infrastructure;

/// <summary>
/// Assembly information written by the CLI.
/// Preserves the command's public JSON contract with a fixed typed shape.
/// Is registered with source-generated JSON metadata for Native AOT.
/// </summary>
internal sealed record CliAssemblyInfoPayload(
    string FilePath,
    string FileName,
    long FileSize,
    string? AssemblyName,
    string? AssemblyVersion,
    string? TargetFramework,
    string Architecture,
    bool HasMetadata,
    BinaryKind BinaryKind,
    NativeAotInfo? NativeAotInfo,
    string DisplayName,
    bool IsBundleBacked,
    string? SourceBundlePath,
    string LaunchPath,
    bool CanSaveInPlace,
    string PreferredRuntimePack,
    PdbProvenance PdbProvenance,
    SourceLinkInfo SourceLink,
    IReadOnlyList<DebugDirectoryInfo> DebugDirectory,
    IReadOnlyList<RtrSection> ReadyToRunSections,
    int RecoveredTypeCount,
    int FrozenStringCount,
    int NativeSymbolCount,
    NativeSymbolSource? NativeSymbolSource,
    NativeSymbolStatus? NativeSymbolStatus,
    string? NativeSymbolsPath,
    CliPreIlcPayload? PreIlc,
    CliReadyToRunPayload? ReadyToRun,
    WebcilSummary? Webcil,
    WasmSummary? Wasm,
    IReadOnlyList<TypeDefInfo> Types,
    IReadOnlyList<MethodDefInfo> Methods,
    IReadOnlyList<AssemblyRefInfo> References);
