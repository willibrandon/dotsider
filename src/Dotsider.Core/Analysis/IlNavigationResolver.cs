using Dotsider.Core.Analysis.Models;
using Dotsider.Core.Analysis.Signatures;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Resolves a metadata token from an IL instruction to an <see cref="IlNavigationTarget"/>
/// describing what the token points to and where it lives.
/// </summary>
public static class IlNavigationResolver
{
    /// <summary>
    /// Resolves the given metadata token against the analyzer's metadata tables.
    /// </summary>
    /// <param name="analyzer">The assembly analyzer containing the metadata.</param>
    /// <param name="token">The raw metadata token from an IL instruction operand.</param>
    /// <param name="contextMethod">
    /// The method whose IL body produced the token, when known. Needed to resolve
    /// bare generic-parameter TypeSpecs (<c>ELEMENT_TYPE_VAR</c>/<c>ELEMENT_TYPE_MVAR</c>),
    /// which do not encode their generic owner on their own.
    /// </param>
    /// <returns>The resolved navigation target.</returns>
    public static IlNavigationTarget Resolve(
        AssemblyAnalyzer analyzer, int token, MethodDefInfo? contextMethod = null)
    {
        var reader = analyzer.GetMetadataReader();
        if (reader is null)
            return new IlNavigationTarget.Unresolved(token, "Assembly has no metadata");

        EntityHandle handle;
        try { handle = MetadataTokens.EntityHandle(token); }
        catch (ArgumentException) { return new IlNavigationTarget.Unresolved(token, $"Invalid token 0x{token:X8}"); }

        return handle.Kind switch
        {
            HandleKind.MethodDefinition => ResolveMethodDef(analyzer, token),
            HandleKind.TypeDefinition => ResolveTypeDef(analyzer, token),
            HandleKind.FieldDefinition => ResolveFieldDef(analyzer, token),
            HandleKind.MemberReference => ResolveMemberRef(analyzer, reader, (MemberReferenceHandle)handle, token, contextMethod),
            HandleKind.TypeReference => ResolveTypeRef(analyzer, reader, (TypeReferenceHandle)handle, token),
            HandleKind.TypeSpecification => ResolveTypeSpec(analyzer, reader, (TypeSpecificationHandle)handle, token, contextMethod),
            HandleKind.MethodSpecification => ResolveMethodSpec(analyzer, reader, (MethodSpecificationHandle)handle, token, contextMethod),
            _ => new IlNavigationTarget.Unsupported(token, $"Unsupported handle kind: {handle.Kind}")
        };
    }

    private static IlNavigationTarget ResolveMethodDef(AssemblyAnalyzer analyzer, int token)
    {
        var method = analyzer.MethodDefs.FirstOrDefault(m => m.Token == token);
        return method is not null
            ? new IlNavigationTarget.LocalMethod(method)
            : new IlNavigationTarget.Unresolved(token, "MethodDef not found");
    }

    private static IlNavigationTarget ResolveTypeDef(AssemblyAnalyzer analyzer, int token)
    {
        var type = analyzer.TypeDefs.FirstOrDefault(t => t.Token == token);
        return type is not null
            ? new IlNavigationTarget.LocalType(type)
            : new IlNavigationTarget.Unresolved(token, "TypeDef not found");
    }

    private static IlNavigationTarget ResolveFieldDef(AssemblyAnalyzer analyzer, int token)
    {
        var field = analyzer.FieldDefs.FirstOrDefault(f => f.Token == token);
        if (field is null)
            return new IlNavigationTarget.Unresolved(token, "FieldDef not found");
        var declaringType = analyzer.TypeDefs.FirstOrDefault(t => t.FullName == field.DeclaringType);
        return declaringType is not null
            ? new IlNavigationTarget.LocalField(field, declaringType)
            : new IlNavigationTarget.Unresolved(token, "Declaring type not found for field");
    }

