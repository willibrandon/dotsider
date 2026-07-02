namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// The result of opening an assembly file via <see cref="AssemblyLoader"/>,
/// distinguishing between direct loads, apphost companion redirects, and
/// single-file bundle entry extractions.
/// </summary>
public abstract record AssemblyOpenResult
{
    /// <summary>
    /// Direct load — the file is a .dll or .exe with metadata, or a native binary
    /// with no metadata and no ReadyToRun header (unknown format).
    /// </summary>
    /// <param name="Analyzer">The analyzer for the opened file.</param>
    public sealed record Direct(AssemblyAnalyzer Analyzer) : AssemblyOpenResult;

    /// <summary>
    /// The file is a Native AOT compiled .NET binary: a valid PE, ELF, or Mach-O
    /// with no COR header whose image embeds a validated ReadyToRun header. No
    /// metadata is available, but PE structure, native import/export/load-config
    /// directories, and raw strings are.
    /// </summary>
    /// <param name="Analyzer">The analyzer for the Native AOT binary (no metadata).</param>
    public sealed record NativeAot(AssemblyAnalyzer Analyzer) : AssemblyOpenResult;

    /// <summary>
    /// The file is a native apphost with a companion managed .dll on disk.
    /// The caller decides when to redirect (e.g. showing a dialog first).
    /// </summary>
    /// <param name="HostAnalyzer">The analyzer for the native apphost (no metadata).</param>
    /// <param name="CompanionDllPath">Full path to the companion managed .dll.</param>
    public sealed record ApphostWithCompanion(
        AssemblyAnalyzer HostAnalyzer, string CompanionDllPath) : AssemblyOpenResult;

    /// <summary>
    /// The file is a single-file bundle. The entry assembly has been extracted
    /// from the bundle and is ready for analysis.
    /// </summary>
    /// <param name="EntryAnalyzer">The analyzer for the extracted entry assembly.</param>
    /// <param name="BundlePath">Full path to the bundle file.</param>
    public sealed record BundleEntry(
        AssemblyAnalyzer EntryAnalyzer, string BundlePath) : AssemblyOpenResult;
}
