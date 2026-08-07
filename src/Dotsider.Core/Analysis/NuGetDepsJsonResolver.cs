using Dotsider.Core.Analysis.Models;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Resolves assembly references by consulting the referencing assembly's <c>.deps.json</c>
/// file to locate its NuGet dependencies in the NuGet global packages folder. This is the
/// probe step that makes library projects work — <c>dotnet build</c> does not copy NuGet
/// package assemblies next to a library's <c>bin</c> output, but the <c>.deps.json</c>
/// manifest records the exact resolved package version and runtime asset path, matching
/// what the .NET host uses at runtime. Manifest paths are treated as untrusted and must
/// remain inside the selected package in the configured global packages folder.
/// </summary>
public static class NuGetDepsJsonResolver
{
    /// <summary>
    /// Attempts to locate <paramref name="assemblyName"/> in the referencing assembly's
    /// <c>.deps.json</c> manifest and resolve it against the NuGet global packages folder.
    /// </summary>
    /// <param name="referencingAssemblyPath">Path of the assembly whose <c>.deps.json</c> is consulted.</param>
    /// <param name="assemblyName">Simple name of the assembly to locate (e.g. <c>Newtonsoft.Json</c>).</param>
    /// <returns>
    /// A <see cref="ResolvedAssembly.FromFile"/> pointing at a contained packaged DLL, or
    /// <see langword="null"/> when the dependency is absent or its manifest path is unsafe.
    /// </returns>
    public static ResolvedAssembly? TryResolve(
        string referencingAssemblyPath,
        string assemblyName) =>
        TryResolve(referencingAssemblyPath, assemblyName, GetNuGetPackagesRoots());

    /// <summary>
    /// Resolves an assembly through a caller-supplied set of NuGet global package roots.
    /// </summary>
    /// <param name="referencingAssemblyPath">Path of the assembly whose <c>.deps.json</c> is consulted.</param>
    /// <param name="assemblyName">Simple name of the assembly to locate.</param>
    /// <param name="packageRoots">Trusted NuGet global package roots in probe order.</param>
    /// <returns>
    /// A <see cref="ResolvedAssembly.FromFile"/> pointing at a contained packaged DLL, or
    /// <see langword="null"/> when the dependency is absent or its manifest path is unsafe.
    /// </returns>
    internal static ResolvedAssembly? TryResolve(
        string referencingAssemblyPath,
        string assemblyName,
        IEnumerable<string> packageRoots)
    {
        var depsJsonPath = FindDepsJsonFor(referencingAssemblyPath);
        if (depsJsonPath is null) return null;

        string? runtimeRelative;
        string? packagePath;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllBytes(depsJsonPath));
            if (!TryFindRuntimeAsset(
                doc.RootElement,
                assemblyName,
                out string? libraryKey,
                out runtimeRelative))
                return null;

            packagePath = libraryKey is null
                ? null
                : ReadLibraryPath(doc.RootElement, libraryKey);
        }
        catch (IOException) { return null; }
        catch (JsonException) { return null; }
        catch (UnauthorizedAccessException) { return null; }

        if (packagePath is null ||
            runtimeRelative is null ||
            !ContainedPathResolver.IsSafeRelativePath(packagePath) ||
            !ContainedPathResolver.IsSafeRelativePath(runtimeRelative))
        {
            return null;
        }

        foreach (var root in packageRoots)
        {
            if (string.IsNullOrWhiteSpace(root) ||
                !ContainedPathResolver.TryResolveExistingDirectory(
                    root,
                    packagePath,
                    out var packageDirectory) ||
                !ContainedPathResolver.TryResolveExistingFile(
                    packageDirectory,
                    runtimeRelative,
                    out var candidate))
            {
                continue;
            }

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
                    var fileName = GetPortableFileNameWithoutExtension(asset.Name);
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

    private static string GetPortableFileNameWithoutExtension(string path)
    {
        var separator = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
        var fileName = path.AsSpan(separator + 1);
        var extension = fileName.LastIndexOf('.');
        return extension < 0
            ? fileName.ToString()
            : fileName[..extension].ToString();
    }

    private static string? ReadLibraryPath(JsonElement root, string libraryKey)
    {
        if (!root.TryGetProperty("libraries", out var libs)
            || libs.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!libs.TryGetProperty(libraryKey, out var entry) || entry.ValueKind != JsonValueKind.Object)
            return null;

        if (!entry.TryGetProperty("type", out var type) ||
            type.ValueKind != JsonValueKind.String ||
            !string.Equals(type.GetString(), "package", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (entry.TryGetProperty("path", out var pathProp) && pathProp.ValueKind == JsonValueKind.String)
            return pathProp.GetString();

        var slash = libraryKey.IndexOf('/');
        return slash > 0
            ? $"{libraryKey[..slash].ToLowerInvariant()}/{libraryKey[(slash + 1)..]}"
            : null;
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