    private static IlNavigationTarget ResolveMemberRef(
        AssemblyAnalyzer analyzer, MetadataReader reader, MemberReferenceHandle handle, int token,
        MethodDefInfo? contextMethod)
    {
        MemberReference mr;
        string name;
        try
        {
            mr = reader.GetMemberReference(handle);
            name = reader.GetString(mr.Name);
        }
        catch (BadImageFormatException)
        {
            return new IlNavigationTarget.Unresolved(token, "Cannot read MemberRef");
        }

        MemberRefKind kind;
        string signature;
        try
        {
            var sigReader = reader.GetBlobReader(mr.Signature);
            var header = sigReader.ReadSignatureHeader();
            if (header.Kind == SignatureKind.Field)
            {
                kind = MemberRefKind.Field;
                _ = SafeSignatureDecoder.DecodeMemberReferenceFieldSignature(
                    reader,
                    handle,
                    new AssemblySignatureTypeProvider(failOnInvalidMetadata: true),
                    genericContext: default);
                signature = "";
            }
            else
            {
                kind = MemberRefKind.Method;
                var sig = SafeSignatureDecoder.DecodeMemberReferenceMethodSignature(
                    reader,
                    handle,
                    new AssemblySignatureTypeProvider(failOnInvalidMetadata: true),
                    genericContext: default);
                signature = $"{sig.ReturnType}({string.Join(", ", sig.ParameterTypes)})";
            }
        }
        catch (BadImageFormatException)
        {
            return new IlNavigationTarget.Unresolved(token, "MemberRef has a malformed signature");
        }

        return mr.Parent.Kind switch
        {
            HandleKind.TypeDefinition => ResolveMemberRefLocalParent(
                analyzer, (TypeDefinitionHandle)mr.Parent, name, signature, kind, token),
            HandleKind.TypeReference => ResolveMemberRefExternalParent(
                reader, (TypeReferenceHandle)mr.Parent, name, signature, kind),
            HandleKind.TypeSpecification => ResolveMemberRefWithTypeSpecParent(
                analyzer, reader, (TypeSpecificationHandle)mr.Parent, name, signature, kind, token, contextMethod),
            _ => new IlNavigationTarget.Unsupported(token, $"MemberRef parent kind: {mr.Parent.Kind}")
        };
    }

    private static IlNavigationTarget ResolveMemberRefLocalParent(
        AssemblyAnalyzer analyzer, TypeDefinitionHandle parentHandle,
        string name, string signature, MemberRefKind kind, int token)
    {
        var parentToken = MetadataTokens.GetToken(parentHandle);
        var declaringType = analyzer.TypeDefs.FirstOrDefault(t => t.Token == parentToken);

        if (kind == MemberRefKind.Field)
        {
            var field = analyzer.FieldDefs.FirstOrDefault(f =>
                f.DeclaringType == declaringType?.FullName && f.Name == name);
            if (field is not null && declaringType is not null)
                return new IlNavigationTarget.LocalField(field, declaringType);
            return new IlNavigationTarget.Unresolved(token, $"Local field {name} not found");
        }

        var candidates = analyzer.MethodDefs
            .Where(m => m.DeclaringType == declaringType?.FullName && m.Name == name).ToList();
        if (candidates.Count == 1) return new IlNavigationTarget.LocalMethod(candidates[0]);
        if (candidates.Count > 1 && !string.IsNullOrEmpty(signature))
        {
            var exact = candidates.FirstOrDefault(m => m.Signature == signature);
            if (exact is not null) return new IlNavigationTarget.LocalMethod(exact);
        }
        if (candidates.Count > 0) return new IlNavigationTarget.LocalMethod(candidates[0]);
        return new IlNavigationTarget.Unresolved(token, $"Local method {name} not found");
    }

