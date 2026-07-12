using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Dotsider.Core.Analysis.ReadyToRun;

internal sealed class ReadyToRunSignatureWalkerState(
    R2RNativeReader reader,
    int offset,
    MetadataReader? metadata,
    Func<int, MetadataReader?>? moduleMetadata,
    MetadataReader? systemMetadata)
{
    // ReadyToRunMethodSigFlags (readytorun.h).
    private const uint SigUnboxingStub = 0x01;
    private const uint SigInstantiatingStub = 0x02;
    private const uint SigMethodInstantiation = 0x04;
    private const uint SigSlotInsteadOfToken = 0x08;
    private const uint SigMemberRefToken = 0x10;
    private const uint SigConstrained = 0x20;
    private const uint SigOwnerType = 0x40;
    private const uint SigUpdateContext = 0x80;
    private const uint SigAsyncVariant = 0x100;
    private const uint KnownMethodFlags = SigUnboxingStub | SigInstantiatingStub
        | SigMethodInstantiation | SigSlotInsteadOfToken | SigMemberRefToken | SigConstrained
        | SigOwnerType | SigUpdateContext | SigAsyncVariant;
    private const uint MaxSignatureDepth = 128;
    private const uint MaxSignatureItemCount = 1024;
    private const uint MaxArrayRank = 256;

    private readonly MetadataReader? _outerMetadata = metadata;
    private readonly int _startOffset = offset;
    private int _depth;
    private int _offset = offset;
    private List<string>? _instantiation;
    private MetadataReader? _methodMetadata;
    private MetadataReader? _metadata = metadata;
    private bool _methodMetadataSelected;
    private string? _ownerDisplay;
    private bool _parsingOwnerType;

    private int _topLevelModuleIndex = -1;
    private int _ownerModuleIndex = -1;

    internal int Offset => _offset;
    internal int MethodToken { get; private set; }
    internal bool CrossModule { get; private set; }

    // The owning module: a top-level ModuleOverride, else the MODULE_ZAPSIG that wraps the owner
    // type (the composite form — the method token then resolves in that component, not the manifest).
    internal int ModuleIndex => _topLevelModuleIndex >= 0 ? _topLevelModuleIndex : _ownerModuleIndex;

    // A method instantiation (Identity&lt;int&gt;) renders as its args; a method on an
    // instantiated generic type (Box&lt;int&gt;.Describe) renders the owning type instead.
    internal string? RenderInstantiation() =>
        _instantiation is { Count: > 0 } instantiation ? $"<{string.Join(", ", instantiation)}>"
        : _ownerDisplay is { } owner && owner.Contains('<') ? $" ({owner})"
        : null;

    internal void ParseMethod()
    {
        var flags = ReadUInt();
        ReadyToRunDiagnostics.Write($"method-start offset=0x{_startOffset:X} flags=0x{flags:X}");
        if ((flags & ~KnownMethodFlags) != 0)
        {
            throw new BadImageFormatException(
                $"ReadyToRun method signature has unknown flags 0x{flags & ~KnownMethodFlags:X}.");
        }

        if ((flags & (SigSlotInsteadOfToken | SigMemberRefToken))
            == (SigSlotInsteadOfToken | SigMemberRefToken))
        {
            throw new BadImageFormatException(
                "ReadyToRun method signature cannot combine slot and MemberRef token forms.");
        }

        if ((flags & SigUpdateContext) != 0)
        {
            _topLevelModuleIndex = (int)ReadUInt(); // the token resolves in the module at this index
            CrossModule = true;
            flags &= ~SigUpdateContext;
            ReadyToRunDiagnostics.Write(
                $"method-update-context start=0x{_startOffset:X} module={_topLevelModuleIndex} next=0x{_offset:X}");
            WithMetadata(moduleMetadata?.Invoke(_topLevelModuleIndex), () => ParseMethodBody(flags));
            ReadyToRunDiagnostics.Write(
                $"method-end start=0x{_startOffset:X} end=0x{_offset:X} token=0x{MethodToken:X8} module={ModuleIndex}");
            return;
        }

        ParseMethodBody(flags);
        ReadyToRunDiagnostics.Write(
            $"method-end start=0x{_startOffset:X} end=0x{_offset:X} token=0x{MethodToken:X8} module={ModuleIndex}");
    }

    private void ParseMethodBody(uint flags)
    {
        if ((flags & SigOwnerType) != 0)
        {
            _parsingOwnerType = true;
            try
            {
                _ownerDisplay = SkipType();
            }
            finally
            {
                _parsingOwnerType = false;
            }

            // Composite signatures omit MODULE_ZAPSIG only for primitive owner types from the
            // system module. Match the runtime reader's fallback before validating the method RID.
            if (!_methodMetadataSelected && _topLevelModuleIndex < 0 && systemMetadata is not null)
            {
                _methodMetadata = systemMetadata;
                _methodMetadataSelected = true;
            }
            flags &= ~SigOwnerType;
        }

        if ((flags & SigSlotInsteadOfToken) != 0)
        {
            ReadUInt(); // a vtable slot rather than a token
            flags &= ~SigSlotInsteadOfToken;
        }
        else if ((flags & SigMemberRefToken) != 0)
        {
            MethodToken = ReadMethodToken(HandleKind.MemberReference);
            flags &= ~SigMemberRefToken;
        }
        else
        {
            MethodToken = ReadMethodToken(HandleKind.MethodDefinition);
        }

        if ((flags & SigMethodInstantiation) != 0)
        {
            var argCount = ReadUInt();
            EnsureBounded(argCount, MaxSignatureItemCount, "method generic argument count");
            if (argCount > 0)
            {
                var instantiation = _instantiation ??= new List<string>((int)argCount);
                for (var i = 0; i < argCount; i++)
                {
                    instantiation.Add(SkipType());
                }
            }
            flags &= ~SigMethodInstantiation;
        }

        if ((flags & SigConstrained) != 0)
        {
            SkipType();
        }

    }

    // Walks a type signature, advancing the offset and returning a best-effort display name.
    private string SkipType()
    {
        EnsureBounded((uint)_depth, MaxSignatureDepth, "type nesting depth");
        _depth++;
        try
        {
            return SkipTypeCore();
        }
        finally
        {
            _depth--;
        }
    }

    private string SkipTypeCore()
    {
        var elementType = reader.ReadByte(ref _offset) & 0x7F;
        ReadyToRunDiagnostics.Write(
            $"type start=0x{_startOffset:X} offset=0x{_offset - 1:X} element=0x{elementType:X2}");
        switch (elementType)
        {
            case 0x01: return "void";
            case 0x02: return "bool";
            case 0x03: return "char";
            case 0x04: return "sbyte";
            case 0x05: return "byte";
            case 0x06: return "short";
            case 0x07: return "ushort";
            case 0x08: return "int";
            case 0x09: return "uint";
            case 0x0a: return "long";
            case 0x0b: return "ulong";
            case 0x0c: return "float";
            case 0x0d: return "double";
            case 0x0e: return "string";
            case 0x16: return "TypedReference";
            case 0x18: return "nint";
            case 0x19: return "nuint";
            case 0x1c: return "object";
            case 0x0f: return SkipType() + "*";                  // PTR
            case 0x10: return "ref " + SkipType();               // BYREF
            case 0x1d: return SkipType() + "[]";                 // SZARRAY
            case 0x45: return SkipType();                        // PINNED
            case 0x11: case 0x12: return TypeTokenName(ReadToken()); // VALUETYPE / CLASS
            case 0x13: return $"!{ReadUInt()}";                  // VAR
            case 0x1e: return $"!!{ReadUInt()}";                 // MVAR
            case 0x3e: return "__Canon";                         // CANON_ZAPSIG
            case 0x3f:                                           // MODULE_ZAPSIG
                {
                    var moduleIndex = (int)ReadUInt();
                    var nextMetadata = moduleMetadata?.Invoke(moduleIndex);
                    // Match ReadyToRunReader.GetMetadataReaderFromModuleOverride: only a leading
                    // module override on the owner type selects the method token's metadata scope.
                    // Overrides in type arguments or constraints apply only while decoding that type.
                    if (_parsingOwnerType && _depth == 1 && _topLevelModuleIndex < 0)
                    {
                        _ownerModuleIndex = moduleIndex;
                        _methodMetadata = nextMetadata;
                        _methodMetadataSelected = true;
                        CrossModule = true;
                    }
                    ReadyToRunDiagnostics.Write(
                        $"type-module start=0x{_startOffset:X} module={moduleIndex} next=0x{_offset:X}");
                    return WithMetadata(nextMetadata, SkipType);
                }
            case 0x3b: ReadUInt(); return "var";                 // VAR_ZAPSIG
            case 0x3d: return SkipType();                        // NATIVE_VALUETYPE_ZAPSIG
            case 0x1f:
            case 0x20:                                // CMOD_REQD / CMOD_OPT
                ReadToken();
                return SkipType();
            case 0x14:                                           // ARRAY
                {
                    var element = SkipType();
                    var rank = ReadUInt();
                    EnsureBounded(rank, MaxArrayRank, "array rank");
                    if (rank == 0)
                    {
                        return element + "[]";
                    }
                    var sizes = ReadUInt();
                    EnsureBounded(sizes, MaxArrayRank, "array size count");
                    for (var i = 0; i < sizes; i++)
                    {
                        ReadUInt();
                    }
                    var lowerBounds = ReadUInt();
                    EnsureBounded(lowerBounds, MaxArrayRank, "array lower-bound count");
                    for (var i = 0; i < lowerBounds; i++)
                    {
                        ReadInt();
                    }
                    return $"{element}[{new string(',', (int)rank - 1)}]";
                }
            case 0x15:                                           // GENERICINST
                {
                    var generic = SkipType();
                    var argCount = ReadUInt();
                    EnsureBounded(argCount, MaxSignatureItemCount, "type generic argument count");
                    ReadyToRunDiagnostics.Write(
                        $"type-genericinst start=0x{_startOffset:X} args={argCount} next=0x{_offset:X}");
                    var args = new string[argCount];
                    WithMetadata(_outerMetadata, () =>
                    {
                        for (var i = 0; i < argCount; i++)
                        {
                            args[i] = SkipType();
                        }
                    });
                    return $"{generic}<{string.Join(", ", args)}>";
                }
            case 0x1b:                                           // FNPTR
                {
                    var header = reader.ReadByte(ref _offset);
                    if ((header & 0x10) != 0)
                    {
                        ReadUInt(); // generic param count
                    }
                    var paramCount = ReadUInt();
                    EnsureBounded(paramCount, MaxSignatureItemCount, "function pointer parameter count");
                    SkipType(); // return
                    for (var i = 0; i < paramCount; i++)
                    {
                        while ((reader.PeekByte(_offset) & 0x7F) == 0x41)
                        {
                            reader.ReadByte(ref _offset); // SENTINEL
                        }
                        SkipType();
                    }

                    return "delegate*";
                }
            default:
                throw new BadImageFormatException(
                    $"ReadyToRun signature contains unsupported element type 0x{elementType:X2}.");
        }
    }

    private string TypeTokenName(int token)
    {
        EntityHandle handle;
        try
        {
            handle = MetadataTokens.EntityHandle(token);
        }
        catch (ArgumentException exception)
        {
            throw new BadImageFormatException(
                $"ReadyToRun signature contains invalid type token 0x{token:X8}.", exception);
        }

        if (handle.Kind is not (HandleKind.TypeDefinition or HandleKind.TypeReference))
        {
            throw new BadImageFormatException(
                $"ReadyToRun signature type token 0x{token:X8} has invalid kind {handle.Kind}.");
        }

        var row = MetadataTokens.GetRowNumber(handle);
        if (row <= 0)
        {
            throw new BadImageFormatException(
                $"ReadyToRun signature type token 0x{token:X8} has invalid row {row}.");
        }

        if (_metadata is null)
        {
            return "Type";
        }

        var rowCount = handle.Kind == HandleKind.TypeDefinition
            ? _metadata.TypeDefinitions.Count
            : _metadata.TypeReferences.Count;
        if (!IsValidRow(row, rowCount))
        {
            throw new BadImageFormatException(
                $"ReadyToRun signature type token 0x{token:X8} row {row} exceeds the "
                + $"{handle.Kind} table size {rowCount}.");
        }

        try
        {
            ReadyToRunDiagnostics.Write(
                $"type-token offset=0x{_offset:X} token=0x{token:X8} kind={handle.Kind} row={row}");
            var name = handle.Kind == HandleKind.TypeDefinition
                ? _metadata.GetString(_metadata.GetTypeDefinition((TypeDefinitionHandle)handle).Name)
                : _metadata.GetString(_metadata.GetTypeReference((TypeReferenceHandle)handle).Name);
            if (name.Length == 0)
            {
                throw new BadImageFormatException(
                    $"ReadyToRun signature type token 0x{token:X8} has an empty name.");
            }

            return name;
        }
        catch (BadImageFormatException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            throw new BadImageFormatException(
                $"ReadyToRun signature type token 0x{token:X8} has malformed metadata.", exception);
        }
    }

    private static void EnsureBounded(uint value, uint max, string name)
    {
        if (value > max)
        {
            throw new BadImageFormatException($"ReadyToRun signature {name} {value} exceeds supported maximum {max}.");
        }
    }

    private static bool IsValidRow(int row, int count) =>
        row > 0 && row <= count;

    private int ReadMethodToken(HandleKind kind)
    {
        var rid = ReadUInt();
        var metadata = _methodMetadataSelected ? _methodMetadata : _metadata;
        return ReadyToRunMethodToken.Create(rid, kind, metadata);
    }

    // Reads a signature token: an ECMA compressed value whose low 2 bits pick the table.
    private int ReadToken()
    {
        var encoded = ReadUInt();
        var rid = (int)(encoded >> 2);
        return (encoded & 3) switch
        {
            0 => 0x0200_0000 | rid, // TypeDef
            1 => 0x0100_0000 | rid, // TypeRef
            2 => 0x1B00_0000 | rid, // TypeSpec
            _ => rid,               // base type
        };
    }

    private uint ReadUInt() => reader.ReadCompressedUInt(ref _offset);

    private int ReadInt() => reader.ReadCompressedInt(ref _offset);

    private void WithMetadata(MetadataReader? next, Action action)
    {
        var previous = _metadata;
        _metadata = next;
        try
        {
            action();
        }
        finally
        {
            _metadata = previous;
        }
    }

    private string WithMetadata(MetadataReader? next, Func<string> action)
    {
        var previous = _metadata;
        _metadata = next;
        try
        {
            return action();
        }
        finally
        {
            _metadata = previous;
        }
    }
}
