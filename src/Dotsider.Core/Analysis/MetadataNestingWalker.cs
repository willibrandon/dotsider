using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Performs bounded, cycle-aware walks over metadata nesting relationships.
/// </summary>
internal static class MetadataNestingWalker
{
    /// <summary>The maximum number of nesting edges accepted by a metadata chain.</summary>
    internal const int MaxDepth = 128;

    private const int InlineCycleThreshold = 8;

    /// <summary>
    /// Walks a <see cref="TypeDefinitionHandle"/> through its declaring-type chain.
    /// </summary>
    /// <param name="reader">The metadata reader that owns the handle.</param>
    /// <param name="handle">The innermost type definition.</param>
    /// <returns>The bounded chain-walk result.</returns>
    internal static ChainWalkResult<TypeDefinitionHandle> DeclaringTypeChain(
        MetadataReader reader,
        TypeDefinitionHandle handle)
    {
        var first = handle;
        var firstNamespace = string.Empty;
        var firstName = string.Empty;
        List<TypeDefinitionHandle>? rest = null;
        List<string>? restNames = null;
        var outermostNamespace = string.Empty;
        HashSet<TypeDefinitionHandle>? visited = null;
        var current = handle;
        var depth = 0;

        ChainWalkResult<TypeDefinitionHandle> Finish(
            EntityHandle terminal,
            ChainTermination termination) =>
            new(
                first,
                firstNamespace,
                firstName,
                rest,
                restNames,
                outermostNamespace,
                terminal,
                termination);

        while (true)
        {
            if (!IsValidRow(current, reader.TypeDefinitions.Count))
            {
                return Finish(default, ChainTermination.InvalidMetadata);
            }

            TypeDefinition definition;
            try
            {
                definition = reader.GetTypeDefinition(current);
            }
            catch (BadImageFormatException)
            {
                return Finish(default, ChainTermination.InvalidMetadata);
            }

            if (!TryReadName(
                    reader,
                    definition.Namespace,
                    definition.Name,
                    out var namespaceName,
                    out var name))
            {
                return Finish(default, ChainTermination.InvalidMetadata);
            }

            RetainName(
                depth,
                namespaceName,
                name,
                ref firstNamespace,
                ref firstName,
                ref restNames,
                ref outermostNamespace);

            if (definition.IsNested && !definition.Namespace.IsNil)
            {
                return Finish(default, ChainTermination.InvalidMetadata);
            }

            TypeDefinitionHandle parent;
            try
            {
                parent = definition.GetDeclaringType();
            }
            catch (BadImageFormatException)
            {
                return Finish(default, ChainTermination.InvalidMetadata);
            }

            if (!definition.IsNested)
            {
                return Finish(
                    parent,
                    parent.IsNil ? ChainTermination.Complete : ChainTermination.InvalidMetadata);
            }

            if (parent.IsNil || !IsValidRow(parent, reader.TypeDefinitions.Count))
            {
                return Finish(parent, ChainTermination.InvalidMetadata);
            }

            if (depth == MaxDepth)
            {
                return Finish(parent, ChainTermination.DepthExceeded);
            }

            if (IsCycle(first, rest, parent, ref visited))
            {
                return Finish(parent, ChainTermination.Cycle);
            }

            (rest ??= []).Add(parent);
            current = parent;
            depth++;
        }
    }

