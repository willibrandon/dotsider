using Dotsider.Core.Analysis;

namespace Dotsider.Core.Protocol;

/// <summary>
/// Builds the shared assembly-information contract used by the CLI and MCP server.
/// Centralizes the mapping from an analyzer into the public protocol shape.
/// Keeps command-line and MCP responses consistent without reflection.
/// </summary>
public static class AssemblyInfoPayloadBuilder
{
    /// <summary>
    /// Builds assembly identity, format, symbol, and sidecar facts.
    /// </summary>
    public static AssemblyInfoPayload Build(AssemblyAnalyzer analyzer, string? mode = null) => new(
        mode,
        analyzer.FilePath,
        analyzer.FileName,
        analyzer.FileSize,
        analyzer.AssemblyName,
        analyzer.AssemblyVersion,
        analyzer.TargetFramework,
        analyzer.Culture,
        analyzer.PublicKeyToken,
        analyzer.Architecture,
        analyzer.HasMetadata,
        analyzer.BinaryKind,
        analyzer.NativeAotInfo,
        analyzer.DisplayName,
        analyzer.SourceBundlePath,
        analyzer.IsBundleBacked,
        analyzer.PreferredRuntimePack,
        analyzer.LaunchPath,
        analyzer.CanSaveInPlace,
        analyzer.PdbProvenance,
        analyzer.SourceLink,
        analyzer.TypeDefs.Count,
        analyzer.MethodDefs.Count,
        analyzer.AssemblyRefs.Count,
        analyzer.ReadyToRunSections.Count,
        analyzer.RecoveredTypes.Count,
        analyzer.FrozenStrings.Count,
        analyzer.NativeSymbols?.Symbols.Count ?? 0,
        analyzer.NativeSymbols?.Source,
        analyzer.NativeSymbols?.Status,
        BuildPreIlcSummary(analyzer),
        BuildReadyToRunSummary(analyzer),
        WebcilPayloadBuilder.BuildSummary(analyzer),
        WasmPayloadBuilder.BuildSummary(analyzer));

    private static PreIlcSummary? BuildPreIlcSummary(AssemblyAnalyzer analyzer)
    {
        if (analyzer.PreIlcSidecars is not { } sidecars)
            return null;

        return new PreIlcSummary(
            sidecars.HasAttachableCompanion,
            sidecars.ManagedAssemblyPath is { } path ? Path.GetFileName(path) : null,
            sidecars.Origin.ToString(),
            sidecars.PdbStatus.ToString(),
            sidecars.MstatPath is not null,
            (sidecars.CodegenDgmlPath ?? sidecars.ScanDgmlPath) is not null,
            sidecars.LocalReferencePaths.Count,
            sidecars.PackageReferenceCount,
            sidecars.OtherReferenceCount);
    }

    private static ReadyToRunSummary? BuildReadyToRunSummary(AssemblyAnalyzer analyzer)
    {
        if (analyzer.ReadyToRunInfo is not { } info)
            return null;

        return new ReadyToRunSummary(
            info.Status.ToString(),
            info.MajorVersion,
            info.MinorVersion,
            info.IsComposite,
            info.IsComponent,
            info.IsPartialImage,
            info.Architecture.ToString(),
            info.OwnerCompositeExecutable,
            analyzer.ReadyToRunIndex?.Methods.Count ?? 0,
            analyzer.ReadyToRunIndex?.InstantiationCount ?? 0,
            analyzer.ReadyToRunIndex?.TotalCodeSize ?? 0);
    }
}
