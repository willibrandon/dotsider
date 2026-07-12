using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Dotsider.Core.Analysis.Signatures;

/// <summary>
/// Performs a bounded, allocation-conscious structural validation of an ECMA-335 signature graph
/// before the framework's recursive decoder is allowed to observe it.
/// </summary>
internal sealed class SignatureBlobValidator
{
    internal const int MaxSignatureDepth = 128;
    internal const int MaxSignatureWork = 100_000;
    internal const int MaxArrayRank = 32;
    internal const int MaxTypeArguments = 1024;

    private const byte GenericAttribute = 0x10;
    private const byte InstanceAttribute = 0x20;
    private const byte ExplicitThisAttribute = 0x40;
    private const byte ReservedAttribute = 0x80;
    private const byte CallingConventionMask = 0x0F;

    private readonly MetadataReader _metadataReader;
    private HashSet<TypeSpecificationHandle>? _activeTypeSpecifications;
    private Dictionary<TypeSpecificationHandle, (int SaturatedWork, int MaxRelativeDepth)>?
        _typeSpecificationSummaries;
    private int _work;

    internal SignatureBlobValidator(MetadataReader metadataReader)
    {
        _metadataReader = metadataReader;
    }

    internal IReadOnlyDictionary<TypeSpecificationHandle, (int SaturatedWork, int MaxRelativeDepth)>?
        TypeSpecificationSummaries => _typeSpecificationSummaries;

    internal void ValidateMethodSignature(BlobHandle signature, SignatureCallerKind callerKind)
    {
        var reader = _metadataReader.GetBlobReader(signature);
        ValidateMethodSignature(ref reader, callerKind, typeDepth: 0);
        RequireComplete(reader);
    }

    internal void ValidateFieldSignature(BlobHandle signature)
    {
        var reader = _metadataReader.GetBlobReader(signature);
        RequireHeader(ref reader, expected: (byte)SignatureKind.Field);
        _ = ValidateType(
            ref reader,
            depth: 0,
            SignatureTypeContext.CustomModifiers |
            SignatureTypeContext.ByReference |
            SignatureTypeContext.TypedReference);
        RequireComplete(reader);
    }

    internal void ValidateLocalSignature(BlobHandle signature)
    {
        var reader = _metadataReader.GetBlobReader(signature);
        RequireHeader(ref reader, expected: (byte)SignatureKind.LocalVariables);
        var count = ReadCompressedUnsigned(ref reader, "local variable count");
        RequireSequenceCount(count, minimum: 1, maximum: RemainingWork, reader.RemainingBytes, "local variable count");

        for (var i = 0; i < count; i++)
        {
            _ = ValidateType(
                ref reader,
                depth: 0,
                SignatureTypeContext.CustomModifiers |
                SignatureTypeContext.ByReference |
                SignatureTypeContext.Pinned |
                SignatureTypeContext.TypedReference);
        }

        RequireComplete(reader);
    }

    internal void ValidateMethodSpecificationSignature(BlobHandle signature)
    {
        var reader = _metadataReader.GetBlobReader(signature);
        RequireHeader(ref reader, expected: (byte)SignatureKind.MethodSpecification);
        var count = ReadCompressedUnsigned(ref reader, "method type argument count");
        RequireSequenceCount(count, minimum: 1, maximum: MaxTypeArguments, reader.RemainingBytes, "method type argument count");

        for (var i = 0; i < count; i++)
        {
            _ = ValidateType(
                ref reader,
                depth: 0,
                SignatureTypeContext.CustomModifiers);
        }

        RequireComplete(reader);
    }

    internal void ValidateTypeSpecification(TypeSpecificationHandle handle)
    {
        RequireTypeSpecificationRow(handle);
        _ = ValidateTypeSpecification(handle, depth: 0);
    }