    /// <summary>
    /// Walks a <see cref="TypeReferenceHandle"/> through its resolution-scope chain.
    /// </summary>
    /// <param name="reader">The metadata reader that owns the handle.</param>
    /// <param name="handle">The innermost type reference.</param>
    /// <returns>The bounded chain-walk result.</returns>
    internal static ChainWalkResult<TypeReferenceHandle> ResolutionScopeChain(
        MetadataReader reader,
        TypeReferenceHandle handle)
    {
        var first = handle;
        var firstNamespace = string.Empty;
        var firstName = string.Empty;
        List<TypeReferenceHandle>? rest = null;
        List<string>? restNames = null;
        var outermostNamespace = string.Empty;
        HashSet<TypeReferenceHandle>? visited = null;
        var current = handle;
        var depth = 0;

        ChainWalkResult<TypeReferenceHandle> Finish(
            EntityHandle terminal,
            ChainTermination termination) =>
            new(
                first,
                firstNamespace,
                firstName,
                rest,
                restNames,
                outermostNamespace,
                terminal,
                termination);

        while (true)
        {
            if (!IsValidRow(current, reader.TypeReferences.Count))
            {
                return Finish(default, ChainTermination.InvalidMetadata);
            }

            TypeReference reference;
            EntityHandle scope;
            try
            {
                reference = reader.GetTypeReference(current);
                scope = reference.ResolutionScope;
            }
            catch (BadImageFormatException)
            {
                return Finish(default, ChainTermination.InvalidMetadata);
            }

            if (!TryReadName(
                    reader,
                    reference.Namespace,
                    reference.Name,
                    out var namespaceName,
                    out var name))
            {
                return Finish(default, ChainTermination.InvalidMetadata);
            }

            RetainName(
                depth,
                namespaceName,
                name,
                ref firstNamespace,
                ref firstName,
                ref restNames,
                ref outermostNamespace);

            if (scope.Kind == HandleKind.TypeReference && !reference.Namespace.IsNil)
            {
                return Finish(default, ChainTermination.InvalidMetadata);
            }

            if (scope.IsNil)
            {
                return Finish(scope, ChainTermination.Complete);
            }

            if (scope.Kind == HandleKind.TypeReference)
            {
                var parent = (TypeReferenceHandle)scope;
                if (!IsValidRow(parent, reader.TypeReferences.Count))
                {
                    return Finish(scope, ChainTermination.InvalidMetadata);
                }

                if (depth == MaxDepth)
                {
                    return Finish(scope, ChainTermination.DepthExceeded);
                }

                if (IsCycle(first, rest, parent, ref visited))
                {
                    return Finish(scope, ChainTermination.Cycle);
                }

                (rest ??= []).Add(parent);
                current = parent;
                depth++;
                continue;
            }

            if (!IsLegalResolutionScopeTerminal(reader, scope))
            {
                return Finish(scope, ChainTermination.InvalidMetadata);
            }

            return Finish(scope, ChainTermination.Complete);
        }
    }

    /// <summary>
    /// Walks an <see cref="ExportedTypeHandle"/> through its implementation chain.
    /// </summary>
    /// <param name="reader">The metadata reader that owns the handle.</param>
    /// <param name="handle">The innermost exported type.</param>
    /// <returns>The bounded chain-walk result.</returns>
    internal static ChainWalkResult<ExportedTypeHandle> ExportedTypeImplementationChain(
        MetadataReader reader,
        ExportedTypeHandle handle)
    {
        var first = handle;
        var firstNamespace = string.Empty;
        var firstName = string.Empty;
        List<ExportedTypeHandle>? rest = null;
        List<string>? restNames = null;
        var outermostNamespace = string.Empty;
        HashSet<ExportedTypeHandle>? visited = null;
        var current = handle;
        var depth = 0;

        ChainWalkResult<ExportedTypeHandle> Finish(
            EntityHandle terminal,
            ChainTermination termination) =>
            new(
                first,
                firstNamespace,
                firstName,
                rest,
                restNames,
                outermostNamespace,
                terminal,
                termination);

        while (true)
        {
            if (!IsValidRow(current, reader.ExportedTypes.Count))
            {
                return Finish(default, ChainTermination.InvalidMetadata);
            }

            ExportedType exportedType;
            EntityHandle implementation;
            try
            {
                exportedType = reader.GetExportedType(current);
                implementation = exportedType.Implementation;
            }
            catch (BadImageFormatException)
            {
                return Finish(default, ChainTermination.InvalidMetadata);
            }

            if (!TryReadName(
                    reader,
                    exportedType.Namespace,
                    exportedType.Name,
                    out var namespaceName,
                    out var name))
            {
                return Finish(default, ChainTermination.InvalidMetadata);
            }

            RetainName(
                depth,
                namespaceName,
                name,
                ref firstNamespace,
                ref firstName,
                ref restNames,
                ref outermostNamespace);

            if (implementation.Kind == HandleKind.ExportedType && !exportedType.Namespace.IsNil)
            {
                return Finish(default, ChainTermination.InvalidMetadata);
            }

            if (implementation.IsNil)
            {
                return Finish(implementation, ChainTermination.Complete);
            }

            if (implementation.Kind == HandleKind.ExportedType)
            {
                var parent = (ExportedTypeHandle)implementation;
                if (!IsValidRow(parent, reader.ExportedTypes.Count))
                {
                    return Finish(implementation, ChainTermination.InvalidMetadata);
                }

                if (depth == MaxDepth)
                {
                    return Finish(implementation, ChainTermination.DepthExceeded);
                }

                if (IsCycle(first, rest, parent, ref visited))
                {
                    return Finish(implementation, ChainTermination.Cycle);
                }

                (rest ??= []).Add(parent);
                current = parent;
                depth++;
                continue;
            }

            if (!IsLegalExportedTypeTerminal(reader, implementation))
            {
                return Finish(implementation, ChainTermination.InvalidMetadata);
            }

            return Finish(implementation, ChainTermination.Complete);
        }
    }

