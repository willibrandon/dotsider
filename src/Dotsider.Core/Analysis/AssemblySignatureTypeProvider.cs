using System.Collections.Immutable;
using System.Reflection.Metadata;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Converts decoded metadata signature types into stable display strings.
/// </summary>
internal sealed class AssemblySignatureTypeProvider : ISignatureTypeProvider<string, object?>
{
    private readonly bool _failOnInvalidMetadata;

    internal AssemblySignatureTypeProvider(bool failOnInvalidMetadata = false)
    {
        _failOnInvalidMetadata = failOnInvalidMetadata;
    }

    /// <inheritdoc/>
    public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode switch
    {
        PrimitiveTypeCode.Void => "void",
        PrimitiveTypeCode.Boolean => "bool",
        PrimitiveTypeCode.Char => "char",
        PrimitiveTypeCode.SByte => "sbyte",
        PrimitiveTypeCode.Byte => "byte",
        PrimitiveTypeCode.Int16 => "short",
        PrimitiveTypeCode.UInt16 => "ushort",
        PrimitiveTypeCode.Int32 => "int",
        PrimitiveTypeCode.UInt32 => "uint",
        PrimitiveTypeCode.Int64 => "long",
        PrimitiveTypeCode.UInt64 => "ulong",
        PrimitiveTypeCode.Single => "float",
        PrimitiveTypeCode.Double => "double",
        PrimitiveTypeCode.String => "string",
        PrimitiveTypeCode.Object => "object",
        PrimitiveTypeCode.IntPtr => "nint",
        PrimitiveTypeCode.UIntPtr => "nuint",
        PrimitiveTypeCode.TypedReference => "TypedReference",
        _ => typeCode.ToString(),
    };

    /// <inheritdoc/>
    public string GetTypeFromDefinition(
        MetadataReader reader,
        TypeDefinitionHandle handle,
        byte rawTypeKind)
    {
        var chain = MetadataNestingWalker.DeclaringTypeChain(reader, handle);
        if (MetadataNestingWalker.TryFormatTypeDefinitionName(chain, out var fullName))
        {
            return fullName;
        }

        if (_failOnInvalidMetadata)
        {
            throw new BadImageFormatException(
                $"TypeDef {MetadataNestingWalker.FormatToken(handle)} has an invalid declaring-type chain.");
        }

        return MetadataNestingWalker.FormatToken(handle);
    }

    /// <inheritdoc/>
    public string GetTypeFromReference(
        MetadataReader reader,
        TypeReferenceHandle handle,
        byte rawTypeKind)
    {
        var chain = MetadataNestingWalker.ResolutionScopeChain(reader, handle);
        if (MetadataNestingWalker.TryFormatTypeReferenceName(
            chain, out var fullName, out _))
        {
            return fullName;
        }

        if (_failOnInvalidMetadata)
        {
            throw new BadImageFormatException(
                $"TypeRef {MetadataNestingWalker.FormatToken(handle)} has an invalid resolution-scope chain.");
        }

        return MetadataNestingWalker.FormatToken(handle);
    }

    /// <inheritdoc/>
    public string GetSZArrayType(string elementType) => $"{elementType}[]";

    /// <inheritdoc/>
    public string GetArrayType(string elementType, ArrayShape shape) =>
        $"{elementType}[{new string(',', shape.Rank - 1)}]";

    /// <inheritdoc/>
    public string GetByReferenceType(string elementType) => $"ref {elementType}";

    /// <inheritdoc/>
    public string GetPointerType(string elementType) => $"{elementType}*";

    /// <inheritdoc/>
    public string GetGenericInstantiation(
        string genericType,
        ImmutableArray<string> typeArguments) =>
        $"{genericType}<{string.Join(", ", typeArguments)}>";

    /// <inheritdoc/>
    public string GetGenericMethodParameter(object? genericContext, int index) => $"!!{index}";

    /// <inheritdoc/>
    public string GetGenericTypeParameter(object? genericContext, int index) => $"!{index}";

    /// <inheritdoc/>
    public string GetPinnedType(string elementType) => $"pinned {elementType}";

    /// <inheritdoc/>
    public string GetTypeFromSpecification(
        MetadataReader reader,
        object? genericContext,
        TypeSpecificationHandle handle,
        byte rawTypeKind) =>
        throw new BadImageFormatException(
            "TypeSpec callbacks must be handled by a SafeSignatureDecoder validation session.");

    /// <inheritdoc/>
    public string GetFunctionPointerType(MethodSignature<string> signature)
    {
        var convention = signature.Header.CallingConvention switch
        {
            SignatureCallingConvention.Default => "managed",
            SignatureCallingConvention.CDecl => "unmanaged[Cdecl]",
            SignatureCallingConvention.StdCall => "unmanaged[Stdcall]",
            SignatureCallingConvention.ThisCall => "unmanaged[Thiscall]",
            SignatureCallingConvention.FastCall => "unmanaged[Fastcall]",
            SignatureCallingConvention.Unmanaged => "unmanaged",
            _ => "managed",
        };
        return $"delegate* {convention} {signature.ReturnType}({string.Join(", ", signature.ParameterTypes)})";
    }

    /// <inheritdoc/>
    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) =>
        isRequired ? $"modreq({modifier}) {unmodifiedType}" : $"modopt({modifier}) {unmodifiedType}";
}
