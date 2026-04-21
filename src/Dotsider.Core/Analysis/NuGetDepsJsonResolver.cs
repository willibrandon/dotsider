using System.Runtime.InteropServices;
using System.Text.Json;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Resolves assembly references by consulting the referencing assembly's <c>.deps.json</c>
/// file to locate its NuGet dependencies in the NuGet global packages folder. This is the
/// probe step that makes library projects work — <c>dotnet build</c> does not copy NuGet
/// package assemblies next to a library's <c>bin</c> output, but the <c>.deps.json</c>
/// manifest records the exact resolved package version and runtime asset path, matching
/// what the .NET host uses at runtime.
/// </summary>
public static class NuGetDepsJsonResolver
{
    /// <summary>
    /// Attempts to locate <paramref name="assemblyName"/> in the referencing assembly's
    /// <c>.deps.json</c> manifest and resolve it against the NuGet global packages folder.
    /// </summary>
    /// <param name="referencingAssemblyPath">Path of the assembly whose <c>.deps.json</c> is consulted.</param>
    /// <param name="assemblyName">Simple name of the assembly to locate (e.g. <c>Newtonsoft.Json</c>).</param>
    /// <returns>A <see cref="ResolvedAssembly.FromFile"/> pointing at the packaged dll, or <see langword="null"/>.</returns>
    public static ResolvedAssembly? TryResolve(string referencingAssemblyPath, string assemblyName)
    {
        var depsJsonPath = FindDepsJsonFor(referencingAssemblyPath);
        if (depsJsonPath is null) return null;

        string? libraryKey;
        string? runtimeRelative;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllBytes(depsJsonPath));
            if (!TryFindRuntimeAsset(doc.RootElement, assemblyName, out libraryKey, out runtimeRelative))
                return null;
        }
        catch (IOException) { return null; }
        catch (JsonException) { return null; }
        catch (UnauthorizedAccessException) { return null; }

        if (libraryKey is null || runtimeRelative is null) return null;

        var packagePath = ReadLibraryPath(depsJsonPath, libraryKey);
        if (packagePath is null) return null;

        foreach (var root in GetNuGetPackagesRoots())
        {
            var candidate = Path.Combine(root, packagePath.Replace('/', Path.DirectorySeparatorChar),
                runtimeRelative.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate))
                return new ResolvedAssembly.FromFile(candidate);
        }

        return null;
    }

    private static string? FindDepsJsonFor(string referencingAssemblyPath)
    {
        // Match only on the referencing assembly's own base name. The .NET SDK publishes
        // the deps manifest alongside the primary output as <AssemblyName>.deps.json, so a
        // direct base-name check covers every normal library and application layout. The
        // previous glob-scan fallback ran a Directory.GetFiles on every resolve call even
        // when no manifest existed (the common case for runtime-dir assemblies) and
        // added tens of microseconds of overhead per probe — a measurable slowdown on the
        // hot path of transitive graph traversal.
        var dir = Path.GetDirectoryName(referencingAssemblyPath);
        if (dir is null) return null;

        var baseName = Path.GetFileNameWithoutExtension(referencingAssemblyPath);
        var direct = Path.Combine(dir, $"{baseName}.deps.json");
        return File.Exists(direct) ? direct : null;
    }

    private static bool TryFindRuntimeAsset(
        JsonElement root, string assemblyName,
        out string? libraryKey, out string? runtimeRelative)
    {
        libraryKey = null;
        runtimeRelative = null;

        if (!root.TryGetProperty("targets", out var targets) || targets.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var target in targets.EnumerateObject())
        {
            if (target.Value.ValueKind != JsonValueKind.Object) continue;

            foreach (var lib in target.Value.EnumerateObject())
            {
                if (lib.Value.ValueKind != JsonValueKind.Object) continue;
                if (!lib.Value.TryGetProperty("runtime", out var runtime) || runtime.ValueKind != JsonValueKind.Object)
                    continue;

                foreach (var asset in runtime.EnumerateObject())
                {
                    var fileName = Path.GetFileNameWithoutExtension(asset.Name);
                    if (string.Equals(fileName, assemblyName, StringComparison.OrdinalIgnoreCase))
                    {
                        libraryKey = lib.Name;
                        runtimeRelative = asset.Name;
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static string? ReadLibraryPath(string depsJsonPath, string libraryKey)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllBytes(depsJsonPath));
            if (!doc.RootElement.TryGetProperty("libraries", out var libs)
                || libs.ValueKind != JsonValueKind.Object)
                return null;

            if (!libs.TryGetProperty(libraryKey, out var entry) || entry.ValueKind != JsonValueKind.Object)
                return null;

            if (!entry.TryGetProperty("type", out var type) || type.GetString() != "package")
                return null;

            if (entry.TryGetProperty("path", out var pathProp) && pathProp.ValueKind == JsonValueKind.String)
                return pathProp.GetString();

            var slash = libraryKey.IndexOf('/');
            return slash > 0
                ? $"{libraryKey[..slash].ToLowerInvariant()}/{libraryKey[(slash + 1)..]}"
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<string> GetNuGetPackagesRoots()
    {
        var fromEnv = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
        if (!string.IsNullOrEmpty(fromEnv))
            yield return fromEnv;

        var home = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Environment.GetEnvironmentVariable("USERPROFILE")
            : Environment.GetEnvironmentVariable("HOME");

        if (!string.IsNullOrEmpty(home))
            yield return Path.Combine(home, ".nuget", "packages");
    }
}
