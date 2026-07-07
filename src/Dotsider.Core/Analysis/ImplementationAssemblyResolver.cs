using Dotsider.Core.Analysis.Models;
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
    /// <param name="netFxBindingContext">
    /// Per-root .NET Framework binding context, or <see langword="null"/> for non-net48 roots.
    /// When supplied alongside <paramref name="referencingAnalyzer"/>, the resolver looks up the
    /// matching <see cref="AssemblyRefInfo"/> in the referencing analyzer's metadata and routes
    /// the bind through <see cref="NetFxBinder"/> for CLR-accurate framework probing. .NET Core
    /// / .NET 5+ callers pass <see langword="null"/> here and behavior is unchanged.
    /// </param>
    /// <param name="referencingAnalyzer">
    /// The analyzer for the assembly that references the target, when available. Used together
    /// with <paramref name="netFxBindingContext"/> to recover the requested AssemblyRef's full
    /// identity (version + culture + PKT) for the binder.
    /// </param>
    /// <returns>The resolved assembly, or null if not found.</returns>
    public static ResolvedAssembly? Resolve(string referencingAssemblyPath, string assemblyName,
        string? declaringType = null, string? targetFramework = null,
        string? preferredRuntimePack = null, string? sourceBundlePath = null,
        NetFxBindingContext? netFxBindingContext = null,
        AssemblyAnalyzer? referencingAnalyzer = null)
    {
        var key = (referencingAssemblyPath, assemblyName, declaringType, sourceBundlePath);
        if (netFxBindingContext is null && Cache.TryGetValue(key, out var cached)) return cached;
        var result = ResolveCore(referencingAssemblyPath, assemblyName, declaringType,
            targetFramework, preferredRuntimePack, sourceBundlePath,
            netFxBindingContext, referencingAnalyzer);
        // Don't cache netfx routes — the binder maintains its own cache keyed on the binding
        // context and identity, and the simple-name key here would collide across two requested
        // versions that redirect to the same loaded version.
        if (netFxBindingContext is null)
            Cache.TryAdd(key, result);
        return result;
    }

    private static ResolvedAssembly? ResolveCore(string referencingAssemblyPath, string assemblyName,
        string? declaringType, string? targetFramework, string? preferredRuntimePack,
        string? sourceBundlePath,
        NetFxBindingContext? netFxBindingContext,
        AssemblyAnalyzer? referencingAnalyzer)
    {
        var directResult = ResolveDirect(
            referencingAssemblyPath, assemblyName, targetFramework, preferredRuntimePack,
            sourceBundlePath, netFxBindingContext, referencingAnalyzer);

        // Partial facades (e.g. System.Collections.dll) ship real IL for some types
        // and forward others, so a whole-assembly signal like HasUsableMetadata
        // cannot tell "owned here" from "forwarded elsewhere". When the declaring
        // type is known, walk forwarders to the assembly that actually owns it.
        if (directResult is not null && declaringType is not null)
        {
            var (outcome, home) = ResolveDeclaringTypeHome(
                referencingAssemblyPath, assemblyName, directResult, declaringType,
                targetFramework, preferredRuntimePack, sourceBundlePath,
                netFxBindingContext);
            switch (outcome)
            {
                case HomeOutcome.Found:
                    return home;
                case HomeOutcome.ChaseBroken:
                    // directResult *did* reference the type (as a forwarder) but the
                    // chain broke before hitting an owning TypeDef. Falling through
                    // to HasUsableMetadata/KnownMappings here would hand callers a
                    // non-owning facade and recreate the original "method not found"
                    // failure downstream. Signal the miss explicitly instead.
                    return null;
                case HomeOutcome.NotFound:
                    break; // type never referenced by directResult — fall through
            }
        }

        if (directResult is not null && HasUsableMetadata(directResult))
            return directResult;

        if (KnownMappings.TryGetValue(assemblyName, out var implName))
        {
            var implResult = ResolveDirect(
                referencingAssemblyPath, implName, targetFramework, preferredRuntimePack,
                sourceBundlePath, netFxBindingContext, referencingAnalyzer);
            if (implResult is not null) return implResult;
        }

        return directResult;
    }

    /// <summary>
    /// Resolves a single simple-name reference. For .NET Framework roots this looks up the full
    /// <see cref="AssemblyRefInfo"/> in <paramref name="referencingAnalyzer"/>'s metadata so the
    /// bind routes through <see cref="NetFxBinder"/> with the requested identity intact.
    /// .NET Core / .NET 5+ callers fall through to <see cref="AssemblyAnalyzer.ResolveAssembly"/>.
    /// </summary>
    private static ResolvedAssembly? ResolveDirect(
        string referencingAssemblyPath, string assemblyName,
        string? targetFramework, string? preferredRuntimePack, string? sourceBundlePath,
        NetFxBindingContext? netFxBindingContext,
        AssemblyAnalyzer? referencingAnalyzer)
    {
        if (netFxBindingContext is not null && referencingAnalyzer is not null)
        {
            var asmRef = referencingAnalyzer.HasMetadata
                ? referencingAnalyzer.AssemblyRefs.FirstOrDefault(
                    r => string.Equals(r.Name, assemblyName, StringComparison.OrdinalIgnoreCase))
                : null;
            if (asmRef is not null)
            {
                var resolution = AssemblyAnalyzer.ResolveAssemblyByIdentity(
                    referencingAssemblyPath, asmRef,
                    targetFramework, preferredRuntimePack, sourceBundlePath, netFxBindingContext);
                return resolution.Resolved;
            }
        }

        return AssemblyAnalyzer.ResolveAssembly(
            referencingAssemblyPath, assemblyName, targetFramework, preferredRuntimePack, sourceBundlePath);
    }

    private enum HomeOutcome
    {
        /// <summary>An assembly owning <c>declaringType</c> as a TypeDef was reached.</summary>
        Found,

        /// <summary>
        /// <c>declaringType</c> is not referenced by the starting assembly at all —
        /// neither as a TypeDef nor as an ExportedType forwarder. Safe to fall back
        /// to other resolution strategies.
        /// </summary>
        NotFound,

        /// <summary>
        /// A forwarder for <c>declaringType</c> was found somewhere in the chain but
        /// the target could not be reached (cycle, unresolvable assembly, or chain
        /// terminated without a TypeDef match). Falling back would return a
        /// non-owning assembly, so callers must treat this as a hard miss.
        /// </summary>
        ChaseBroken,
    }

    /// <summary>
    /// Walks type forwarders starting at <paramref name="start"/> until we reach the
    /// assembly that owns <paramref name="declaringType"/> as a TypeDef.
    /// </summary>
    /// <returns>
    /// A tuple describing the outcome. <see cref="HomeOutcome.Found"/> returns the
    /// owning assembly; <see cref="HomeOutcome.NotFound"/> and
    /// <see cref="HomeOutcome.ChaseBroken"/> return <c>null</c> but carry different
    /// semantics for <see cref="ResolveCore"/>.
    /// </returns>
    private static (HomeOutcome Outcome, ResolvedAssembly? Assembly) ResolveDeclaringTypeHome(
        string referencingAssemblyPath, string startAssemblyName, ResolvedAssembly start,
        string declaringType, string? targetFramework, string? preferredRuntimePack,
        string? sourceBundlePath,
        NetFxBindingContext? netFxBindingContext)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { startAssemblyName };
        var current = start;
        var chasing = false;

        while (true)
        {
            bool ownsType;
            AssemblyRefInfo? forwardedRef;
            try
            {
                (ownsType, forwardedRef) = InspectForDeclaringType(current, declaringType);
            }
            catch
            {
                return (chasing ? HomeOutcome.ChaseBroken : HomeOutcome.NotFound, null);
            }

            if (ownsType) return (HomeOutcome.Found, current);

            if (forwardedRef is null)
                return (chasing ? HomeOutcome.ChaseBroken : HomeOutcome.NotFound, null);

            if (!visited.Add(forwardedRef.Name))
                return (HomeOutcome.ChaseBroken, null);

            // Resolve the next hop using the full identity recorded by *this* assembly's
            // forwarder. For net48 roots that means routing through NetFxBinder with the
            // forwarder's recorded version/PKT — not whatever AssemblyRef the original
            // referencer happened to declare for the same simple name.
            ResolvedAssembly? next;
            if (netFxBindingContext is not null)
            {
                var currentPath = current is ResolvedAssembly.FromFile f ? f.Path : referencingAssemblyPath;
                var resolution = AssemblyAnalyzer.ResolveAssemblyByIdentity(
                    currentPath, forwardedRef,
                    targetFramework, preferredRuntimePack, sourceBundlePath, netFxBindingContext);
                next = resolution.Resolved;
            }
            else
            {
                next = AssemblyAnalyzer.ResolveAssembly(
                    referencingAssemblyPath, forwardedRef.Name,
                    targetFramework, preferredRuntimePack, sourceBundlePath);
            }

            if (next is null) return (HomeOutcome.ChaseBroken, null);

            current = next;
            chasing = true;
        }
    }

    /// <summary>
    /// Opens the assembly once and reports either "owns the type as a TypeDef" or
    /// "forwards it to AssemblyRef N" (or neither). The forwarded ref carries the *current*
    /// assembly's full identity for the target — name, version, culture, PKT — so subsequent
    /// netfx binds use full identity rather than re-looking-up against the original referencer.
    /// </summary>
    /// <param name="resolved">The assembly to inspect.</param>
    /// <param name="declaringType">The type whose owning assembly is sought.</param>
    /// <returns>
    /// <c>OwnsType=true</c> when this assembly defines <paramref name="declaringType"/> as a
    /// TypeDef. Otherwise <c>ForwardedTo</c> carries the full identity of the next-hop assembly
    /// when this assembly forwards <paramref name="declaringType"/>; <see langword="null"/> when
    /// the type is not referenced at all.
    /// </returns>
    private static (bool OwnsType, AssemblyRefInfo? ForwardedTo) InspectForDeclaringType(
        ResolvedAssembly resolved, string declaringType)
    {
        Stream stream = resolved switch
        {
            ResolvedAssembly.FromFile(var path) => File.OpenRead(path),
            ResolvedAssembly.FromBundle(var bytes, _, _) => new MemoryStream(bytes, writable: false),
            _ => null!
        };

        if (stream is null) return (false, null);

        using (stream)
        using (var pe = new PEReader(stream))
        {
            if (!pe.HasMetadata) return (false, null);
            var reader = pe.GetMetadataReader();

            foreach (var h in reader.TypeDefinitions)
            {
                if (GetTypeDefFullName(reader, h) == declaringType)
                    return (true, null);
            }

            foreach (var h in reader.ExportedTypes)
            {
                if (GetExportedTypeFullName(reader, h) != declaringType) continue;
                var forwarded = GetForwardedAssemblyRef(reader, h);
                if (forwarded is not null) return (false, forwarded);
            }
        }

        return (false, null);
    }

    /// <summary>
    /// Builds the full name for a TypeDef, walking <see cref="TypeDefinition.GetDeclaringType"/>
    /// for nested types. Uses '/' as the nesting separator to match the convention
    /// produced by <see cref="GetExportedTypeFullName"/>.
    /// </summary>
    private static string GetTypeDefFullName(MetadataReader reader, TypeDefinitionHandle handle)
    {
        var td = reader.GetTypeDefinition(handle);
        var name = reader.GetString(td.Name);
        if (td.IsNested)
        {
            var parent = GetTypeDefFullName(reader, td.GetDeclaringType());
            return $"{parent}/{name}";
        }

        var ns = reader.GetString(td.Namespace);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
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
    /// Follows the Implementation chain from an exported type up to the root AssemblyReference,
    /// returning the full identity (name, version, culture, PKT) for that reference.
    /// </summary>
    /// <param name="reader">Metadata reader for the assembly that owns the exported type.</param>
    /// <param name="handle">The exported type handle whose forwarder target is sought.</param>
    /// <returns>The forwarded reference's full identity, or <see langword="null"/>.</returns>
    private static AssemblyRefInfo? GetForwardedAssemblyRef(MetadataReader reader, ExportedTypeHandle handle)
    {
        var impl = reader.GetExportedType(handle).Implementation;
        while (impl.Kind == HandleKind.ExportedType)
            impl = reader.GetExportedType((ExportedTypeHandle)impl).Implementation;

        if (impl.Kind != HandleKind.AssemblyReference) return null;
        var asmRef = reader.GetAssemblyReference((AssemblyReferenceHandle)impl);
        var name = reader.GetString(asmRef.Name);
        var version = asmRef.Version?.ToString() ?? string.Empty;
        var culture = reader.GetString(asmRef.Culture);
        if (string.IsNullOrEmpty(culture)) culture = "neutral";
        string? pkt = null;
        if (!asmRef.PublicKeyOrToken.IsNil)
        {
            var bytes = reader.GetBlobBytes(asmRef.PublicKeyOrToken);
            if (bytes.Length == 8)
                pkt = Convert.ToHexStringLower(bytes);
            // .NET Framework AssemblyRefs always store a PKT (8 bytes), not a full public key,
            // so we don't compute SHA1+truncate here. If a non-8-byte blob slips through, we
            // intentionally leave PKT null and let the binder treat the ref as weak-named.
        }
        return new AssemblyRefInfo(name, version, culture, pkt);
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
