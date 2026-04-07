using System.Collections.Concurrent;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Resolves reference assemblies (e.g., System.Runtime, mscorlib) to their implementation
/// assemblies (e.g., System.Private.CoreLib) by probing for type forwarding.
/// </summary>
public static class ImplementationAssemblyResolver
{
    private static readonly ConcurrentDictionary<(string, string, string?), string?> Cache = new();

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
    /// <param name="declaringType">Optional declaring type for type-aware resolution (needed for mscorlib).</param>
    /// <returns>The resolved path, or null if not found.</returns>
    public static string? Resolve(string referencingAssemblyPath, string assemblyName,
        string? declaringType = null)
    {
        var key = (referencingAssemblyPath, assemblyName, declaringType);
        if (Cache.TryGetValue(key, out var cached)) return cached;
        var result = ResolveCore(referencingAssemblyPath, assemblyName, declaringType);
        Cache.TryAdd(key, result);
        return result;
    }

    private static string? ResolveCore(string referencingAssemblyPath, string assemblyName,
        string? declaringType)
    {
        var directPath = AssemblyAnalyzer.ResolveAssemblyPath(referencingAssemblyPath, assemblyName);
        if (directPath is not null && HasUsableMetadata(directPath))
            return directPath;

        if (KnownMappings.TryGetValue(assemblyName, out var implName))
        {
            var implPath = AssemblyAnalyzer.ResolveAssemblyPath(referencingAssemblyPath, implName);
            if (implPath is not null) return implPath;
        }

        // mscorlib is monolithic in .NET Framework but its types are spread across
        // many assemblies in .NET Core. The stub mscorlib.dll in the runtime directory
        // contains type forwarders that point each type to its real implementation
        // assembly — read those to resolve the exact assembly for the declaring type.
        if (directPath is not null && declaringType is not null)
        {
            var forwarded = ResolveViaTypeForwarders(referencingAssemblyPath, directPath, declaringType);
            if (forwarded is not null) return forwarded;
        }

        return directPath;
    }

    private static string? ResolveViaTypeForwarders(string referencingAssemblyPath, string stubPath,
        string declaringType)
    {
        try
        {
            using var stream = File.OpenRead(stubPath);
            using var pe = new PEReader(stream);
            if (!pe.HasMetadata) return null;
            var reader = pe.GetMetadataReader();

            foreach (var handle in reader.ExportedTypes)
            {
                var fullName = GetExportedTypeFullName(reader, handle);
                if (fullName != declaringType) continue;

                var asmName = GetForwardedAssemblyName(reader, handle);
                if (asmName is null) continue;
                return AssemblyAnalyzer.ResolveAssemblyPath(referencingAssemblyPath, asmName);
            }
        }
        catch
        {
            // Stub might not be readable — fall through
        }
        return null;
    }

    /// <summary>
    /// Builds the full name for an exported type, following nested type parents.
    /// Uses '/' as the nesting separator to match the IL metadata convention.
    /// </summary>
    private static string GetExportedTypeFullName(MetadataReader reader, ExportedTypeHandle handle)
    {
        var exported = reader.GetExportedType(handle);
        var name = reader.GetString(exported.Name);

        if (exported.Implementation.Kind == HandleKind.ExportedType)
        {
            var parentName = GetExportedTypeFullName(reader, (ExportedTypeHandle)exported.Implementation);
            return $"{parentName}/{name}";
        }

        var ns = reader.GetString(exported.Namespace);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    /// <summary>
    /// Follows the Implementation chain from an exported type up to the root AssemblyReference.
    /// Returns the assembly name, or null if the chain doesn't end at an AssemblyReference.
    /// </summary>
    private static string? GetForwardedAssemblyName(MetadataReader reader, ExportedTypeHandle handle)
    {
        var impl = reader.GetExportedType(handle).Implementation;
        while (impl.Kind == HandleKind.ExportedType)
            impl = reader.GetExportedType((ExportedTypeHandle)impl).Implementation;

        if (impl.Kind != HandleKind.AssemblyReference) return null;
        return reader.GetString(reader.GetAssemblyReference((AssemblyReferenceHandle)impl).Name);
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
