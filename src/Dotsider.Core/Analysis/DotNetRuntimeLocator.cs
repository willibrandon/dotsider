using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Discovers system .NET installations and resolves shared framework assembly paths.
/// </summary>
public static class DotNetRuntimeLocator
{
    private static string? _cachedBasePath;
    private static bool _basePathResolved;
    private static readonly ConcurrentDictionary<(string, string?, string?), string?> Cache = new();

    private static readonly string[] RuntimePacks =
    [
        "Microsoft.NETCore.App",
        "Microsoft.WindowsDesktop.App",
        "Microsoft.AspNetCore.App",
        "Microsoft.AspNetCore.All"
    ];

    /// <summary>
    /// Finds an assembly in the system .NET shared framework installation,
    /// matching the closest runtime version to the target framework.
    /// </summary>
    /// <param name="assemblyName">Assembly name without extension (e.g. "System.Runtime").</param>
    /// <param name="targetFramework">
    /// Target framework moniker (e.g. ".NETCoreApp,Version=v10.0"). Used for version matching.
    /// </param>
    /// <param name="preferredRuntimePack">
    /// If specified, this runtime pack is probed first (e.g. "Microsoft.AspNetCore.App").
    /// </param>
    /// <returns>Full path to the assembly, or <c>null</c> if not found.</returns>
    public static string? FindAssemblyInSharedFramework(
        string assemblyName, string? targetFramework, string? preferredRuntimePack = null)
    {
        var key = (assemblyName, targetFramework, preferredRuntimePack);
        if (Cache.TryGetValue(key, out var cached))
            return cached;

        var result = FindAssemblyCore(assemblyName, targetFramework, preferredRuntimePack);
        Cache.TryAdd(key, result);
        return result;
    }

    /// <summary>
    /// Discovers the system .NET installation base path.
    /// Checks <c>DOTNET_ROOT</c>, then <c>dotnet</c> in PATH with symlink resolution,
    /// then well-known system paths.
    /// </summary>
    /// <returns>The base path (e.g. "/usr/share/dotnet"), or <c>null</c> if not found.</returns>
    internal static string? FindDotNetBasePath()
    {
        if (_basePathResolved)
            return _cachedBasePath;

        _cachedBasePath = FindDotNetBasePathCore();
        _basePathResolved = true;
        return _cachedBasePath;
    }

    /// <summary>
    /// Clears the resolution cache. Used in tests.
    /// </summary>
    internal static void ClearCache()
    {
        Cache.Clear();
        _cachedBasePath = null;
        _basePathResolved = false;
    }

    private static string? FindAssemblyCore(
        string assemblyName, string? targetFramework, string? preferredRuntimePack)
    {
        var basePath = FindDotNetBasePath();
        if (basePath is null)
            return null;

        var targetVersion = ParseTargetFrameworkVersion(targetFramework);

        IEnumerable<string> packs = RuntimePacks;
        if (preferredRuntimePack is not null)
            packs = new[] { preferredRuntimePack }.Concat(packs).Distinct();

        foreach (var pack in packs)
        {
            var packDir = Path.Combine(basePath, "shared", pack);
            if (!Directory.Exists(packDir))
                continue;

            var versionFolder = GetClosestVersionFolder(packDir, targetVersion);
            if (versionFolder is null)
                continue;

            var dllPath = Path.Combine(packDir, versionFolder, $"{assemblyName}.dll");
            if (File.Exists(dllPath))
                return dllPath;

            var exePath = Path.Combine(packDir, versionFolder, $"{assemblyName}.exe");
            if (File.Exists(exePath))
                return exePath;
        }

        return null;
    }