    private int ValidateMethodSignature(
        ref BlobReader reader,
        SignatureCallerKind callerKind,
        int typeDepth)
    {
        var header = ReadByte(ref reader, "signature header");
        ValidateMethodHeader(header, callerKind);

        if (callerKind == SignatureCallerKind.PropertyDefinition)
        {
            var propertyParameterCount = ReadCompressedUnsigned(ref reader, "property parameter count");
            RequireSequenceCount(
                propertyParameterCount,
                minimum: 0,
                maximum: RemainingWork,
                reader.RemainingBytes,
                "property parameter count");

            var maximumDepth = ValidateType(
                ref reader,
                typeDepth,
                SignatureTypeContext.CustomModifiers |
                SignatureTypeContext.ByReference |
                SignatureTypeContext.TypedReference);
            for (var i = 0; i < propertyParameterCount; i++)
            {
                maximumDepth = Math.Max(maximumDepth, ValidateParameter(ref reader, typeDepth));
            }
            return maximumDepth;
        }

        if ((header & GenericAttribute) != 0)
        {
            var genericArity = ReadCompressedUnsigned(ref reader, "generic parameter count");
            RequireScalar(genericArity, minimum: 1, maximum: MaxTypeArguments, "generic parameter count");
        }

        var parameterCount = ReadCompressedUnsigned(ref reader, "parameter count");
        RequireSequenceCount(
            parameterCount,
            minimum: 0,
            maximum: RemainingWork,
            reader.RemainingBytes,
            "parameter count");

        var maxDepth = ValidateReturnType(ref reader, typeDepth);
        var sentinelSeen = false;
        for (var i = 0; i < parameterCount; i++)
        {
            if (PeekByte(reader) == (byte)SignatureTypeCode.Sentinel)
            {
                if (sentinelSeen || !AllowsSentinel(header, callerKind))
                {
                    ThrowMalformed("SENTINEL is only valid once, before an optional parameter in a vararg call-site signature.");
                }

                _ = reader.ReadByte();
                sentinelSeen = true;
            }

            maxDepth = Math.Max(maxDepth, ValidateParameter(ref reader, typeDepth));
        }

        return maxDepth;
    }

    private int ValidateReturnType(ref BlobReader reader, int depth) =>
        ValidateType(
            ref reader,
            depth,
            SignatureTypeContext.CustomModifiers |
            SignatureTypeContext.ByReference |
            SignatureTypeContext.TypedReference |
            SignatureTypeContext.Void);

    private int ValidateParameter(ref BlobReader reader, int depth) =>
        ValidateType(
            ref reader,
            depth,
            SignatureTypeContext.CustomModifiers |
            SignatureTypeContext.ByReference |
            SignatureTypeContext.TypedReference);

    private int ValidateType(
        ref BlobReader reader,
        int depth,
        SignatureTypeContext context)
    {
        var code = PeekByte(reader);

        if (code is (byte)SignatureTypeCode.RequiredModifier or (byte)SignatureTypeCode.OptionalModifier)
        {
            if ((context & SignatureTypeContext.CustomModifiers) == 0)
            {
                ThrowMalformed("A custom modifier is not valid in this signature position.");
            }

            EnterTypeNode(depth);
            _ = reader.ReadByte();
            var maxDepth = depth;
            maxDepth = Math.Max(maxDepth, ReadTypeHandle(ref reader, allowTypeSpecification: true, depth + 1));
            maxDepth = Math.Max(maxDepth, ValidateType(ref reader, depth + 1, context));
            return maxDepth;
        }

        if (code == (byte)SignatureTypeCode.Pinned)
        {
            if ((context & SignatureTypeContext.Pinned) == 0)
            {
                ThrowMalformed("PINNED is only valid in a local-variable signature.");
            }

            EnterTypeNode(depth);
            _ = reader.ReadByte();
            var childContext = context & ~SignatureTypeContext.Pinned;
            return Math.Max(depth, ValidateType(ref reader, depth + 1, childContext));
        }

        if (code == (byte)SignatureTypeCode.ByReference)
        {
            if ((context & SignatureTypeContext.ByReference) == 0)
            {
                ThrowMalformed("BYREF is not valid in this signature position.");
            }

            EnterTypeNode(depth);
            _ = reader.ReadByte();
            return Math.Max(
                depth,
                ValidateType(
                    ref reader,
                    depth + 1,
                    SignatureTypeContext.CustomModifiers |
                    SignatureTypeContext.TypedReference));
        }

        if (code == (byte)SignatureTypeCode.Void)
        {
            if ((context & SignatureTypeContext.Void) == 0)
            {
                ThrowMalformed("VOID is not valid in this signature position.");
            }

            EnterTypeNode(depth);
            _ = reader.ReadByte();
            return depth;
        }

        if (code == (byte)SignatureTypeCode.TypedReference)
        {
            if ((context & SignatureTypeContext.TypedReference) == 0)
            {
                ThrowMalformed("TYPEDBYREF is not valid in this signature position.");
            }

            EnterTypeNode(depth);
            _ = reader.ReadByte();
            return depth;
        }

        return ValidateTypeCore(ref reader, depth);
    }

