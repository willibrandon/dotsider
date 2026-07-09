using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Dotsider.Core.Analysis.ReadyToRun;

/// <summary>
/// Walks an <c>InstanceMethodEntryPoints</c> method signature (the crossgen2 encoding) to recover
/// the owning MethodDef token and a rendered instantiation, and — critically — to advance to the
/// runtime-function index that follows it so the entry point can be marked. Signatures use the
/// ECMA compressed-integer codec and a recursive type grammar; every form is walked so the offset
/// lands correctly even when a shape is only summarized for display.
/// </summary>
internal static class ReadyToRunSignatureWalker
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
    private const uint MaxSignatureItemCount = 1024;
    private const uint MaxArrayRank = 256;

    /// <summary>The result of walking one method signature.</summary>
    /// <param name="Offset">The file offset immediately after the signature (where the runtime-function index begins).</param>
    /// <param name="MethodToken">The recovered method token (a MethodDef or MemberRef), or 0 when unavailable.</param>
    /// <param name="InstantiationDisplay">A rendered method instantiation such as <c>&lt;int&gt;</c>, or null.</param>
    /// <param name="CrossModule">Whether the signature overrides the module context (its token resolves in another module).</param>
    /// <param name="ModuleIndex">The module-override index identifying the owning module, or -1 when there is no override.</param>
    public readonly record struct MethodSignature(
        int Offset, int MethodToken, string? InstantiationDisplay, bool CrossModule, int ModuleIndex);

    /// <summary>Walks the method signature at <paramref name="offset"/>.</summary>
    /// <param name="reader">The image reader.</param>
    /// <param name="offset">The file offset of the signature.</param>
    /// <param name="metadata">The metadata reader for resolving token names, or null.</param>
    /// <param name="moduleMetadata">Resolves a ReadyToRun module override index to metadata, or null when unavailable.</param>
    public static MethodSignature ParseMethod(
        R2RNativeReader reader,
        int offset,
        MetadataReader? metadata,
        Func<int, MetadataReader?>? moduleMetadata = null)
    {
        var walker = new Walker(reader, offset, metadata, moduleMetadata);
        walker.ParseMethod();
        return new MethodSignature(
            walker.Offset, walker.MethodToken, walker.RenderInstantiation(), walker.CrossModule, walker.ModuleIndex);
    }

    private sealed class Walker(
        R2RNativeReader reader,
        int offset,
        MetadataReader? metadata,
        Func<int, MetadataReader?>? moduleMetadata)
    {
        private readonly List<string> _instantiation = [];
        private readonly MetadataReader? _outerMetadata = metadata;
        private readonly int _startOffset = offset;
        private int _offset = offset;
        private MetadataReader? _metadata = metadata;
        private string? _ownerDisplay;

        private int _topLevelModuleIndex = -1;
        private int _ownerModuleIndex = -1;

        public int Offset => _offset;
        public int MethodToken { get; private set; }
        public bool CrossModule { get; private set; }

        // The owning module: a top-level ModuleOverride, else the MODULE_ZAPSIG that wraps the owner
        // type (the composite form — the method token then resolves in that component, not the manifest).
        public int ModuleIndex => _topLevelModuleIndex >= 0 ? _topLevelModuleIndex : _ownerModuleIndex;

        // A method instantiation (Identity&lt;int&gt;) renders as its args; a method on an
        // instantiated generic type (Box&lt;int&gt;.Describe) renders the owning type instead.
        public string? RenderInstantiation() =>
            _instantiation.Count > 0 ? $"<{string.Join(", ", _instantiation)}>"
            : _ownerDisplay is { } owner && owner.Contains('<') ? $" ({owner})"
            : null;

        public void ParseMethod()
        {
            var flags = ReadUInt();
            ReadyToRunDiagnostics.Write($"method-start offset=0x{_startOffset:X} flags=0x{flags:X}");
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
                _ownerDisplay = SkipType();
                flags &= ~SigOwnerType;
            }

            if ((flags & SigSlotInsteadOfToken) != 0)
            {
                ReadUInt(); // a vtable slot rather than a token
                flags &= ~SigSlotInsteadOfToken;
            }
            else if ((flags & SigMemberRefToken) != 0)
            {
                MethodToken = 0x0A00_0000 | (int)ReadUInt();
                flags &= ~SigMemberRefToken;
            }
            else
            {
                MethodToken = 0x0600_0000 | (int)ReadUInt();
            }

            if ((flags & SigMethodInstantiation) != 0)
            {
                var argCount = ReadUInt();
                EnsureBounded(argCount, MaxSignatureItemCount, "method generic argument count");
                for (var i = 0; i < argCount; i++)
                    _instantiation.Add(SkipType());
                flags &= ~SigMethodInstantiation;
            }

            if ((flags & SigConstrained) != 0)
                SkipType();

            _ = SigUnboxingStub;
            _ = SigInstantiatingStub;
        }

        // Walks a type signature, advancing the offset and returning a best-effort display name.
        private string SkipType()
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
                    // The type (and the method whose owner it is) resolves in this module. The first
                    // one seen is the owner's; capture it so the method token attributes correctly.
                    var moduleIndex = (int)ReadUInt();
                    if (_ownerModuleIndex < 0) { _ownerModuleIndex = moduleIndex; CrossModule = true; }
                    ReadyToRunDiagnostics.Write(
                        $"type-module start=0x{_startOffset:X} module={moduleIndex} next=0x{_offset:X}");
                    return WithMetadata(moduleMetadata?.Invoke(moduleIndex), SkipType);
                }
                case 0x3b: ReadUInt(); return "var";                 // VAR_ZAPSIG
                case 0x3d: return SkipType();                        // NATIVE_VALUETYPE_ZAPSIG
                case 0x1f: case 0x20:                                // CMOD_REQD / CMOD_OPT
                    ReadToken();
                    return SkipType();
                case 0x14:                                           // ARRAY
                {
                    var element = SkipType();
                    var rank = ReadUInt();
                    EnsureBounded(rank, MaxArrayRank, "array rank");
                    if (rank == 0) return element + "[]";
                    var sizes = ReadUInt();
                    EnsureBounded(sizes, MaxArrayRank, "array size count");
                    for (var i = 0; i < sizes; i++) ReadUInt();
                    var lowerBounds = ReadUInt();
                    EnsureBounded(lowerBounds, MaxArrayRank, "array lower-bound count");
                    for (var i = 0; i < lowerBounds; i++) ReadInt();
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
                        for (var i = 0; i < argCount; i++) args[i] = SkipType();
                    });
                    return $"{generic}<{string.Join(", ", args)}>";
                }
                case 0x1b:                                           // FNPTR
                {
                    var header = reader.ReadByte(ref _offset);
                    if ((header & 0x10) != 0) ReadUInt(); // generic param count
                    var paramCount = ReadUInt();
                    EnsureBounded(paramCount, MaxSignatureItemCount, "function pointer parameter count");
                    SkipType(); // return
                    for (var i = 0; i < paramCount; i++)
                    {
                        while ((reader.PeekByte(_offset) & 0x7F) == 0x41) reader.ReadByte(ref _offset); // SENTINEL
                        SkipType();
                    }

                    return "delegate*";
                }
                default:
                    return "?";
            }
        }

        private string TypeTokenName(int token)
        {
            if (_metadata is null) return "Type";
            try
            {
                var handle = MetadataTokens.EntityHandle(token);
                var row = MetadataTokens.GetRowNumber(handle);
                ReadyToRunDiagnostics.Write(
                    $"type-token offset=0x{_offset:X} token=0x{token:X8} kind={handle.Kind} row={row}");
                return handle.Kind switch
                {
                    HandleKind.TypeDefinition when IsValidRow(row, _metadata.TypeDefinitions.Count) => _metadata.GetString(
                        _metadata.GetTypeDefinition((TypeDefinitionHandle)handle).Name),
                    HandleKind.TypeReference when IsValidRow(row, _metadata.TypeReferences.Count) => _metadata.GetString(
                        _metadata.GetTypeReference((TypeReferenceHandle)handle).Name),
                    _ => "Type",
                };
            }
            catch (Exception ex) when (ex is BadImageFormatException or ArgumentException or InvalidOperationException)
            {
                return "Type";
            }
        }

        private static void EnsureBounded(uint value, uint max, string name)
        {
            if (value > max)
                throw new BadImageFormatException($"ReadyToRun signature {name} {value} exceeds supported maximum {max}.");
        }

        private static bool IsValidRow(int row, int count) =>
            row > 0 && row <= count;

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
}
