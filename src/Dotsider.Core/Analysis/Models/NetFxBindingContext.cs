using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;

namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Per-root metadata required to drive a CLR-accurate .NET Framework bind. Built once per
/// analyzed root via <see cref="TryBuild"/>; carried alongside the analyzer through every
/// resolution surface (Dep Graph, IL navigation, General-tab drill-in, type-forwarder chase)
/// so that every code path produces the same answer for any .NET Framework reference.
/// </summary>
/// <param name="EntryAssemblyPath">Path to the root EXE/DLL.</param>
/// <param name="AppBaseDirectory">Application base — the directory containing the entry assembly.</param>
/// <param name="ConfigPath">Adjacent <c>*.exe.config</c>/<c>*.dll.config</c>, or <see langword="null"/>.</param>
/// <param name="TargetFramework">
/// Target framework moniker (e.g. <c>.NETFramework,Version=v4.8</c>), or <see langword="null"/>
/// when the root assembly carries no <c>TargetFrameworkAttribute</c> — typical for CLR 2 roots
/// (.NET Framework 2.0 / 3.0 / 3.5), where the runtime version is inferred from the
/// <c>mscorlib</c> assembly reference instead.
/// </param>
/// <param name="EffectiveArchitecture">Runtime process bitness for the root.</param>
/// <param name="Policy">Layered binding policy (framework unification + machine + publisher + app).</param>
/// <param name="PrivatePaths">
/// <c>&lt;probing privatePath&gt;</c> entries from the app config, rooted at
/// <paramref name="AppBaseDirectory"/>.
/// </param>
/// <param name="GacRoots">
/// Roots to scan when probing the GAC. Defaults to <c>[%WINDIR%\Microsoft.NET\assembly]</c> for
/// <see cref="NetFxRuntimeVersion.Clr4"/> and <c>[%WINDIR%\assembly]</c> for
/// <see cref="NetFxRuntimeVersion.Clr2"/>; tests may inject additional roots to exercise
/// publisher-policy discovery without touching the system GAC.
/// </param>
/// <param name="RuntimeVersion">
/// CLR generation the root targets. Drives every per-runtime difference: GAC layout, GAC token
/// format, machine.config path, framework runtime directory, reference-assemblies tree, and
/// <c>appliesTo</c> filtering.
/// </param>
/// <param name="IsRuntimeVersionInferred">
/// <see langword="true"/> when <see cref="RuntimeVersion"/> was determined from the
/// <c>mscorlib</c> assembly reference rather than from a <c>TargetFrameworkAttribute</c> whose
/// value pinned the runtime. Stays <see langword="true"/> for a CLR 2 root that carries a real
/// <c>.NETFramework,Version=v3.5</c> TFM (TFM is read but the gate fires on mscorlib v2).
/// </param>
public sealed record NetFxBindingContext(
    string EntryAssemblyPath,
    string AppBaseDirectory,
    string? ConfigPath,
    string? TargetFramework,
    NetFxArchitecture EffectiveArchitecture,
    BindingPolicy Policy,
    IReadOnlyList<string> PrivatePaths,
    IReadOnlyList<string> GacRoots,
    NetFxRuntimeVersion RuntimeVersion = NetFxRuntimeVersion.Clr4,
    bool IsRuntimeVersionInferred = false)
{
    /// <summary>
    /// Builds a context for a .NET Framework root, or returns <see langword="null"/> for any
    /// other target. .NET Core / .NET 5+ analyzers always receive a <see langword="null"/>
    /// context and fall back to the existing probe chain unchanged.
    /// </summary>
    /// <remarks>
    /// Detection branches:
    /// <list type="number">
    ///   <item>TFM starts with <c>.NETFramework,Version=v4</c> →
    ///     <see cref="NetFxRuntimeVersion.Clr4"/> context.</item>
    ///   <item>Assembly references <c>mscorlib, Version=2.x.x.x, PKT=b77a5c561934e089</c> →
    ///     <see cref="NetFxRuntimeVersion.Clr2"/> context with
    ///     <see cref="IsRuntimeVersionInferred"/> set to <see langword="true"/>. Catches
    ///     net20 / net30 / net35 roots whether or not they carry a
    ///     <c>TargetFrameworkAttribute</c> (the attribute didn't exist before .NET 4.0).</item>
    ///   <item>Otherwise → <see langword="null"/>.</item>
    /// </list>
    /// </remarks>
    /// <param name="rootAnalyzer">The root assembly analyzer.</param>
    /// <returns>A populated context, or <see langword="null"/> for non-.NET-Framework roots.</returns>
    public static NetFxBindingContext? TryBuild(AssemblyAnalyzer rootAnalyzer)
    {
        var tfm = rootAnalyzer.TargetFramework;
        var isClr4 = tfm is not null
            && tfm.StartsWith(".NETFramework,Version=v4", StringComparison.OrdinalIgnoreCase);
        var isClr2 = !isClr4 && LooksLikeClr2(rootAnalyzer);
        if (!isClr4 && !isClr2) return null;

        var runtimeVersion = isClr4 ? NetFxRuntimeVersion.Clr4 : NetFxRuntimeVersion.Clr2;
        var inferred = !isClr4;

        var entryPath = rootAnalyzer.FilePath;
        var appBase = Path.GetDirectoryName(entryPath) ?? Environment.CurrentDirectory;
        var configCandidate = entryPath + ".config";
        var configPath = File.Exists(configCandidate) ? configCandidate : null;

        var arch = DetectArchitecture(rootAnalyzer);
        var gacRoots = DefaultGacRoots(runtimeVersion);
        var policy = BindingPolicy.LoadFrom(configPath, arch, gacRoots, runtimeVersion);

        // Reuse the parser that drives BindingPolicy: it already filters <assemblyBinding>
        // blocks by appliesTo, so privatePath segments declared under blocks scoped to the
        // wrong runtime (e.g. v4.0.30319 in a Clr2 context) are excluded.
        var privatePaths = BindingPolicy
            .ParseConfigFile(configPath, PolicyLayer.AppConfig, runtimeVersion)
            .PrivatePaths;

        return new NetFxBindingContext(
            EntryAssemblyPath: entryPath,
            AppBaseDirectory: appBase,
            ConfigPath: configPath,
            TargetFramework: tfm,
            EffectiveArchitecture: arch,
            Policy: policy,
            PrivatePaths: privatePaths,
            GacRoots: gacRoots,
            RuntimeVersion: runtimeVersion,
            IsRuntimeVersionInferred: inferred);
    }

    private static bool LooksLikeClr2(AssemblyAnalyzer analyzer) =>
        analyzer.AssemblyRefs.Any(r =>
            string.Equals(r.Name, "mscorlib", StringComparison.OrdinalIgnoreCase) &&
            r.Version.StartsWith("2.", StringComparison.Ordinal) &&
            string.Equals(r.PublicKeyToken, "b77a5c561934e089", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Returns the GAC sub-directories in the architecture-prioritized scan list. The shape
    /// differs by CLR:
    /// <list type="bullet">
    ///   <item><see cref="NetFxRuntimeVersion.Clr4"/>: <c>GAC_MSIL</c> + <c>GAC_64</c> for Amd64,
    ///     <c>GAC_MSIL</c> + <c>GAC_32</c> for X86. The bare <c>GAC</c> bucket is reached via
    ///     <see cref="LegacyGacScanList"/> for the COM-PIA fallback.</item>
    ///   <item><see cref="NetFxRuntimeVersion.Clr2"/>: <c>GAC_MSIL</c> + arch + bare <c>GAC</c>
    ///     (CLR 1.x carryover, still consulted by CLR2 fusion). All three are scanned with the
    ///     legacy <c>&lt;version&gt;__&lt;pkt&gt;</c> token format.</item>
    /// </list>
    /// </summary>
    /// <returns>Absolute paths to the GAC sub-directories the binder should scan, in order.</returns>
    public IReadOnlyList<string> GacScanList()
    {
        var arch = EffectiveArchitecture == NetFxArchitecture.Amd64 ? "GAC_64" : "GAC_32";
        var result = new List<string>();
        foreach (var root in GacRoots)
        {
            result.Add(Path.Combine(root, "GAC_MSIL"));
            result.Add(Path.Combine(root, arch));
            if (RuntimeVersion == NetFxRuntimeVersion.Clr2)
                result.Add(Path.Combine(root, "GAC"));
        }
        return result;
    }

    /// <summary>
    /// Returns the legacy CLR 2.0 GAC sub-directories under <c>%WINDIR%\assembly</c>. For
    /// <see cref="NetFxRuntimeVersion.Clr4"/> this is the COM-PIA fallback path probed after
    /// the .NET 4 GAC scan misses (e.g. <c>stdole 7.0.3300.0</c>). For
    /// <see cref="NetFxRuntimeVersion.Clr2"/> this returns empty — the primary
    /// <see cref="GacScanList"/> already covers <c>%WINDIR%\assembly</c> directly.
    /// </summary>
    /// <returns>Absolute paths to scan, in order; empty when not on Windows or for Clr2.</returns>
    public IReadOnlyList<string> LegacyGacScanList()
    {
        if (RuntimeVersion == NetFxRuntimeVersion.Clr2) return [];
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return [];
        var windir = Environment.GetEnvironmentVariable("WINDIR");
        if (string.IsNullOrEmpty(windir)) return [];
        var root = Path.Combine(windir!, "assembly");
        if (!Directory.Exists(root)) return [];
        var arch = EffectiveArchitecture == NetFxArchitecture.Amd64 ? "GAC_64" : "GAC_32";
        return
        [
            Path.Combine(root, "GAC_MSIL"),
            Path.Combine(root, arch),
            Path.Combine(root, "GAC"),
        ];
    }

    /// <summary>
    /// Returns the architecture-correct .NET Framework runtime directory. For
    /// <see cref="NetFxRuntimeVersion.Clr4"/>: <c>%WINDIR%\Microsoft.NET\Framework[64]\v4.0.30319</c>.
    /// For <see cref="NetFxRuntimeVersion.Clr2"/>:
    /// <c>%WINDIR%\Microsoft.NET\Framework[64]\v2.0.50727</c>.
    /// </summary>
    /// <returns>The directory if it exists on disk, otherwise <see langword="null"/>.</returns>
    public string? FrameworkRuntimeDirectory()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return null;
        var windir = Environment.GetEnvironmentVariable("WINDIR");
        if (string.IsNullOrEmpty(windir)) return null;
        var subdir = EffectiveArchitecture == NetFxArchitecture.Amd64 ? "Framework64" : "Framework";
        var runtimeDir = RuntimeVersion == NetFxRuntimeVersion.Clr2 ? "v2.0.50727" : "v4.0.30319";
        var path = Path.Combine(windir!, "Microsoft.NET", subdir, runtimeDir);
        return Directory.Exists(path) ? path : null;
    }

    private static string[] DefaultGacRoots(NetFxRuntimeVersion runtimeVersion)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return [];
        var windir = Environment.GetEnvironmentVariable("WINDIR");
        if (string.IsNullOrEmpty(windir)) return [];
        // Clr4 GAC: %WINDIR%\Microsoft.NET\assembly. Clr2 GAC: %WINDIR%\assembly.
        var path = runtimeVersion == NetFxRuntimeVersion.Clr2
            ? Path.Combine(windir!, "assembly")
            : Path.Combine(windir!, "Microsoft.NET", "assembly");
        return Directory.Exists(path) ? [path] : [];
    }

    private static NetFxArchitecture DetectArchitecture(AssemblyAnalyzer analyzer)
    {
        var pe = analyzer.PeHeaders;
        var clr = analyzer.ClrHeader;
        var hostIs64 = Environment.Is64BitOperatingSystem;

        var corFlags = clr?.Flags ?? 0;
        var requires32Bit = (corFlags & CorFlags.Requires32Bit) != 0;
        var prefer32Bit = (corFlags & CorFlags.Prefers32Bit) != 0;
        var ilOnly = (corFlags & CorFlags.ILOnly) != 0;
        var machine = pe?.Machine ?? Machine.Unknown;

        if (requires32Bit) return NetFxArchitecture.X86;
        if (machine == Machine.Amd64) return NetFxArchitecture.Amd64;
        if (machine == Machine.I386 && !ilOnly) return NetFxArchitecture.X86;
        if (prefer32Bit && hostIs64) return NetFxArchitecture.X86;
        return hostIs64 ? NetFxArchitecture.Amd64 : NetFxArchitecture.X86;
    }
}