    private static IlNavigationTarget ResolveMemberRefExternalParent(
        MetadataReader reader, TypeReferenceHandle parentHandle,
        string name, string signature, MemberRefKind kind)
    {
        var assemblyName = GetAssemblyNameFromTypeRef(reader, parentHandle);
        var declaringType = GetFullTypeRefName(reader, parentHandle);
        if (kind == MemberRefKind.Field)
            return new IlNavigationTarget.ExternalField(name, declaringType, assemblyName);
        return new IlNavigationTarget.ExternalMethod(name, declaringType, signature, assemblyName);
    }

    private static IlNavigationTarget ResolveTypeRef(
        AssemblyAnalyzer analyzer, MetadataReader reader, TypeReferenceHandle handle, int token)
    {
        var typeRef = analyzer.TypeRefs.FirstOrDefault(t => t.Token == token);
        if (typeRef is null) return new IlNavigationTarget.Unresolved(token, "TypeRef not found");
        var assemblyName = GetAssemblyNameFromTypeRef(reader, handle);
        return new IlNavigationTarget.ExternalType(typeRef, assemblyName);
    }

    private static IlNavigationTarget ResolveTypeSpec(
        AssemblyAnalyzer analyzer, MetadataReader reader, TypeSpecificationHandle handle, int token,
        MethodDefInfo? contextMethod)
    {
        // A bare ELEMENT_TYPE_VAR / ELEMENT_TYPE_MVAR TypeSpec names a generic
        // parameter but does not encode its generic owner. Route through the
        // enclosing method's context before trying to match TypeDefs/TypeRefs,
        // since the decoded string ("!N"/"!!N") will not match anything.
        string decoded;
        (GenericParamKind Kind, int Index)? genericParam;
        try
        {
            decoded = SafeSignatureDecoder.DecodeType(
                reader, handle, new AssemblySignatureTypeProvider(failOnInvalidMetadata: true),
                genericContext: default);
            genericParam = TryReadGenericParameter(reader, handle);
        }
        catch (BadImageFormatException)
        {
            return new IlNavigationTarget.Unresolved(token, "TypeSpec has a malformed signature");
        }

        if (genericParam is { } gp)
        {
            return ResolveGenericParameter(analyzer, gp.Kind, gp.Index, token, contextMethod);
        }

        // Decode the TypeSpec to a string, then find the underlying open generic type
        // by matching the name prefix (before the generic arguments) against TypeDefs/TypeRefs.
        var openName = StripGenericArgs(decoded);

        // Check local TypeDefs first
        var localType = analyzer.TypeDefs.FirstOrDefault(t =>
            t.FullName == openName || t.Name == openName);
        if (localType is not null)
            return new IlNavigationTarget.LocalType(localType);

        // Check TypeRefs for external types
        var typeRef = analyzer.TypeRefs.FirstOrDefault(t =>
            t.FullName == openName || t.Name == openName);
        if (typeRef is not null)
        {
            var assemblyName = GetAssemblyNameFromTypeRef(reader,
                MetadataTokens.TypeReferenceHandle(MetadataTokens.GetRowNumber(
                    MetadataTokens.EntityHandle(typeRef.Token))));
            return new IlNavigationTarget.ExternalType(typeRef, assemblyName);
        }

        return new IlNavigationTarget.Unsupported(token, $"Cannot resolve TypeSpec: {decoded}");
    }