    /// <summary>
    /// Formats a completed declaring-type chain using the metadata nesting-name convention.
    /// </summary>
    /// <param name="result">The chain to format.</param>
    /// <param name="fullName">Receives the formatted name when successful.</param>
    /// <returns><see langword="true"/> when the complete chain and all names are valid.</returns>
    internal static bool TryFormatTypeDefinitionName(
        ChainWalkResult<TypeDefinitionHandle> result,
        out string fullName) =>
        TryFormatTypeDefinitionName(result, out fullName, out _);

    /// <summary>
    /// Formats a completed declaring-type chain and returns the outermost type namespace.
    /// </summary>
    /// <param name="result">The chain to format.</param>
    /// <param name="fullName">Receives the formatted name when successful.</param>
    /// <param name="namespaceName">Receives the outermost type's namespace.</param>
    /// <returns><see langword="true"/> when the complete chain and all names are valid.</returns>
    internal static bool TryFormatTypeDefinitionName(
        ChainWalkResult<TypeDefinitionHandle> result,
        out string fullName,
        out string namespaceName) =>
        TryFormatName(result, includeFirst: true, out fullName, out namespaceName);

    /// <summary>
    /// Formats a completed resolution-scope chain using the metadata nesting-name convention.
    /// </summary>
    /// <param name="result">The chain to format.</param>
    /// <param name="fullName">Receives the formatted name when successful.</param>
    /// <param name="namespaceName">Receives the outermost type's namespace.</param>
    /// <returns><see langword="true"/> when the complete chain and all names are valid.</returns>
    internal static bool TryFormatTypeReferenceName(
        ChainWalkResult<TypeReferenceHandle> result,
        out string fullName,
        out string namespaceName) =>
        TryFormatName(result, includeFirst: true, out fullName, out namespaceName);

    /// <summary>
    /// Formats the parent TypeRef portion of a completed resolution-scope chain.
    /// </summary>
    /// <param name="result">The chain whose first row has a TypeRef resolution scope.</param>
    /// <param name="fullName">Receives the formatted parent name when successful.</param>
    /// <returns><see langword="true"/> when the complete chain has a TypeRef parent.</returns>
    internal static bool TryFormatTypeReferenceParentName(
        ChainWalkResult<TypeReferenceHandle> result,
        out string fullName) =>
        TryFormatName(result, includeFirst: false, out fullName, out _);

    /// <summary>
    /// Formats a completed exported-type implementation chain using the metadata nesting-name convention.
    /// </summary>
    /// <param name="result">The chain to format.</param>
    /// <param name="fullName">Receives the formatted name when successful.</param>
    /// <returns><see langword="true"/> when the complete chain and all names are valid.</returns>
    internal static bool TryFormatExportedTypeName(
        ChainWalkResult<ExportedTypeHandle> result,
        out string fullName) =>
        TryFormatName(result, includeFirst: true, out fullName, out _);

    /// <summary>Formats a metadata handle as a deterministic hexadecimal token.</summary>
    /// <param name="handle">The metadata handle to format.</param>
    /// <returns>The eight-digit metadata token.</returns>
    internal static string FormatToken(EntityHandle handle) =>
        $"0x{MetadataTokens.GetToken(handle):X8}";

    private static void Append(Span<char> destination, ref int offset, string value)
    {
        value.AsSpan().CopyTo(destination[offset..]);
        offset += value.Length;
    }

