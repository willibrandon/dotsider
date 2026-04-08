using System.Collections.Concurrent;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Resolves reference assemblies (e.g., System.Runtime, mscorlib) to their implementation
/// assemblies (e.g., System.Private.CoreLib) by probing for type forwarding.
/// </summary>
public static class ImplementationAssemblyResolver
{
    private static readonly ConcurrentDictionary<(string, string, string?, string?), ResolvedAssembly?> Cache = new();

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
    /// Resolves an assembly name to a path or bundle entry, falling back to the
    /// implementation assembly if the reference assembly has no IL.
    /// </summary>
    /// <param name="referencingAssemblyPath">The path of the assembly that references the target.</param>
    /// <param name="assemblyName">The assembly name to resolve.</param>
    /// <param name="declaringType">Optional declaring type for type-aware resolution (needed for mscorlib).</param>
    /// <param name="targetFramework">Target framework moniker for shared framework probing.</param>
    /// <param name="preferredRuntimePack">Preferred runtime pack to probe first.</param>
    /// <param name="sourceBundlePath">If the referencing assembly came from a bundle, the bundle path.</param>
    /// <returns>The resolved assembly, or null if not found.</returns>
    public static ResolvedAssembly? Resolve(string referencingAssemblyPath, string assemblyName,
        string? declaringType = null, string? targetFramework = null,
        string? preferredRuntimePack = null, string? sourceBundlePath = null)
    {
        var key = (referencingAssemblyPath, assemblyName, declaringType, sourceBundlePath);
        if (Cache.TryGetValue(key, out var cached)) return cached;
        var result = ResolveCore(referencingAssemblyPath, assemblyName, declaringType,
            targetFramework, preferredRuntimePack, sourceBundlePath);
        Cache.TryAdd(key, result);
        return result;
    }

    private static ResolvedAssembly? ResolveCore(string referencingAssemblyPath, string assemblyName,
        string? declaringType, string? targetFramework, string? preferredRuntimePack,
        string? sourceBundlePath)
    {
        var directResult = AssemblyAnalyzer.ResolveAssembly(
            referencingAssemblyPath, assemblyName, targetFramework, preferredRuntimePack, sourceBundlePath);
        if (directResult is not null && HasUsableMetadata(directResult))
            return directResult;

        if (KnownMappings.TryGetValue(assemblyName, out var implName))
        {
            var implResult = AssemblyAnalyzer.ResolveAssembly(
                referencingAssemblyPath, implName, targetFramework, preferredRuntimePack, sourceBundlePath);
            if (implResult is not null) return implResult;
        }

        // mscorlib is monolithic in .NET Framework but its types are spread across
        // many assemblies in .NET Core. The stub mscorlib.dll in the runtime directory
        // contains type forwarders that point each type to its real implementation
        // assembly — read those to resolve the exact assembly for the declaring type.
        if (directResult is not null && declaringType is not null)
        {
            var forwarded = ResolveViaTypeForwarders(
                referencingAssemblyPath, directResult, declaringType,
                targetFramework, preferredRuntimePack, sourceBundlePath);
            if (forwarded is not null) return forwarded;
        }

        return directResult;
    }

    private static ResolvedAssembly? ResolveViaTypeForwarders(string referencingAssemblyPath,
        ResolvedAssembly stubAssembly, string declaringType,
        string? targetFramework, string? preferredRuntimePack, string? sourceBundlePath)
    {
        try
        {
            PEReader pe;
            Stream? stream = null;
            switch (stubAssembly)
            {
                case ResolvedAssembly.FromFile(var path):
                    stream = File.OpenRead(path);
                    pe = new PEReader(stream);
                    break;
                case ResolvedAssembly.FromBundle(var bytes, _, _):
                    stream = new MemoryStream(bytes, writable: false);
                    pe = new PEReader(stream);
                    break;
                default:
                    return null;
            }

            using (stream)
            using (pe)
            {
                if (!pe.HasMetadata) return null;
                var reader = pe.GetMetadataReader();

                foreach (var handle in reader.ExportedTypes)
                {
                    var fullName = GetExportedTypeFullName(reader, handle);
                    if (fullName != declaringType) continue;

                    var asmName = GetForwardedAssemblyName(reader, handle);
                    if (asmName is null) continue;
                    return AssemblyAnalyzer.ResolveAssembly(
                        referencingAssemblyPath, asmName,
                        targetFramework, preferredRuntimePack, sourceBundlePath);
                }
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

    private static bool HasUsableMetadata(ResolvedAssembly resolved)
    {
        try
        {
            Stream? stream = null;
            switch (resolved)
            {
                case ResolvedAssembly.FromFile(var path):
                    stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                    break;
                case ResolvedAssembly.FromBundle(var bytes, _, _):
                    stream = new MemoryStream(bytes, writable: false);
                    break;
                default:
                    return false;
            }

            using (stream)
            using (var pe = new PEReader(stream))
            {
                if (!pe.HasMetadata)
                    return false;

                var reader = pe.GetMetadataReader();

                // Fast path: reference assemblies carry ReferenceAssemblyAttribute
                // on the assembly definition and never have IL bodies.
                foreach (var handle in reader.GetCustomAttributes(EntityHandle.AssemblyDefinition))
                {
                    var attr = reader.GetCustomAttribute(handle);
                    if (attr.Constructor.Kind == HandleKind.MemberReference)
                    {
                        var ctor = reader.GetMemberReference((MemberReferenceHandle)attr.Constructor);
                        if (ctor.Parent.Kind == HandleKind.TypeReference)
                        {
                            var typeRef = reader.GetTypeReference((TypeReferenceHandle)ctor.Parent);
                            if (reader.GetString(typeRef.Name) == "ReferenceAssemblyAttribute"
                                && reader.GetString(typeRef.Namespace) == "System.Runtime.CompilerServices")
                            {
                                return false;
                            }
                        }
                    }
                }

                // Stub assemblies (e.g. mscorlib) have metadata and type forwarders
                // but no IL bodies. Check that at least one method has an RVA,
                // stopping at the first hit rather than enumerating every MethodDef.
                foreach (var methodHandle in reader.MethodDefinitions)
                {
                    if (reader.GetMethodDefinition(methodHandle).RelativeVirtualAddress > 0)
                        return true;
                }

                return false;
            }
        }
        catch { return false; }
    }

    /// <summary>
    /// Clears the resolution cache. Used in tests.
    /// </summary>
    internal static void ClearCache() => Cache.Clear();
}
