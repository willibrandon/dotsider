using Dotsider.Core.Analysis.Models;
using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Resolves reference assemblies (e.g., System.Runtime, mscorlib) to their implementation
/// assemblies (e.g., System.Private.CoreLib) by probing for type forwarding.
/// </summary>
public static class ImplementationAssemblyResolver
{
    private const TypeAttributes ForwarderAttribute = (TypeAttributes)0x0020_0000;

    private static readonly ConcurrentDictionary<
        (string, string, string?, string?, string?, string?),
        ResolvedAssembly?> Cache = new();

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
    /// Resolves an assembly name to an assembly file, bundle entry, or authenticated sibling
    /// module, falling back to the implementation assembly if the reference assembly has no IL.
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
    /// <returns>The resolved assembly or module, or <see langword="null"/> if not found.</returns>
    public static ResolvedAssembly? Resolve(string referencingAssemblyPath, string assemblyName,
        string? declaringType = null, string? targetFramework = null,
        string? preferredRuntimePack = null, string? sourceBundlePath = null,
        NetFxBindingContext? netFxBindingContext = null,
        AssemblyAnalyzer? referencingAnalyzer = null)
    {
        var key = (
            referencingAssemblyPath,
            assemblyName,
            declaringType,
            targetFramework,
            preferredRuntimePack,
            sourceBundlePath);
        if (netFxBindingContext is null && Cache.TryGetValue(key, out var cached))
        {
            return cached;
        }
        var result = ResolveCore(referencingAssemblyPath, assemblyName, declaringType,
            targetFramework, preferredRuntimePack, sourceBundlePath,
            netFxBindingContext, referencingAnalyzer);
        // Don't cache netfx routes — the binder maintains its own cache keyed on the binding
        // context and identity, and the simple-name key here would collide across two requested
        // versions that redirect to the same loaded version.
        if (netFxBindingContext is null && result is not null and not ResolvedModule)
        {
            Cache.TryAdd(key, result);
        }
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
        {
            return directResult;
        }

        if (KnownMappings.TryGetValue(assemblyName, out var implName))
        {
            var implResult = ResolveDirect(
                referencingAssemblyPath, implName, targetFramework, preferredRuntimePack,
                sourceBundlePath, netFxBindingContext, referencingAnalyzer);
            if (implResult is not null)
            {
                return implResult;
            }
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
            ResolvedAssembly? owner;
            AssemblyRefInfo? forwardedRef;
            try
            {
                (owner, forwardedRef) = InspectForDeclaringType(
                    current,
                    declaringType,
                    targetFramework,
                    preferredRuntimePack);
            }
            catch (BadImageFormatException)
            {
                return (HomeOutcome.ChaseBroken, null);
            }
            catch (IOException)
            {
                return (HomeOutcome.ChaseBroken, null);
            }
            catch (UnauthorizedAccessException)
            {
                return (HomeOutcome.ChaseBroken, null);
            }

            if (owner is not null)
            {
                return (HomeOutcome.Found, owner);
            }

            if (forwardedRef is null)
            {
                return (chasing ? HomeOutcome.ChaseBroken : HomeOutcome.NotFound, null);
            }

            if (!visited.Add(forwardedRef.Name))
            {
                return (HomeOutcome.ChaseBroken, null);
            }

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

            if (next is null)
            {
                return (HomeOutcome.ChaseBroken, null);
            }

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
    /// <param name="targetFramework">Target-framework context inherited by sibling modules.</param>
    /// <param name="preferredRuntimePack">Preferred runtime-pack context inherited by sibling modules.</param>
    /// <returns>
    /// <c>Owner</c> identifies this assembly or an authenticated sibling module when it defines
    /// <paramref name="declaringType"/>. Otherwise <c>ForwardedTo</c> carries the full identity of
    /// the next-hop assembly when this assembly forwards the type. Both are
    /// <see langword="null"/> when the type is not referenced.
    /// </returns>
    private static (ResolvedAssembly? Owner, AssemblyRefInfo? ForwardedTo) InspectForDeclaringType(
        ResolvedAssembly resolved,
        string declaringType,
        string? targetFramework,
        string? preferredRuntimePack)
    {
        Stream stream = resolved switch
        {
            ResolvedAssembly.FromFile(var path) => File.OpenRead(path),
            ResolvedAssembly.FromBundle(var bytes, _, _) => new MemoryStream(bytes, writable: false),
            ResolvedModule module => new MemoryStream([.. module.Bytes], writable: false),
            _ => null!
        };

        if (stream is null)
        {
            return (null, null);
        }

        using (stream)
        using (var pe = new PEReader(stream))
        {
            if (!pe.HasMetadata)
            {
                return (null, null);
            }
            var reader = pe.GetMetadataReader();
            var malformedMetadata = false;
            var ownsDeclaringType = false;
            var hasExportedMatch = false;
            ResolvedAssembly? exportedOwner = null;
            AssemblyRefInfo? exportedReference = null;

            foreach (var h in reader.TypeDefinitions)
            {
                var fullName = GetTypeDefFullName(reader, h);
                if (fullName is null)
                {
                    malformedMetadata = true;
                    continue;
                }

                if (fullName == declaringType)
                {
                    if (ownsDeclaringType)
                    {
                        throw new BadImageFormatException(
                            $"Type '{declaringType}' has duplicate TypeDef ownership metadata.");
                    }

                    ownsDeclaringType = true;
                }
            }

            foreach (var h in reader.ExportedTypes)
            {
                var fullName = GetExportedTypeFullName(reader, h);
                if (fullName is null)
                {
                    malformedMetadata = true;
                    continue;
                }

                if (fullName != declaringType)
                {
                    continue;
                }

                if (hasExportedMatch)
                {
                    throw new BadImageFormatException(
                        $"Type '{declaringType}' has duplicate ExportedType ownership metadata.");
                }

                hasExportedMatch = true;
                var chain = MetadataNestingWalker.ExportedTypeImplementationChain(reader, h);
                if (chain.IsComplete && chain.Terminal.Kind == HandleKind.AssemblyFile)
                {
                    if (!IsValidAssemblyFileExportChain(reader, chain))
                    {
                        throw new BadImageFormatException(
                            $"Exported type '{declaringType}' has invalid module-export metadata.");
                    }

                    var module = ResolveSiblingModule(
                        resolved,
                        reader,
                        (AssemblyFileHandle)chain.Terminal,
                        declaringType,
                        targetFramework,
                        preferredRuntimePack);
                    if (module is not null)
                    {
                        exportedOwner = module;
                        continue;
                    }

                    throw new BadImageFormatException(
                        $"Exported type '{declaringType}' has no valid sibling module.");
                }

                var forwarded = GetForwardedAssemblyRef(reader, h, out var broken);
                if (forwarded is not null)
                {
                    exportedReference = forwarded;
                    continue;
                }
                if (broken)
                {
                    throw new BadImageFormatException(
                        $"Exported type '{declaringType}' has no valid forwarding assembly.");
                }
            }

            if (ownsDeclaringType && hasExportedMatch)
            {
                throw new BadImageFormatException(
                    $"Type '{declaringType}' has conflicting TypeDef and ExportedType ownership metadata.");
            }

            if (ownsDeclaringType)
            {
                return (resolved, null);
            }

            if (exportedOwner is not null)
            {
                return (exportedOwner, null);
            }

            if (exportedReference is not null)
            {
                return (null, exportedReference);
            }

            if (malformedMetadata)
            {
                throw new BadImageFormatException(
                    "The assembly contains a malformed type ownership or forwarding chain.");
            }
        }

        return (null, null);
    }

    /// <summary>
    /// Builds the full name for a TypeDef, walking <see cref="TypeDefinition.GetDeclaringType"/>
    /// for nested types. Uses '/' as the nesting separator to match the convention
    /// produced by <see cref="GetExportedTypeFullName"/>.
    /// </summary>
    private static string? GetTypeDefFullName(MetadataReader reader, TypeDefinitionHandle handle)
    {
        var chain = MetadataNestingWalker.DeclaringTypeChain(reader, handle);
        return MetadataNestingWalker.TryFormatTypeDefinitionName(chain, out var fullName)
            ? fullName
            : null;
    }

    private static bool IsValidAssemblyFileExportChain(
        MetadataReader reader,
        ChainWalkResult<ExportedTypeHandle> chain)
    {
        try
        {
            var outermost = chain.Rest is { Count: > 0 } parents ? parents[^1] : chain.First;
            var outer = reader.GetExportedType(outermost);
            if ((outer.Attributes & TypeAttributes.VisibilityMask) != TypeAttributes.Public ||
                (outer.Attributes & ForwarderAttribute) != 0)
            {
                return false;
            }

            if (chain.Rest is not { Count: > 0 } nestedParents)
            {
                return true;
            }

            if (!IsValidNestedModuleExport(reader.GetExportedType(chain.First)))
            {
                return false;
            }

            for (var index = 0; index < nestedParents.Count - 1; index++)
            {
                if (!IsValidNestedModuleExport(reader.GetExportedType(nestedParents[index])))
                {
                    return false;
                }
            }

            return true;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }

    private static bool IsValidForwarderExportChain(
        MetadataReader reader,
        ChainWalkResult<ExportedTypeHandle> chain)
    {
        try
        {
            var outermost = chain.Rest is { Count: > 0 } parents ? parents[^1] : chain.First;
            var outer = reader.GetExportedType(outermost);
            var outerVisibility = outer.Attributes & TypeAttributes.VisibilityMask;
            if (outerVisibility is not TypeAttributes.NotPublic and not TypeAttributes.Public ||
                (outer.Attributes & ForwarderAttribute) == 0)
            {
                return false;
            }

            if (chain.Rest is not { Count: > 0 } nestedParents)
            {
                return true;
            }

            if (!IsValidNestedForwarder(reader.GetExportedType(chain.First)))
            {
                return false;
            }

            for (var index = 0; index < nestedParents.Count - 1; index++)
            {
                if (!IsValidNestedForwarder(reader.GetExportedType(nestedParents[index])))
                {
                    return false;
                }
            }

            return true;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }

    private static bool IsValidNestedModuleExport(ExportedType exportedType) =>
        (exportedType.Attributes & TypeAttributes.VisibilityMask) == TypeAttributes.NestedPublic &&
        (exportedType.Attributes & ForwarderAttribute) == 0 &&
        exportedType.Namespace.IsNil;

    private static bool IsValidNestedForwarder(ExportedType exportedType)
    {
        var visibility = exportedType.Attributes & TypeAttributes.VisibilityMask;
        // Runtime facades such as the active framework's mscorlib encode nested forwarders with
        // zero attributes (NotPublic), even though the outer row carries the Forwarder flag. Keep
        // that emitted runtime convention alongside the ECMA NestedPublic form; the surrounding
        // chain validation still requires a valid outer forwarder and AssemblyRef terminal.
        return (visibility is TypeAttributes.NotPublic or TypeAttributes.NestedPublic) &&
            (exportedType.Attributes & ForwarderAttribute) == 0 &&
            exportedType.Namespace.IsNil;
    }

    private static bool IsValidModuleTypeDefinitionChain(
        MetadataReader reader,
        TypeDefinitionHandle handle)
    {
        var chain = MetadataNestingWalker.DeclaringTypeChain(reader, handle);
        if (!chain.IsComplete)
        {
            return false;
        }

        try
        {
            var outermost = chain.Rest is { Count: > 0 } parents ? parents[^1] : chain.First;
            var outer = reader.GetTypeDefinition(outermost);
            if ((outer.Attributes & TypeAttributes.VisibilityMask) != TypeAttributes.Public)
            {
                return false;
            }

            if (chain.Rest is not { Count: > 0 } nestedParents)
            {
                return true;
            }

            if (!IsValidNestedTypeDefinition(reader.GetTypeDefinition(chain.First)))
            {
                return false;
            }

            for (var index = 0; index < nestedParents.Count - 1; index++)
            {
                if (!IsValidNestedTypeDefinition(reader.GetTypeDefinition(nestedParents[index])))
                {
                    return false;
                }
            }

            return true;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }

    private static bool IsValidNestedTypeDefinition(TypeDefinition definition) =>
        (definition.Attributes & TypeAttributes.VisibilityMask) == TypeAttributes.NestedPublic &&
        definition.Namespace.IsNil;

    private static ResolvedModule? ResolveSiblingModule(
        ResolvedAssembly resolved,
        MetadataReader manifestReader,
        AssemblyFileHandle fileHandle,
        string declaringType,
        string? targetFramework,
        string? preferredRuntimePack)
    {
        if (resolved is not ResolvedAssembly.FromFile(var manifestPath))
        {
            return null;
        }

        string moduleName;
        byte[] expectedHash;
        AssemblyHashAlgorithm hashAlgorithm;
        try
        {
            var file = manifestReader.GetAssemblyFile(fileHandle);
            if (!file.ContainsMetadata)
            {
                return null;
            }

            moduleName = manifestReader.GetString(file.Name);
            expectedHash = manifestReader.GetBlobBytes(file.HashValue);
            hashAlgorithm = manifestReader.GetAssemblyDefinition().HashAlgorithm;
        }
        catch (BadImageFormatException)
        {
            return null;
        }

        if (!IsSimpleModuleName(moduleName) || expectedHash.Length == 0)
        {
            return null;
        }

        string manifestFullPath;
        string modulePath;
        try
        {
            manifestFullPath = Path.GetFullPath(manifestPath);
            var manifestDirectory = Path.GetDirectoryName(manifestFullPath);
            if (manifestDirectory is null)
            {
                return null;
            }

            modulePath = Path.GetFullPath(Path.Combine(manifestDirectory, moduleName));
            var moduleDirectory = Path.GetDirectoryName(modulePath);
            var pathComparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!string.Equals(manifestDirectory, moduleDirectory, pathComparison))
            {
                return null;
            }
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
        {
            return null;
        }

        if (!NoFollowFileReader.TryReadAllBytes(modulePath, out var moduleBytes))
        {
            return null;
        }

        if (!TryComputeModuleHash(moduleBytes, hashAlgorithm, out var actualHash) ||
            !CryptographicOperations.FixedTimeEquals(expectedHash, actualHash))
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(moduleBytes, writable: false);
            using var peReader = new PEReader(stream);
            if (!peReader.HasMetadata)
            {
                return null;
            }

            var moduleReader = peReader.GetMetadataReader();
            if (moduleReader.IsAssembly)
            {
                return null;
            }

            var moduleDefinition = moduleReader.GetModuleDefinition();
            if (!string.Equals(
                moduleReader.GetString(moduleDefinition.Name),
                moduleName,
                StringComparison.Ordinal))
            {
                return null;
            }

            TypeDefinitionHandle matchingType = default;
            foreach (var typeHandle in moduleReader.TypeDefinitions)
            {
                var fullName = GetTypeDefFullName(moduleReader, typeHandle);
                if (fullName is null)
                {
                    continue;
                }

                if (fullName == declaringType)
                {
                    if (!matchingType.IsNil || !IsValidModuleTypeDefinitionChain(moduleReader, typeHandle))
                    {
                        return null;
                    }

                    matchingType = typeHandle;
                }
            }

            return matchingType.IsNil
                ? null
                : new ResolvedModule(
                    [.. moduleBytes],
                    modulePath,
                    manifestFullPath,
                    targetFramework,
                    preferredRuntimePack);
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    private static bool IsSimpleModuleName(string name)
    {
        if (name.Length == 0 ||
            name is "." or ".." ||
            name.EndsWith(' ') ||
            name.EndsWith('.') ||
            Path.IsPathRooted(name) ||
            !string.Equals(Path.GetFileName(name), name, StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var character in name)
        {
            if (char.IsControl(character) || character is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*')
            {
                return false;
            }
        }

        var firstDot = name.IndexOf('.');
        var deviceBaseName = firstDot < 0 ? name : name[..firstDot];
        if (deviceBaseName.EndsWith(' ') ||
            deviceBaseName.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            deviceBaseName.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            deviceBaseName.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            deviceBaseName.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
            deviceBaseName.Equals("CLOCK$", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !((deviceBaseName.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                  deviceBaseName.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
                 deviceBaseName.Length == 4 &&
                 IsWindowsDeviceDigit(deviceBaseName[3]));
    }

    private static bool IsWindowsDeviceDigit(char character) =>
        character is (>= '1' and <= '9') or '\u00B9' or '\u00B2' or '\u00B3';

    private static bool TryComputeModuleHash(
        byte[] moduleBytes,
        AssemblyHashAlgorithm algorithm,
        out byte[] hash)
    {
        hash = algorithm switch
        {
            AssemblyHashAlgorithm.None or AssemblyHashAlgorithm.Sha1 => SHA1.HashData(moduleBytes),
            AssemblyHashAlgorithm.Sha256 => SHA256.HashData(moduleBytes),
            AssemblyHashAlgorithm.Sha384 => SHA384.HashData(moduleBytes),
            AssemblyHashAlgorithm.Sha512 => SHA512.HashData(moduleBytes),
            AssemblyHashAlgorithm.MD5 => MD5.HashData(moduleBytes),
            _ => [],
        };
        return hash.Length > 0;
    }

    /// <summary>
    /// Builds the full name for an exported type, following nested type parents.
    /// Uses '/' as the nesting separator to match the IL metadata convention.
    /// </summary>
    private static string? GetExportedTypeFullName(MetadataReader reader, ExportedTypeHandle handle)
    {
        var chain = MetadataNestingWalker.ExportedTypeImplementationChain(reader, handle);
        return MetadataNestingWalker.TryFormatExportedTypeName(chain, out var fullName)
            ? fullName
            : null;
    }

    /// <summary>
    /// Follows the Implementation chain from an exported type up to the root AssemblyReference,
    /// returning the full identity (name, version, culture, PKT) for that reference.
    /// </summary>
    /// <param name="reader">Metadata reader for the assembly that owns the exported type.</param>
    /// <param name="handle">The exported type handle whose forwarder target is sought.</param>
    /// <param name="broken">
    /// Receives <see langword="true"/> when the matching exported type cannot identify a valid
    /// forwarding assembly.
    /// </param>
    /// <returns>The forwarded reference's full identity, or <see langword="null"/>.</returns>
    private static AssemblyRefInfo? GetForwardedAssemblyRef(
        MetadataReader reader,
        ExportedTypeHandle handle,
        out bool broken)
    {
        var chain = MetadataNestingWalker.ExportedTypeImplementationChain(reader, handle);
        if (!chain.IsComplete ||
            chain.Terminal.Kind != HandleKind.AssemblyReference ||
            !IsValidForwarderExportChain(reader, chain))
        {
            broken = true;
            return null;
        }

        string name;
        string version;
        string culture;
        BlobHandle publicKeyOrToken;
        AssemblyFlags flags;
        try
        {
            var asmRef = reader.GetAssemblyReference((AssemblyReferenceHandle)chain.Terminal);
            name = reader.GetString(asmRef.Name);
            version = asmRef.Version?.ToString() ?? string.Empty;
            culture = reader.GetString(asmRef.Culture);
            publicKeyOrToken = asmRef.PublicKeyOrToken;
            flags = asmRef.Flags;
        }
        catch (BadImageFormatException)
        {
            broken = true;
            return null;
        }

        if (name.Length == 0)
        {
            broken = true;
            return null;
        }

        if (string.IsNullOrEmpty(culture))
        {
            culture = "neutral";
        }
        string? pkt = null;
        if (!publicKeyOrToken.IsNil)
        {
            byte[] bytes;
            try
            {
                bytes = reader.GetBlobBytes(publicKeyOrToken);
            }
            catch (BadImageFormatException)
            {
                broken = true;
                return null;
            }

            if ((flags & AssemblyFlags.PublicKey) != 0)
            {
                if (bytes.Length == 0)
                {
                    broken = true;
                    return null;
                }

                var hash = SHA1.HashData(bytes);
                Span<byte> token = stackalloc byte[8];
                hash.AsSpan(hash.Length - token.Length, token.Length).CopyTo(token);
                token.Reverse();
                pkt = Convert.ToHexStringLower(token);
            }
            else if (bytes.Length == 8)
            {
                pkt = Convert.ToHexStringLower(bytes);
            }
            else
            {
                broken = true;
                return null;
            }
        }
        broken = false;
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
                case ResolvedModule module:
                    stream = new MemoryStream([.. module.Bytes], writable: false);
                    break;
                default:
                    return false;
            }

            using (stream)
            using (var pe = new PEReader(stream))
            {
                if (!pe.HasMetadata)
                {
                    return false;
                }

                var reader = pe.GetMetadataReader();

                // Fast path: reference assemblies carry ReferenceAssemblyAttribute
                // on the assembly definition and never have IL bodies.
                if (reader.IsAssembly)
                {
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
                }

                // Stub assemblies (e.g. mscorlib) have metadata and type forwarders
                // but no IL bodies. Check that at least one method has an RVA,
                // stopping at the first hit rather than enumerating every MethodDef.
                foreach (var methodHandle in reader.MethodDefinitions)
                {
                    if (reader.GetMethodDefinition(methodHandle).RelativeVirtualAddress > 0)
                    {
                        return true;
                    }
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
