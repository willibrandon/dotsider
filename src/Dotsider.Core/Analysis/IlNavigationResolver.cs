using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Dotsider.Core.Analysis.Models;

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
    /// <returns>The resolved navigation target.</returns>
    public static IlNavigationTarget Resolve(AssemblyAnalyzer analyzer, int token)
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
            HandleKind.MemberReference => ResolveMemberRef(analyzer, reader, (MemberReferenceHandle)handle, token),
            HandleKind.TypeReference => ResolveTypeRef(analyzer, reader, (TypeReferenceHandle)handle, token),
            HandleKind.TypeSpecification => ResolveTypeSpec(analyzer, reader, (TypeSpecificationHandle)handle, token),
            HandleKind.MethodSpecification => ResolveMethodSpec(analyzer, reader, (MethodSpecificationHandle)handle, token),
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
        AssemblyAnalyzer analyzer, MetadataReader reader, MemberReferenceHandle handle, int token)
    {
        MemberReference mr;
        try { mr = reader.GetMemberReference(handle); }
        catch { return new IlNavigationTarget.Unresolved(token, "Cannot read MemberRef"); }

        var name = reader.GetString(mr.Name);
        MemberRefKind kind;
        string signature;
        try
        {
            var sigReader = reader.GetBlobReader(mr.Signature);
            var header = sigReader.ReadSignatureHeader();
            if (header.Kind == SignatureKind.Field)
            {
                kind = MemberRefKind.Field;
                signature = "";
            }
            else
            {
                kind = MemberRefKind.Method;
                var sigProvider = new AssemblyAnalyzer.SignatureTypeProvider();
                var sig = mr.DecodeMethodSignature(sigProvider, genericContext: default);
                signature = $"{sig.ReturnType}({string.Join(", ", sig.ParameterTypes)})";
            }
        }
        catch { kind = MemberRefKind.Method; signature = ""; }

        return mr.Parent.Kind switch
        {
            HandleKind.TypeDefinition => ResolveMemberRefLocalParent(
                analyzer, (TypeDefinitionHandle)mr.Parent, name, signature, kind, token),
            HandleKind.TypeReference => ResolveMemberRefExternalParent(
                reader, (TypeReferenceHandle)mr.Parent, name, signature, kind),
            HandleKind.TypeSpecification => ResolveMemberRefWithTypeSpecParent(
                analyzer, reader, (TypeSpecificationHandle)mr.Parent, name, signature, kind, token),
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
        AssemblyAnalyzer analyzer, MetadataReader reader, TypeSpecificationHandle handle, int token)
    {
        // Decode the TypeSpec to a string, then find the underlying open generic type
        // by matching the name prefix (before the generic arguments) against TypeDefs/TypeRefs.
        string decoded;
        try
        {
            var ts = reader.GetTypeSpecification(handle);
            decoded = ts.DecodeSignature(new AssemblyAnalyzer.SignatureTypeProvider(), genericContext: default);
        }
        catch { decoded = analyzer.ResolveToken(token); }
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

    private static IlNavigationTarget ResolveMemberRefWithTypeSpecParent(
        AssemblyAnalyzer analyzer, MetadataReader reader, TypeSpecificationHandle typeSpecHandle,
        string name, string signature, MemberRefKind kind, int token)
    {
        // Decode the TypeSpec to find the underlying type name, then resolve the member.
        string decoded;
        try
        {
            var ts = reader.GetTypeSpecification(typeSpecHandle);
            decoded = ts.DecodeSignature(new AssemblyAnalyzer.SignatureTypeProvider(), genericContext: default);
        }
        catch { decoded = ""; }

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

        // Fallback: name-only search
        if (kind == MemberRefKind.Field)
        {
            var field = analyzer.FieldDefs.FirstOrDefault(f => f.Name == name);
            if (field is not null)
            {
                var dt = analyzer.TypeDefs.FirstOrDefault(t => t.FullName == field.DeclaringType);
                if (dt is not null) return new IlNavigationTarget.LocalField(field, dt);
            }
        }
        else
        {
            var method = analyzer.MethodDefs.FirstOrDefault(m => m.Name == name);
            if (method is not null) return new IlNavigationTarget.LocalMethod(method);
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
        // Count generic args to reconstruct the arity suffix
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
        AssemblyAnalyzer analyzer, MetadataReader reader, MethodSpecificationHandle handle, int token)
    {
        try
        {
            var ms = reader.GetMethodSpecification(handle);
            var methodToken = MetadataTokens.GetToken(ms.Method);
            var underlying = Resolve(analyzer, methodToken);
            if (underlying is IlNavigationTarget.LocalMethod) return underlying;
            return new IlNavigationTarget.GenericInstantiation(token, analyzer.ResolveToken(token));
        }
        catch { return new IlNavigationTarget.GenericInstantiation(token, analyzer.ResolveToken(token)); }
    }

    private static string GetAssemblyNameFromTypeRef(MetadataReader reader, TypeReferenceHandle handle)
    {
        try
        {
            var tr = reader.GetTypeReference(handle);
            return tr.ResolutionScope.Kind switch
            {
                HandleKind.AssemblyReference => reader.GetString(
                    reader.GetAssemblyReference((AssemblyReferenceHandle)tr.ResolutionScope).Name),
                HandleKind.TypeReference => GetAssemblyNameFromTypeRef(
                    reader, (TypeReferenceHandle)tr.ResolutionScope),
                _ => "Unknown"
            };
        }
        catch { return "Unknown"; }
    }

    private static string GetFullTypeRefName(MetadataReader reader, TypeReferenceHandle handle)
    {
        try
        {
            var tr = reader.GetTypeReference(handle);
            var name = reader.GetString(tr.Name);
            if (tr.ResolutionScope.Kind == HandleKind.TypeReference)
            {
                var outer = GetFullTypeRefName(reader, (TypeReferenceHandle)tr.ResolutionScope);
                return $"{outer}/{name}";
            }
            var ns = reader.GetString(tr.Namespace);
            return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        }
        catch { return "?"; }
    }
}