    private int ValidateTypeCore(ref BlobReader reader, int depth)
    {
        EnterTypeNode(depth);
        var code = ReadByte(ref reader, "type code");

        switch ((SignatureTypeCode)code)
        {
            case SignatureTypeCode.Boolean:
            case SignatureTypeCode.Char:
            case SignatureTypeCode.SByte:
            case SignatureTypeCode.Byte:
            case SignatureTypeCode.Int16:
            case SignatureTypeCode.UInt16:
            case SignatureTypeCode.Int32:
            case SignatureTypeCode.UInt32:
            case SignatureTypeCode.Int64:
            case SignatureTypeCode.UInt64:
            case SignatureTypeCode.Single:
            case SignatureTypeCode.Double:
            case SignatureTypeCode.IntPtr:
            case SignatureTypeCode.UIntPtr:
            case SignatureTypeCode.Object:
            case SignatureTypeCode.String:
                return depth;

            case SignatureTypeCode.Pointer:
                return Math.Max(
                    depth,
                    ValidateType(
                        ref reader,
                        depth + 1,
                        SignatureTypeContext.CustomModifiers |
                        SignatureTypeContext.TypedReference |
                        SignatureTypeContext.Void));

            case SignatureTypeCode.SZArray:
                return Math.Max(
                    depth,
                    ValidateType(
                        ref reader,
                        depth + 1,
                        SignatureTypeContext.CustomModifiers));

            case SignatureTypeCode.Array:
                return ValidateArray(ref reader, depth);

            case SignatureTypeCode.FunctionPointer:
                return Math.Max(
                    depth,
                    ValidateMethodSignature(ref reader, SignatureCallerKind.FunctionPointer, depth + 1));

            case SignatureTypeCode.GenericTypeInstance:
                return ValidateGenericInstantiation(ref reader, depth);

            case SignatureTypeCode.GenericTypeParameter:
                _ = ReadCompressedUnsigned(ref reader, "generic type parameter index");
                return depth;

            case SignatureTypeCode.GenericMethodParameter:
                _ = ReadCompressedUnsigned(ref reader, "generic method parameter index");
                return depth;

            case (SignatureTypeCode)SignatureTypeKind.Class:
            case (SignatureTypeCode)SignatureTypeKind.ValueType:
                _ = ReadTypeHandle(ref reader, allowTypeSpecification: false, depth);
                return depth;

            case SignatureTypeCode.Void:
            case SignatureTypeCode.TypedReference:
            case SignatureTypeCode.ByReference:
            case SignatureTypeCode.Pinned:
            case SignatureTypeCode.RequiredModifier:
            case SignatureTypeCode.OptionalModifier:
            case SignatureTypeCode.Sentinel:
            default:
                ThrowMalformed($"Unexpected signature type code 0x{code:X2}.");
                return depth;
        }
    }