    private static bool IsCycle<THandle>(
        THandle first,
        List<THandle>? rest,
        THandle next,
        ref HashSet<THandle>? visited)
        where THandle : struct
    {
        var comparer = EqualityComparer<THandle>.Default;
        var count = 1 + (rest?.Count ?? 0);
        if (visited is null && count < InlineCycleThreshold)
        {
            if (comparer.Equals(first, next))
            {
                return true;
            }

            if (rest is not null)
            {
                for (var i = 0; i < rest.Count; i++)
                {
                    if (comparer.Equals(rest[i], next))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        if (visited is null)
        {
            visited = new HashSet<THandle>(comparer) { first };
            if (rest is not null)
            {
                for (var i = 0; i < rest.Count; i++)
                {
                    visited.Add(rest[i]);
                }
            }
        }

        return !visited.Add(next);
    }

    private static bool IsLegalExportedTypeTerminal(MetadataReader reader, EntityHandle terminal) =>
        terminal.Kind switch
        {
            HandleKind.AssemblyReference =>
                IsValidRow((AssemblyReferenceHandle)terminal, reader.AssemblyReferences.Count),
            HandleKind.AssemblyFile =>
                IsValidRow((AssemblyFileHandle)terminal, reader.GetTableRowCount(TableIndex.File)),
            _ => false,
        };

    private static bool IsLegalResolutionScopeTerminal(MetadataReader reader, EntityHandle terminal) =>
        terminal.Kind switch
        {
            HandleKind.AssemblyReference =>
                IsValidRow((AssemblyReferenceHandle)terminal, reader.AssemblyReferences.Count),
            HandleKind.ModuleReference =>
                IsValidRow((ModuleReferenceHandle)terminal, reader.GetTableRowCount(TableIndex.ModuleRef)),
            HandleKind.ModuleDefinition =>
                IsValidRow(terminal, reader.GetTableRowCount(TableIndex.Module)),
            _ => false,
        };

    private static bool IsValidRow(TypeDefinitionHandle handle, int rowCount) =>
        IsValidRow((EntityHandle)handle, rowCount);

    private static bool IsValidRow(TypeReferenceHandle handle, int rowCount) =>
        IsValidRow((EntityHandle)handle, rowCount);

    private static bool IsValidRow(ExportedTypeHandle handle, int rowCount) =>
        IsValidRow((EntityHandle)handle, rowCount);

    private static bool IsValidRow(AssemblyReferenceHandle handle, int rowCount) =>
        IsValidRow((EntityHandle)handle, rowCount);

    private static bool IsValidRow(ModuleReferenceHandle handle, int rowCount) =>
        IsValidRow((EntityHandle)handle, rowCount);

    private static bool IsValidRow(AssemblyFileHandle handle, int rowCount) =>
        IsValidRow((EntityHandle)handle, rowCount);

    private static bool IsValidRow(EntityHandle handle, int rowCount)
    {
        var row = MetadataTokens.GetRowNumber(handle);
        return row > 0 && row <= rowCount;
    }

    private static void RetainName(
        int depth,
        string namespaceName,
        string name,
        ref string firstNamespace,
        ref string firstName,
        ref List<string>? restNames,
        ref string outermostNamespace)
    {
        if (depth == 0)
        {
            firstNamespace = namespaceName;
            firstName = name;
        }
        else
        {
            (restNames ??= []).Add(name);
        }

        outermostNamespace = namespaceName;
    }

    private static bool TryFormatName<THandle>(
        ChainWalkResult<THandle> result,
        bool includeFirst,
        out string fullName,
        out string namespaceName)
        where THandle : struct
    {
        fullName = string.Empty;
        namespaceName = string.Empty;
        if (!result.IsComplete || string.IsNullOrEmpty(result.FirstName))
        {
            return false;
        }

        var restCount = result.Rest?.Count ?? 0;
        if (restCount != (result.RestNames?.Count ?? 0) || (!includeFirst && restCount == 0))
        {
            return false;
        }

        namespaceName = result.OutermostNamespace;
        var nameCount = restCount + (includeFirst ? 1 : 0);
        if (nameCount == 1)
        {
            var name = includeFirst ? result.FirstName : result.RestNames![0];
            fullName = namespaceName.Length == 0
                ? name
                : string.Concat(namespaceName, ".", name);
            return true;
        }

        var length = namespaceName.Length + (namespaceName.Length == 0 ? 0 : 1);
        if (result.RestNames is { } restNames)
        {
            for (var i = 0; i < restNames.Count; i++)
            {
                length += restNames[i].Length;
            }
        }

        if (includeFirst)
        {
            length += result.FirstName.Length;
        }

        length += nameCount - 1;
        fullName = string.Create(
            length,
            (Result: result, IncludeFirst: includeFirst),
            static (destination, state) =>
            {
                var offset = 0;
                if (state.Result.OutermostNamespace.Length > 0)
                {
                    Append(destination, ref offset, state.Result.OutermostNamespace);
                    destination[offset++] = '.';
                }

                var names = state.Result.RestNames!;
                var wroteName = false;
                for (var i = names.Count - 1; i >= 0; i--)
                {
                    if (wroteName)
                    {
                        destination[offset++] = '/';
                    }

                    Append(destination, ref offset, names[i]);
                    wroteName = true;
                }

                if (state.IncludeFirst)
                {
                    if (wroteName)
                    {
                        destination[offset++] = '/';
                    }

                    Append(destination, ref offset, state.Result.FirstName);
                }
            });
        return true;
    }

    private static bool TryReadName(
        MetadataReader reader,
        StringHandle namespaceHandle,
        StringHandle nameHandle,
        out string namespaceName,
        out string name)
    {
        try
        {
            namespaceName = namespaceHandle.IsNil
                ? string.Empty
                : reader.GetString(namespaceHandle);
            name = nameHandle.IsNil
                ? string.Empty
                : reader.GetString(nameHandle);
            return name.Length > 0 && (namespaceHandle.IsNil || namespaceName.Length > 0);
        }
        catch (BadImageFormatException)
        {
            namespaceName = string.Empty;
            name = string.Empty;
            return false;
        }
    }
}
