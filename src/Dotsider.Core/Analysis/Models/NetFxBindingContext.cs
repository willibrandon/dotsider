using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;

namespace Dotsider.Core.Analysis.Models;

/// <summary>
/// Per-root metadata required to drive a CLR-accurate .NET Framework bind. Built once per
/// analyzed root via <see cref="TryBuild"/>; carried alongside the analyzer through every
/// resolution surface (Dep Graph, IL navigation, General-tab drill-in, type-forwarder chase)
/// so that every code path produces the same answer for any net48 reference.
/// </summary>
/// <param name="EntryAssemblyPath">Path to the root EXE/DLL.</param>
/// <param name="AppBaseDirectory">Application base — the directory containing the entry assembly.</param>
/// <param name="ConfigPath">Adjacent <c>*.exe.config</c>/<c>*.dll.config</c>, or <see langword="null"/>.</param>
/// <param name="TargetFramework">Target framework moniker (e.g. <c>.NETFramework,Version=v4.8</c>).</param>
/// <param name="EffectiveArchitecture">Runtime process bitness for the root.</param>
/// <param name="Policy">Layered binding policy (framework unification + machine + publisher + app).</param>
/// <param name="PrivatePaths">
/// <c>&lt;probing privatePath&gt;</c> entries from the app config, rooted at
/// <paramref name="AppBaseDirectory"/>.
/// </param>
/// <param name="GacRoots">
/// Roots to scan when probing the GAC. Defaults to <c>[%WINDIR%\Microsoft.NET\assembly]</c>;
/// tests may inject additional roots to exercise publisher-policy discovery without touching the
/// system GAC.
/// </param>
public sealed record NetFxBindingContext(
    string EntryAssemblyPath,
    string AppBaseDirectory,
    string? ConfigPath,
    string TargetFramework,
    NetFxArchitecture EffectiveArchitecture,
    BindingPolicy Policy,
    IReadOnlyList<string> PrivatePaths,
    IReadOnlyList<string> GacRoots)
{
    /// <summary>
    /// Builds a context for a .NET Framework root, or returns <see langword="null"/> for any
    /// other target framework. .NET Core / .NET 5+ analyzers always receive a <see langword="null"/>
    /// context and fall back to the existing probe chain unchanged.
    /// </summary>
    /// <param name="rootAnalyzer">The root assembly analyzer.</param>
    /// <returns>A populated context, or <see langword="null"/> for non-net48 roots.</returns>
    public static NetFxBindingContext? TryBuild(AssemblyAnalyzer rootAnalyzer)
    {
        var tfm = rootAnalyzer.TargetFramework;
        if (tfm is null || !tfm.StartsWith(".NETFramework,Version=v4", StringComparison.OrdinalIgnoreCase))
            return null;

        var entryPath = rootAnalyzer.FilePath;
        var appBase = Path.GetDirectoryName(entryPath) ?? Environment.CurrentDirectory;
        var configCandidate = entryPath + ".config";
        var configPath = File.Exists(configCandidate) ? configCandidate : null;

        var arch = DetectArchitecture(rootAnalyzer);
        var gacRoots = DefaultGacRoots();
        var policy = BindingPolicy.LoadFrom(configPath, arch, gacRoots);

        // Reuse the parser that drives BindingPolicy: it already filters <assemblyBinding>
        // blocks by appliesTo, so privatePath segments declared under non-v4 blocks (e.g.
        // appliesTo="v2.0.50727") are excluded for net48 roots.
        var privatePaths = BindingPolicy.ParseConfigFile(configPath, PolicyLayer.AppConfig).PrivatePaths;

        return new NetFxBindingContext(
            EntryAssemblyPath: entryPath,
            AppBaseDirectory: appBase,
            ConfigPath: configPath,
            TargetFramework: tfm,
            EffectiveArchitecture: arch,
            Policy: policy,
            PrivatePaths: privatePaths,
            GacRoots: gacRoots);
    }

    /// <summary>
    /// Returns the GAC sub-directories in the architecture-prioritized scan list:
    /// <c>GAC_MSIL</c> + <c>GAC_64</c> for Amd64, <c>GAC_MSIL</c> + <c>GAC_32</c> for X86.
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
        }
        return result;
    }

    /// <summary>
    /// Returns the legacy CLR 2.0 GAC sub-directories — <c>%WINDIR%\assembly\GAC_MSIL</c>,
    /// the architecture-matching <c>GAC_64</c> or <c>GAC_32</c>, and the original
    /// <c>GAC</c> (CLR 1.x). Net4 fusion still consults this cache for COM PIAs and other
    /// 2.0-registered assemblies (e.g. <c>stdole 7.0.3300.0</c>), so the binder probes
    /// these locations after the .NET 4 GAC scan misses. Token format here is
    /// <c>&lt;version&gt;__&lt;pkt&gt;</c> with no <c>v4.0_</c> prefix.
    /// </summary>
    /// <returns>Absolute paths to scan, in order; empty when not on Windows.</returns>
    public IReadOnlyList<string> LegacyGacScanList()
    {
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
    /// Returns the architecture-correct .NET Framework runtime directory:
    /// <c>%WINDIR%\Microsoft.NET\Framework64\v4.0.30319</c> for Amd64,
    /// <c>%WINDIR%\Microsoft.NET\Framework\v4.0.30319</c> for X86.
    /// </summary>
    /// <returns>The directory if it exists on disk, otherwise <see langword="null"/>.</returns>
    public string? FrameworkRuntimeDirectory()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return null;
        var windir = Environment.GetEnvironmentVariable("WINDIR");
        if (string.IsNullOrEmpty(windir)) return null;
        var subdir = EffectiveArchitecture == NetFxArchitecture.Amd64 ? "Framework64" : "Framework";
        var path = Path.Combine(windir!, "Microsoft.NET", subdir, "v4.0.30319");
        return Directory.Exists(path) ? path : null;
    }

    private static string[] DefaultGacRoots()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return [];
        var windir = Environment.GetEnvironmentVariable("WINDIR");
        if (string.IsNullOrEmpty(windir)) return [];
        var path = Path.Combine(windir!, "Microsoft.NET", "assembly");
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