    private int ValidateArray(ref BlobReader reader, int depth)
    {
        var maxDepth = Math.Max(
            depth,
            ValidateType(
                ref reader,
                depth + 1,
                SignatureTypeContext.CustomModifiers));
        var rank = ReadCompressedUnsigned(ref reader, "array rank");
        RequireScalar(rank, minimum: 1, maximum: MaxArrayRank, "array rank");

        var sizes = ReadCompressedUnsigned(ref reader, "array size count");
        RequireSequenceCount(sizes, minimum: 0, maximum: rank, reader.RemainingBytes, "array size count");
        for (var i = 0; i < sizes; i++)
        {
            _ = ReadCompressedUnsigned(ref reader, "array size");
        }

        var lowerBounds = ReadCompressedUnsigned(ref reader, "array lower-bound count");
        RequireSequenceCount(
            lowerBounds,
            minimum: 0,
            maximum: rank,
            reader.RemainingBytes,
            "array lower-bound count");
        for (var i = 0; i < lowerBounds; i++)
        {
            _ = ReadCompressedSigned(ref reader, "array lower bound");
        }

        return maxDepth;
    }

    private int ValidateGenericInstantiation(ref BlobReader reader, int depth)
    {
        var baseKind = ReadByte(ref reader, "generic instantiation base kind");
        if (baseKind is not ((byte)SignatureTypeKind.Class) and not ((byte)SignatureTypeKind.ValueType))
        {
            ThrowMalformed("A GENERICINST base must be CLASS or VALUETYPE.");
        }

        _ = ReadTypeHandle(ref reader, allowTypeSpecification: false, depth);
        var count = ReadCompressedUnsigned(ref reader, "generic type argument count");
        RequireSequenceCount(
            count,
            minimum: 1,
            maximum: MaxTypeArguments,
            reader.RemainingBytes,
            "generic type argument count");

        var maxDepth = depth;
        for (var i = 0; i < count; i++)
        {
            maxDepth = Math.Max(
                maxDepth,
                ValidateType(
                    ref reader,
                    depth + 1,
                    SignatureTypeContext.CustomModifiers));
        }
        return maxDepth;
    }

    private int ValidateTypeSpecification(TypeSpecificationHandle handle, int depth)
    {
        EnsureDepth(depth);

        if (_typeSpecificationSummaries is not null &&
            _typeSpecificationSummaries.TryGetValue(handle, out var summary))
        {
            var maximumDepth = checked(depth + summary.MaxRelativeDepth);
            EnsureDepth(maximumDepth);
            ChargeWork(summary.SaturatedWork);
            return maximumDepth;
        }

        _activeTypeSpecifications ??= [];
        if (!_activeTypeSpecifications.Add(handle))
        {
            ThrowMalformed($"Cyclic TypeSpec graph at token 0x{MetadataTokens.GetToken(handle):X8}.");
        }

        var workBefore = _work;
        try
        {
            var reader = _metadataReader.GetBlobReader(_metadataReader.GetTypeSpecification(handle).Signature);
            var maximumDepth = ValidateTypeSpecificationRoot(ref reader, depth);
            RequireComplete(reader);

            var saturatedWork = _work - workBefore;
            _typeSpecificationSummaries ??= [];
            _typeSpecificationSummaries.Add(handle, (saturatedWork, maximumDepth - depth));
            return maximumDepth;
        }
        finally
        {
            _activeTypeSpecifications.Remove(handle);
        }
    }

    private int ValidateTypeSpecificationRoot(ref BlobReader reader, int depth)
    {
        var code = PeekByte(reader);
        if (code is (byte)SignatureTypeKind.Class or (byte)SignatureTypeKind.ValueType)
        {
            ThrowMalformed(
                $"TypeSpec root type code 0x{code:X2} is a redundant direct type-handle production.");
        }

        return ValidateType(
            ref reader,
            depth,
            SignatureTypeContext.CustomModifiers |
            SignatureTypeContext.ByReference |
            SignatureTypeContext.TypedReference |
            SignatureTypeContext.Void);
    }