    private static string? GetClosestVersionFolder(string packDir, Version targetVersion)
    {
        DirectoryInfo[] dirs;
        try
        {
            dirs = new DirectoryInfo(packDir).GetDirectories();
        }
        catch
        {
            return null;
        }

        var versions = dirs
            .Select(d => (version: ParseVersionFolder(d.Name), directory: d))
            .Where(v => v.version is not null)
            .OrderBy(v => v.version)
            .ToList();

        // Find the lowest installed version >= target
        foreach (var (version, directory) in versions)
        {
            if (version! >= targetVersion
                && directory.EnumerateFiles("*.dll", SearchOption.TopDirectoryOnly).Any())
            {
                return directory.Name;
            }
        }

        // If no version >= target, use the highest available
        for (var i = versions.Count - 1; i >= 0; i--)
        {
            if (versions[i].directory.EnumerateFiles("*.dll", SearchOption.TopDirectoryOnly).Any())
                return versions[i].directory.Name;
        }

        return null;
    }

    private static Version? ParseVersionFolder(string name)
    {
        // Strip trailing version info like "-preview1" or "-rc.1"
        var dashIndex = name.IndexOf('-');
        var versionStr = dashIndex > 0 ? name[..dashIndex] : name;

        return Version.TryParse(versionStr, out var version) ? version : null;
    }

    private static Version ParseTargetFrameworkVersion(string? targetFramework)
    {
        // Parse ".NETCoreApp,Version=v10.0" or "net10.0" style TFMs
        if (targetFramework is not null)
        {
            var versionIndex = targetFramework.IndexOf("Version=v", StringComparison.OrdinalIgnoreCase);
            if (versionIndex >= 0)
            {
                var versionStr = targetFramework[(versionIndex + "Version=v".Length)..];
                if (Version.TryParse(versionStr, out var v))
                    return v;
            }
        }

        // Default to the highest available so framework assemblies without a TFM
        // (e.g. System.Runtime.dll reached via navigation) resolve against the
        // newest installed runtime rather than the oldest.
        return new Version(int.MaxValue, 0);
    }

    private static string? FindDotNetBasePathCore()
    {
        // 1. DOTNET_ROOT / DOTNET_ROOT(x86)
        var dotnetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrEmpty(dotnetRoot) && Directory.Exists(dotnetRoot))
            return dotnetRoot;

        var dotnetRootX86 = Environment.GetEnvironmentVariable("DOTNET_ROOT(x86)");
        if (!string.IsNullOrEmpty(dotnetRootX86) && Directory.Exists(dotnetRootX86))
            return dotnetRootX86;

        // 2. Find dotnet in PATH, resolve symlinks on Unix
        var pathResult = FindDotNetInPath();
        if (pathResult is not null)
            return pathResult;

        // 3. Well-known paths
        string[] wellKnownPaths = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? [@"C:\Program Files\dotnet", @"C:\Program Files (x86)\dotnet"]
            : ["/usr/share/dotnet", "/usr/local/share/dotnet"];

        foreach (var path in wellKnownPaths)
        {
            if (Directory.Exists(Path.Combine(path, "shared")))
                return path;
        }

        return null;
    }

    private static string? FindDotNetInPath()
    {
        var dotnetExeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "dotnet.exe"
            : "dotnet";

        var pathVar = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVar))
            return null;

        foreach (var item in pathVar.Split(Path.PathSeparator))
        {
            try
            {
                var fileName = Path.Combine(item, dotnetExeName);
                if (!File.Exists(fileName))
                    continue;

                // On Unix, resolve symlinks
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var info = new FileInfo(fileName);
                    if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        var resolved = ResolveSymlink(fileName);
                        if (resolved is not null && File.Exists(resolved))
                            fileName = resolved;
                    }
                }

                var dir = Path.GetDirectoryName(fileName);
                if (dir is not null && Directory.Exists(Path.Combine(dir, "shared")))
                    return dir;
            }
            catch (ArgumentException)
            {
                // Invalid path component
            }
        }

        return null;
    }

    private static string? ResolveSymlink(string path)
    {
        try
        {
            var target = File.ResolveLinkTarget(path, returnFinalTarget: true);
            return target?.FullName;
        }
        catch
        {
            return null;
        }
    }
}
