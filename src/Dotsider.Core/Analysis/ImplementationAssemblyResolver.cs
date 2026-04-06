using System.Collections.Concurrent;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Resolves reference assemblies (e.g., System.Runtime) to their implementation
/// assemblies (e.g., System.Private.CoreLib) by probing for type forwarding.
/// </summary>
public static class ImplementationAssemblyResolver
{
    private static readonly ConcurrentDictionary<(string, string), string?> Cache = new();

    private static readonly Dictionary<string, string> KnownMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["System.Runtime"] = "System.Private.CoreLib",
        ["System.Runtime.Extensions"] = "System.Private.CoreLib",
        ["System.Runtime.InteropServices"] = "System.Private.CoreLib",
        ["System.Collections"] = "System.Private.CoreLib",
        ["System.Threading"] = "System.Private.CoreLib",
        ["System.Diagnostics.Debug"] = "System.Private.CoreLib",
        ["System.Diagnostics.Tools"] = "System.Private.CoreLib",
        ["System.Text.Encoding"] = "System.Private.CoreLib",
        ["System.IO"] = "System.Private.CoreLib",
        ["System.Reflection"] = "System.Private.CoreLib",
        ["System.Reflection.Primitives"] = "System.Private.CoreLib",
        ["System.Resources.ResourceManager"] = "System.Private.CoreLib",
        ["System.Threading.Tasks"] = "System.Private.CoreLib",
        ["System.ComponentModel"] = "System.Private.CoreLib",
        ["netstandard"] = "System.Private.CoreLib",
    };

    /// <summary>
    /// Resolves an assembly name to a path, falling back to the implementation assembly
    /// if the reference assembly has no IL.
    /// </summary>
    /// <param name="referencingAssemblyPath">The path of the assembly that references the target.</param>
    /// <param name="assemblyName">The assembly name to resolve.</param>
    /// <returns>The resolved path, or null if not found.</returns>
    public static string? Resolve(string referencingAssemblyPath, string assemblyName)
    {
        var key = (referencingAssemblyPath, assemblyName);
        if (Cache.TryGetValue(key, out var cached)) return cached;
        var result = ResolveCore(referencingAssemblyPath, assemblyName);
        Cache.TryAdd(key, result);
        return result;
    }

    private static string? ResolveCore(string referencingAssemblyPath, string assemblyName)
    {
        var directPath = AssemblyAnalyzer.ResolveAssemblyPath(referencingAssemblyPath, assemblyName);
        if (directPath is not null && HasUsableMetadata(directPath))
            return directPath;

        if (KnownMappings.TryGetValue(assemblyName, out var implName))
        {
            var implPath = AssemblyAnalyzer.ResolveAssemblyPath(referencingAssemblyPath, implName);
            if (implPath is not null) return implPath;
        }

        return directPath;
    }

    private static bool HasUsableMetadata(string path)
    {
        try
        {
            using var analyzer = new AssemblyAnalyzer(path);
            return analyzer.HasMetadata && analyzer.MethodDefs.Any(m => m.Rva > 0);
        }
        catch { return false; }
    }

    /// <summary>
    /// Clears the resolution cache. Used in tests.
    /// </summary>
    internal static void ClearCache() => Cache.Clear();
}