    private int ReadTypeHandle(ref BlobReader reader, bool allowTypeSpecification, int depth)
    {
        var encoded = ReadCompressedUnsigned(ref reader, "type handle");
        var row = encoded >> 2;
        if (row == 0)
        {
            ThrowMalformed("A signature type handle must not be nil.");
        }

        switch (encoded & 0x03)
        {
            case 0:
                RequireRow(row, TableIndex.TypeDef, "TypeDef");
                return depth;
            case 1:
                RequireRow(row, TableIndex.TypeRef, "TypeRef");
                return depth;
            case 2 when allowTypeSpecification:
                RequireRow(row, TableIndex.TypeSpec, "TypeSpec");
                return ValidateTypeSpecification(MetadataTokens.TypeSpecificationHandle(row), depth);
            case 2:
                ThrowMalformed("A TypeSpec token is not valid in this signature position.");
                return depth;
            default:
                ThrowMalformed("A signature type handle has a reserved table tag.");
                return depth;
        }
    }

    private void RequireTypeSpecificationRow(TypeSpecificationHandle handle)
    {
        var row = MetadataTokens.GetRowNumber(handle);
        RequireRow(row, TableIndex.TypeSpec, "TypeSpec");
    }

    private void RequireRow(int row, TableIndex table, string tableName)
    {
        var count = _metadataReader.GetTableRowCount(table);
        if (row <= 0 || row > count)
        {
            ThrowMalformed($"{tableName} row {row} is outside the table's 1..{count} range.");
        }
    }

    private static void ValidateMethodHeader(byte header, SignatureCallerKind callerKind)
    {
        if ((header & ReservedAttribute) != 0)
        {
            ThrowMalformed($"Signature header 0x{header:X2} uses a reserved attribute bit.");
        }

        if (callerKind == SignatureCallerKind.PropertyDefinition)
        {
            if (header is not ((byte)SignatureKind.Property) and
                not ((byte)SignatureKind.Property | InstanceAttribute))
            {
                ThrowMalformed($"Property signature header 0x{header:X2} is invalid.");
            }
            return;
        }

        var convention = header & CallingConventionMask;
        var validConvention = callerKind switch
        {
            SignatureCallerKind.MethodDefinition or SignatureCallerKind.MemberReference =>
                convention is (byte)SignatureCallingConvention.Default or (byte)SignatureCallingConvention.VarArgs,
            SignatureCallerKind.StandaloneSignature or SignatureCallerKind.FunctionPointer =>
                convention <= (byte)SignatureCallingConvention.VarArgs ||
                convention == (byte)SignatureCallingConvention.Unmanaged,
            _ => false,
        };
        if (!validConvention)
        {
            ThrowMalformed($"Calling convention 0x{convention:X1} is invalid for {callerKind}.");
        }

        if ((header & ExplicitThisAttribute) != 0 && (header & InstanceAttribute) == 0)
        {
            ThrowMalformed("EXPLICITTHIS requires HASTHIS.");
        }

        if (callerKind is SignatureCallerKind.MethodDefinition or SignatureCallerKind.MemberReference &&
            (header & ExplicitThisAttribute) != 0)
        {
            ThrowMalformed("EXPLICITTHIS is valid only in standalone and function-pointer signatures.");
        }

        if (callerKind == SignatureCallerKind.StandaloneSignature && (header & GenericAttribute) != 0)
        {
            ThrowMalformed("A standalone method signature cannot declare generic parameters.");
        }
    }