    /// <summary>
    /// Maps a bare generic-parameter TypeSpec to the closest navigable definition —
    /// for <c>ELEMENT_TYPE_VAR</c> the enclosing type, for <c>ELEMENT_TYPE_MVAR</c>
    /// the enclosing method.
    /// </summary>
    private static IlNavigationTarget ResolveGenericParameter(
        AssemblyAnalyzer analyzer, GenericParamKind kind, int index, int token,
        MethodDefInfo? contextMethod)
    {
        var label = kind == GenericParamKind.TypeParameter ? $"!{index}" : $"!!{index}";
        if (contextMethod is null)
        {
            return new IlNavigationTarget.Unsupported(token,
                $"Generic parameter {label} requires method context for navigation");
        }

        if (kind == GenericParamKind.MethodParameter)
        {
            // A method-level generic parameter's only definition site is the method
            // signature the user is already reading. Routing to LocalMethod(self)
            // would hit the "already selected" short-circuit in NavigateToIlDefinition
            // and silently no-op, so surface an explicit transient notice instead.
            return new IlNavigationTarget.Unsupported(token,
                $"Generic method parameter {label} of {contextMethod.Name} — defined by this method's signature");
        }

        // ELEMENT_TYPE_VAR: navigate to the enclosing type. It is the one whose
        // GenericParam rows define this index, so it's the only place the parameter
        // exists as a definition in metadata.
        var declaringTypeName = contextMethod.DeclaringType;
        var localType = analyzer.TypeDefs.FirstOrDefault(t => t.FullName == declaringTypeName);
        if (localType is not null)
            return new IlNavigationTarget.LocalType(localType);

        return new IlNavigationTarget.Unresolved(token,
            $"Generic parameter {label} declaring type not found: {declaringTypeName}");
    }

    /// <summary>
    /// Attempts to identify a validated TypeSpec whose root is a generic type or method parameter,
    /// skipping any leading custom modifiers.
    /// </summary>
    private static (GenericParamKind Kind, int Index)? TryReadGenericParameter(
        MetadataReader reader,
        TypeSpecificationHandle handle)
    {
        var specification = reader.GetTypeSpecification(handle);
        var blob = reader.GetBlobReader(specification.Signature);
        SignatureTypeCode code;
        do
        {
            code = blob.ReadSignatureTypeCode();
            if (code is SignatureTypeCode.OptionalModifier or SignatureTypeCode.RequiredModifier)
            {
                _ = blob.ReadTypeHandle();
            }
        }
        while (code is SignatureTypeCode.OptionalModifier or SignatureTypeCode.RequiredModifier);

        var result = code switch
        {
            SignatureTypeCode.GenericTypeParameter =>
                (GenericParamKind.TypeParameter, blob.ReadCompressedInteger()),
            SignatureTypeCode.GenericMethodParameter =>
                (GenericParamKind.MethodParameter, blob.ReadCompressedInteger()),
            _ => ((GenericParamKind Kind, int Index)?)null,
        };

        return result is not null && blob.RemainingBytes == 0 ? result : null;
    }

    private static IlNavigationTarget ResolveMemberRefWithTypeSpecParent(
        AssemblyAnalyzer analyzer, MetadataReader reader, TypeSpecificationHandle typeSpecHandle,
        string name, string signature, MemberRefKind kind, int token,
        MethodDefInfo? contextMethod)
    {
        // MemberRef parent is a TypeSpec naming a generic parameter on its own —
        // route through the context's declaring type so we don't try to match "!N"
        // against TypeDefs/TypeRefs below.
        string decoded;
        (GenericParamKind Kind, int Index)? genericParam;
        try
        {
            decoded = SafeSignatureDecoder.DecodeType(
                reader, typeSpecHandle, new AssemblySignatureTypeProvider(failOnInvalidMetadata: true),
                genericContext: default);
            genericParam = TryReadGenericParameter(reader, typeSpecHandle);
        }
        catch (BadImageFormatException)
        {
            return new IlNavigationTarget.Unresolved(token, "TypeSpec parent has a malformed signature");
        }

        if (genericParam is { } gp && contextMethod is not null
            && gp.Kind == GenericParamKind.TypeParameter)
        {
            var declType = analyzer.TypeDefs.FirstOrDefault(
                t => t.FullName == contextMethod.DeclaringType);
            if (declType is not null)
            {
                var localHandle = MetadataTokens.TypeDefinitionHandle(
                    MetadataTokens.GetRowNumber(MetadataTokens.EntityHandle(declType.Token)));
                return ResolveMemberRefLocalParent(analyzer, localHandle, name, signature, kind, token);
            }
        }
        var openName = StripGenericArgs(decoded);

        // Check local TypeDefs
        var localType = analyzer.TypeDefs.FirstOrDefault(t =>
            t.FullName == openName || t.Name == openName);
        if (localType is not null)
        {
            var localHandle = MetadataTokens.TypeDefinitionHandle(
                MetadataTokens.GetRowNumber(MetadataTokens.EntityHandle(localType.Token)));
            return ResolveMemberRefLocalParent(analyzer, localHandle, name, signature, kind, token);
        }

        // Check TypeRefs for external types
        var typeRef = analyzer.TypeRefs.FirstOrDefault(t =>
            t.FullName == openName || t.Name == openName);
        if (typeRef is not null)
        {
            var refHandle = MetadataTokens.TypeReferenceHandle(
                MetadataTokens.GetRowNumber(MetadataTokens.EntityHandle(typeRef.Token)));
            return ResolveMemberRefExternalParent(reader, refHandle, name, signature, kind);
        }

        return new IlNavigationTarget.Unsupported(token, $"Cannot resolve {openName}::{name}");
    }

