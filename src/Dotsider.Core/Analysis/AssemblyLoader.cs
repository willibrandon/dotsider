using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Shared factory for opening assembly files. Handles apphosts (companion .dll redirect),
/// single-file bundles (entry assembly extraction), Native AOT binaries, and direct
/// .dll/.exe loading. Returns an <see cref="AssemblyOpenResult"/> that preserves the
/// distinction so callers can decide how to present each case (e.g. showing an apphost dialog).
/// </summary>
public static class AssemblyLoader
{
    /// <summary>
    /// Opens an assembly from the given path, detecting apphosts and single-file bundles.
    /// </summary>
    /// <param name="filePath">Path to the file to open.</param>
    /// <returns>
    /// An <see cref="AssemblyOpenResult"/> describing the result:
    /// <see cref="AssemblyOpenResult.Direct"/> for regular assemblies,
    /// <see cref="AssemblyOpenResult.ApphostWithCompanion"/> for native apphosts with a companion .dll,
    /// <see cref="AssemblyOpenResult.BundleEntry"/> for single-file bundles,
    /// or <see cref="AssemblyOpenResult.NativeAot"/> for Native AOT compiled binaries.
    /// </returns>
    public static AssemblyOpenResult Open(string filePath)
    {
        var analyzer = new AssemblyAnalyzer(filePath);

        // If it has metadata, it's a regular managed assembly
        if (analyzer.HasMetadata)
            return new AssemblyOpenResult.Direct(analyzer);

        // No metadata — try apphost companion .dll
        var companion = ApphostDetector.FindCompanionDll(filePath);
        if (companion is not null)
            return new AssemblyOpenResult.ApphostWithCompanion(analyzer, companion);

        // No companion — try single-file bundle
        var bundled = ApphostDetector.FindBundledEntryAssembly(filePath);
        if (bundled is not null)
        {
            analyzer.Dispose();
            var entryAnalyzer = new AssemblyAnalyzer(
                bundled.Value.Bytes, filePath, sourceBundlePath: filePath,
                displayName: bundled.Value.Name);
            return new AssemblyOpenResult.BundleEntry(entryAnalyzer, filePath);
        }

        // Validated ReadyToRun header with no COR header — Native AOT compiled .NET.
        // Probed only after the bundle check: R2R assemblies inside a bundle also
        // contain RTR signatures.
        if (analyzer.NativeAotInfo is not null)
            return new AssemblyOpenResult.NativeAot(analyzer);

        // Native binary with no metadata (unknown format)
        return new AssemblyOpenResult.Direct(analyzer);
    }
}