    private static bool AllowsSentinel(byte header, SignatureCallerKind callerKind)
    {
        if (callerKind is SignatureCallerKind.MethodDefinition or SignatureCallerKind.PropertyDefinition)
        {
            return false;
        }

        var convention = header & CallingConventionMask;
        return convention == (byte)SignatureCallingConvention.VarArgs ||
            ((callerKind == SignatureCallerKind.StandaloneSignature ||
              callerKind == SignatureCallerKind.FunctionPointer) &&
             convention == (byte)SignatureCallingConvention.CDecl);
    }

    private static void RequireHeader(ref BlobReader reader, byte expected)
    {
        var actual = ReadByte(ref reader, "signature header");
        if (actual != expected)
        {
            ThrowMalformed($"Signature header 0x{actual:X2} does not match expected header 0x{expected:X2}.");
        }
    }

    private static void RequireComplete(BlobReader reader)
    {
        if (reader.RemainingBytes != 0)
        {
            ThrowMalformed($"Signature contains {reader.RemainingBytes} trailing byte(s).");
        }
    }

    private static void RequireScalar(int value, int minimum, int maximum, string name)
    {
        if (value < minimum || value > maximum)
        {
            ThrowMalformed($"Signature {name} {value} is outside the supported {minimum}..{maximum} range.");
        }
    }

    private static void RequireSequenceCount(
        int value,
        int minimum,
        int maximum,
        int remainingBytes,
        string name)
    {
        RequireScalar(value, minimum, maximum, name);
        if (value > remainingBytes)
        {
            ThrowMalformed($"Signature {name} {value} cannot fit in the remaining {remainingBytes} byte(s).");
        }
    }

    private int RemainingWork => MaxSignatureWork - _work;

    private void EnterTypeNode(int depth)
    {
        EnsureDepth(depth);
        ChargeWork(1);
    }

    private static void EnsureDepth(int depth)
    {
        if (depth > MaxSignatureDepth)
        {
            ThrowMalformed($"Signature nesting depth {depth} exceeds supported maximum {MaxSignatureDepth}.");
        }
    }

    private void ChargeWork(int work)
    {
        if (work < 0 || work > MaxSignatureWork - _work)
        {
            var attempted = (long)_work + work;
            ThrowMalformed($"Signature expanded work {attempted} exceeds supported maximum {MaxSignatureWork}.");
        }

        _work += work;
    }

    private static byte PeekByte(BlobReader reader)
    {
        if (reader.RemainingBytes == 0)
        {
            ThrowMalformed("Signature ended before the next type was complete.");
        }
        return reader.ReadByte();
    }

    private static byte ReadByte(ref BlobReader reader, string name)
    {
        if (reader.RemainingBytes == 0)
        {
            ThrowMalformed($"Signature ended before {name}.");
        }
        return reader.ReadByte();
    }

    private static int ReadCompressedUnsigned(ref BlobReader reader, string name)
    {
        var start = reader.Offset;
        var value = reader.ReadCompressedInteger();
        if (value < 0)
        {
            ThrowMalformed($"Signature {name} has an invalid compressed unsigned integer.");
        }

        var encodedLength = reader.Offset - start;
        var minimalLength = value <= 0x7F ? 1 : value <= 0x3FFF ? 2 : 4;
        if (encodedLength != minimalLength)
        {
            ThrowMalformed($"Signature {name} uses a non-minimal compressed unsigned integer.");
        }
        return value;
    }

    private static int ReadCompressedSigned(ref BlobReader reader, string name)
    {
        var start = reader.Offset;
        var value = reader.ReadCompressedSignedInteger();
        var encodedLength = reader.Offset - start;
        var minimalLength = value is >= -64 and <= 63 ? 1 : value is >= -8192 and <= 8191 ? 2 : 4;
        if (encodedLength != minimalLength)
        {
            ThrowMalformed($"Signature {name} uses a non-minimal compressed signed integer.");
        }
        return value;
    }

    private static void ThrowMalformed(string message) =>
        throw new BadImageFormatException(message);
}