    /// <summary>
    /// Strips generic arguments from a type name to get the open generic name.
    /// e.g., "System.Collections.Generic.List&lt;byte[]&gt;" → "System.Collections.Generic.List`1"
    /// </summary>
    private static string StripGenericArgs(string typeName)
    {
        var angleBracket = typeName.IndexOf('<');
        if (angleBracket < 0) return typeName;
        var baseName = typeName[..angleBracket];
        // If the base name already has a backtick+arity suffix from metadata
        // (e.g., "Dictionary`2"), return it as-is to avoid a double suffix.
        var lastBacktick = baseName.LastIndexOf('`');
        if (lastBacktick >= 0 && lastBacktick < baseName.Length - 1
            && int.TryParse(baseName[(lastBacktick + 1)..], out _))
            return baseName;
        // Otherwise, count generic args to reconstruct the arity suffix.
        var depth = 0;
        var count = 1;
        for (var i = angleBracket; i < typeName.Length; i++)
        {
            if (typeName[i] == '<') depth++;
            else if (typeName[i] == '>') depth--;
            else if (typeName[i] == ',' && depth == 1) count++;
        }
        return $"{baseName}`{count}";
    }

    private static IlNavigationTarget ResolveMethodSpec(
        AssemblyAnalyzer analyzer, MetadataReader reader, MethodSpecificationHandle handle, int token,
        MethodDefInfo? contextMethod)
    {
        int methodToken;
        try
        {
            var ms = reader.GetMethodSpecification(handle);
            _ = SafeSignatureDecoder.DecodeMethodSpecificationSignature(
                reader,
                handle,
                new AssemblySignatureTypeProvider(failOnInvalidMetadata: true),
                genericContext: default);
            methodToken = MetadataTokens.GetToken(ms.Method);
        }
        catch (Exception ex) when (ex is BadImageFormatException or InvalidOperationException or ArgumentException)
        {
            return new IlNavigationTarget.GenericInstantiation(token, ex.Message);
        }

        return Resolve(analyzer, methodToken, contextMethod);
    }

    private static string GetAssemblyNameFromTypeRef(MetadataReader reader, TypeReferenceHandle handle)
    {
        var chain = MetadataNestingWalker.ResolutionScopeChain(reader, handle);
        if (!chain.IsComplete || chain.Terminal.Kind != HandleKind.AssemblyReference)
        {
            return "Unknown";
        }

        var assemblyReferenceHandle = (AssemblyReferenceHandle)chain.Terminal;
        try
        {
            return reader.GetString(reader.GetAssemblyReference(assemblyReferenceHandle).Name);
        }
        catch (BadImageFormatException)
        {
            return "Unknown";
        }
    }

    private static string GetFullTypeRefName(MetadataReader reader, TypeReferenceHandle handle)
    {
        var chain = MetadataNestingWalker.ResolutionScopeChain(reader, handle);
        return MetadataNestingWalker.TryFormatTypeReferenceName(
            chain, out var fullName, out _)
            ? fullName
            : "Unknown";
    }
}
