using Dotsider.Core.Analysis.Models;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Dotsider.Core.Analysis.ReadyToRun;

/// <summary>
/// Parses a ReadyToRun image's <c>ImportSections</c> (section 101) into a map from each import
/// slot's virtual address to a display name. Precompiled code calls other methods and runtime
/// helpers <em>indirectly</em> through these slots, so an indirect <c>call [rip+disp]</c> resolves
/// to <c>Type.Method</c> or a helper name instead of a bare address. Each slot's signature is a
/// ReadyToRun fixup (readytorun.h): a fixup-kind byte (optionally module-override prefixed), then a
/// method signature, token, or helper id. Cross-module fixups degrade to a token label rather than
/// guessing. Never throws — an unreadable slot is simply left unnamed.
/// </summary>
internal sealed class ReadyToRunImportMap
{
    private readonly Dictionary<ulong, string> _byVirtualAddress;
    private readonly ulong _thunkStart;
    private readonly ulong _thunkEnd;

    private ReadyToRunImportMap(Dictionary<ulong, string> byVirtualAddress, ulong thunkStart, ulong thunkEnd)
    {
        _byVirtualAddress = byVirtualAddress;
        _thunkStart = thunkStart;
        _thunkEnd = thunkEnd;
    }

    /// <summary>The number of named import slots.</summary>
    public int Count => _byVirtualAddress.Count;

    /// <summary>Resolves an import slot's virtual address to its target name.</summary>
    public bool TryResolve(ulong virtualAddress, out NativeSymbolRef import)
    {
        if (_byVirtualAddress.TryGetValue(virtualAddress, out var name))
        {
            import = new NativeSymbolRef(virtualAddress, name, NativeSymbolKind.Stub, 0);
            return true;
        }

        // Section 106 holds the shared DelayLoad_MethodCall helper thunks — one per import section, not
        // one per method (the x64 stub pushes the import-section index and takes the cell from the
        // caller's register). The per-method target is the section-101 cell, named above; a reference
        // into 106 is the shared helper, so name it as such rather than as a bare address.
        if (_thunkEnd > _thunkStart && virtualAddress >= _thunkStart && virtualAddress < _thunkEnd)
        {
            import = new NativeSymbolRef(
                _thunkStart, "DelayLoad_MethodCall (helper)", NativeSymbolKind.Stub, (long)(virtualAddress - _thunkStart));
            return true;
        }

        import = default;
        return false;
    }

    private const int ImportSectionRecordSize = 20;

    // ReadyToRunFixupKind (readytorun.h).
    private const uint FixupModuleOverride = 0x80;
    private const uint FixupTypeHandle = 0x10;
    private const uint FixupMethodHandle = 0x11;
    private const uint FixupFieldHandle = 0x12;
    private const uint FixupMethodEntry = 0x13;
    private const uint FixupMethodEntryDefToken = 0x14;
    private const uint FixupMethodEntryRefToken = 0x15;
    private const uint FixupVirtualEntry = 0x16;
    private const uint FixupVirtualEntryDefToken = 0x17;
    private const uint FixupVirtualEntryRefToken = 0x18;
    private const uint FixupHelper = 0x1A;
    private const uint FixupStringHandle = 0x1B;
    private const uint FixupIndirectPInvokeTarget = 0x2E;
    private const uint FixupPInvokeTarget = 0x2F;

    /// <summary>Builds the import map for <paramref name="analyzer"/> (a ReadyToRun code image), or null.</summary>
    public static ReadyToRunImportMap? Build(AssemblyAnalyzer analyzer) =>
        Build(analyzer, components: null, providerFor: null);

    /// <summary>
    /// Builds the import map for a ReadyToRun code image with an optional caller-supplied component
    /// context. Component-opened composite disassembly uses this to resolve import names without
    /// forcing the owner composite to materialize every component method map.
    /// </summary>
    public static ReadyToRunImportMap? Build(
        AssemblyAnalyzer analyzer,
        IReadOnlyList<ReadyToRunComponent>? components,
        Func<Guid, AssemblyAnalyzer?>? providerFor)
    {
        // Only a Valid image's import sections are trusted to parse (a corrupt/unsupported header does
        // not vouch for the current layout).
        if (analyzer.ReadyToRunInfo is not { Status: ReadyToRunStatus.Valid } info)
            return null;
        ReadyToRunSectionEntry? section = null;
        foreach (var s in info.Sections)
            if (s.Type == (int)ReadyToRunSectionType.ImportSections)
            {
                section = s;
                break;
            }

        if (section is not { FileOffset: { } sectionOffset, Size: > 0 } sec)
            return null;

        var rawLength = analyzer.RawBytes.Length;
        if (sectionOffset < 0 ||
            sectionOffset > rawLength - ImportSectionRecordSize ||
            sec.Size > rawLength - sectionOffset ||
            sec.Size % ImportSectionRecordSize != 0)
        {
            return null;
        }

        var addressSpace = NativeAddressSpace.Create(analyzer.RawBytes.Span);
        if (addressSpace is null) return null;
        var imageBase = analyzer.PeHeaders?.ImageBase ?? 0;
        var pointerSize = info.Architecture is NativeArchitecture.X86 or NativeArchitecture.Arm32
            or NativeArchitecture.Wasm32 ? 4 : 8;

        // Cross-module fixups (composite) resolve their token against the owning component's metadata.
        Dictionary<Guid, AssemblyAnalyzer>? transientProviders = null;
        var moduleContext = components is { Count: > 0 }
            ? ReadyToRunModuleContext.Create(
                info, components, mvid => ResolveProvider(analyzer, components, providerFor, mvid, ref transientProviders))
            : ReadyToRunModuleContext.ForImage(analyzer);

        var map = new Dictionary<ulong, string>();
        try
        {
            var reader = new R2RNativeReader(analyzer.RawBytes);
            var metadata = analyzer.GetMetadataReader();
            var methodDefs = analyzer.MethodDefs;
            var end = sectionOffset + sec.Size;
            for (var record = sectionOffset; record + ImportSectionRecordSize <= end; record += ImportSectionRecordSize)
            {
                try
                {
                    ReadRecord(
                        reader, record, imageBase, addressSpace, pointerSize,
                        metadata, methodDefs, moduleContext, map);
                }
                catch (Exception exception) when (IsMalformedImportException(exception))
                {
                    ReadyToRunDiagnostics.Write(
                        $"import-record-rejected record=0x{record:X} "
                        + $"exception={exception.GetType().Name} message={exception.Message}");
                }
            }
        }
        finally
        {
            if (transientProviders is not null)
                foreach (var provider in transientProviders.Values)
                    provider.Dispose();
        }

        // The delay-load method-call thunk region (section 106) — named as a region in TryResolve.
        ulong thunkStart = 0, thunkEnd = 0;
        foreach (var s in info.Sections)
            if (s.Type == (int)ReadyToRunSectionType.DelayLoadMethodCallThunks && s.Size > 0)
            {
                thunkStart = imageBase + (uint)s.Rva;
                thunkEnd = thunkStart + (uint)s.Size;
            }

        return map.Count > 0 || thunkEnd > thunkStart
            ? new ReadyToRunImportMap(map, thunkStart, thunkEnd)
            : null;
    }

    private static AssemblyAnalyzer? ResolveProvider(
        AssemblyAnalyzer codeImage,
        IReadOnlyList<ReadyToRunComponent> components,
        Func<Guid, AssemblyAnalyzer?>? providerFor,
        Guid mvid,
        ref Dictionary<Guid, AssemblyAnalyzer>? transientProviders)
    {
        if (transientProviders is not null && transientProviders.TryGetValue(mvid, out var cached))
        {
            return cached;
        }

        // ReadyToRunMetadataProviderFor deliberately falls back to its owning analyzer when an MVID
        // is absent. That is useful to callers displaying incomplete models, but it is not a valid
        // token scope for a module override. Accept a supplied provider only when its module identity
        // actually matches; otherwise resolve the requested component beside the composite image.
        if (providerFor?.Invoke(mvid) is { } existing && HasModuleVersionId(existing, mvid))
        {
            return existing;
        }

        ReadyToRunComponent? component = null;
        foreach (var candidate in components)
            if (candidate.Mvid == mvid)
            {
                component = candidate;
                break;
            }

        if (component is null)
            return null;

        var directory = Path.GetDirectoryName(codeImage.FilePath) ?? ".";
        var opened = ReadyToRunComponentResolver.Resolve(directory, component.AssemblyName, component.Mvid);
        if (opened is null)
            return null;

        transientProviders ??= [];
        transientProviders[mvid] = opened;
        return opened;
    }

    private static bool HasModuleVersionId(AssemblyAnalyzer analyzer, Guid expected)
    {
        try
        {
            var reader = analyzer.GetMetadataReader();
            return reader is not null && reader.GetGuid(reader.GetModuleDefinition().Mvid) == expected;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }

    private static void ReadRecord(
        R2RNativeReader reader, int record, ulong imageBase, NativeAddressSpace addressSpace,
        int pointerSize, MetadataReader? metadata, IReadOnlyList<MethodDefInfo> methodDefs,
        ReadyToRunModuleContext? moduleContext, Dictionary<ulong, string> map)
    {
        var offset = record;
        var slotsRva = reader.ReadInt32(ref offset);
        var slotsSize = reader.ReadInt32(ref offset);
        offset += 2 + 1; // flags (u16) + type (u8)
        int entrySize = reader.ReadByte(ref offset);
        var signaturesRva = reader.ReadInt32(ref offset);
        if (entrySize == 0)
        {
            entrySize = pointerSize;
        }

        if (entrySize <= 0 || slotsSize <= 0 || slotsRva == 0 || signaturesRva == 0)
        {
            return;
        }

        if (!addressSpace.TryGetFileOffset(
                imageBase + (uint)slotsRva,
                out _,
                out var slotBytesAvailable) ||
            !addressSpace.TryGetFileOffset(
                imageBase + (uint)signaturesRva,
                out var signaturesOffset,
                out var signatureBytesAvailable) ||
            !TryGetSlotCount(
                slotsSize,
                entrySize,
                slotBytesAvailable,
                signatureBytesAvailable,
                out var count))
        {
            throw new BadImageFormatException(
                $"ReadyToRun import record at 0x{record:X} has inconsistent slot extents.");
        }

        for (var i = 0; i < count; i++)
        {
            try
            {
                var sigArrayEntry = signaturesOffset + i * 4;
                var sigRva = reader.ReadUInt32(ref sigArrayEntry);
                if (sigRva == 0
                    || !addressSpace.TryGetFileOffset(imageBase + sigRva, out var sigOffset, out _))
                {
                    continue;
                }

                var name = DecodeFixup(reader, sigOffset, metadata, methodDefs, moduleContext);
                if (name is not null)
                {
                    map[imageBase + (uint)slotsRva + (uint)(entrySize * i)] = name;
                }
            }
            catch (Exception exception) when (IsMalformedImportException(exception))
            {
                ReadyToRunDiagnostics.Write(
                    $"import-slot-rejected record=0x{record:X} slot={i} "
                    + $"exception={exception.GetType().Name} message={exception.Message}");
            }
        }
    }

    /// <summary>
    /// Validates the extents that govern an import record's slot and signature-table iteration.
    /// </summary>
    /// <param name="slotsSize">The byte size of the import-cell region.</param>
    /// <param name="entrySize">The size of one import cell.</param>
    /// <param name="slotBytesAvailable">Mapped bytes available from the first import cell.</param>
    /// <param name="signatureBytesAvailable">Mapped bytes available from the signature RVA table.</param>
    /// <param name="count">Receives the validated number of entries.</param>
    /// <returns><see langword="true"/> when all entries are fully backed by the image.</returns>
    internal static bool TryGetSlotCount(
        int slotsSize,
        int entrySize,
        int slotBytesAvailable,
        int signatureBytesAvailable,
        out int count)
    {
        count = 0;
        if (slotsSize <= 0 ||
            entrySize <= 0 ||
            slotBytesAvailable < slotsSize ||
            slotsSize % entrySize != 0)
        {
            return false;
        }

        count = slotsSize / entrySize;
        if (signatureBytesAvailable < 0 || count > signatureBytesAvailable / sizeof(uint))
        {
            count = 0;
            return false;
        }

        return true;
    }

    private static bool IsMalformedImportException(Exception exception) =>
        exception is BadImageFormatException or IndexOutOfRangeException or ArgumentOutOfRangeException;

    private static string? DecodeFixup(
        R2RNativeReader reader, int offset, MetadataReader? metadata, IReadOnlyList<MethodDefInfo> methodDefs,
        ReadyToRunModuleContext? moduleContext)
    {
        Func<int, MetadataReader?>? resolveMetadata =
            moduleContext is null ? null : moduleContext.ResolveMetadata;
        uint fixup = reader.ReadByte(ref offset);
        string? modulePrefix = null;
        if ((fixup & FixupModuleOverride) != 0)
        {
            var moduleIndex = (int)reader.ReadCompressedUInt(ref offset);
            fixup &= ~FixupModuleOverride;
            // Resolve the token against the owning component's metadata rather than leaving it unnamed.
            if (moduleContext?.Resolve(moduleIndex) is { } module)
            {
                modulePrefix = module.AssemblyName;
                metadata = module.Provider?.GetMetadataReader();
                methodDefs = module.Provider?.MethodDefs ?? [];
            }
            else
            {
                metadata = null;
                methodDefs = [];
                modulePrefix = "?";
            }
        }

        switch (fixup)
        {
            case FixupMethodEntry or FixupMethodHandle or FixupVirtualEntry:
                {
                    var sig = ReadyToRunSignatureWalker.ParseMethod(
                        reader, offset, metadata, resolveMetadata, moduleContext?.ResolveSystemMetadata());
                    return DecorateMethod(sig, metadata, methodDefs, modulePrefix);
                }

            case FixupMethodEntryDefToken or FixupVirtualEntryDefToken:
                return NameForToken(
                    ReadyToRunMethodToken.Create(
                        reader.ReadCompressedUInt(ref offset), HandleKind.MethodDefinition, metadata),
                    metadata,
                    methodDefs,
                    modulePrefix);

            case FixupMethodEntryRefToken or FixupVirtualEntryRefToken:
                return NameForToken(
                    ReadyToRunMethodToken.Create(
                        reader.ReadCompressedUInt(ref offset), HandleKind.MemberReference, metadata),
                    metadata,
                    methodDefs,
                    modulePrefix);

            case FixupPInvokeTarget or FixupIndirectPInvokeTarget:
                {
                    var sig = ReadyToRunSignatureWalker.ParseMethod(
                        reader, offset, metadata, resolveMetadata, moduleContext?.ResolveSystemMetadata());
                    var name = DecorateMethod(sig, metadata, methodDefs, modulePrefix);
                    return name is null ? null : $"{name} (pinvoke)";
                }

            case FixupHelper:
                return HelperName(reader.ReadCompressedUInt(ref offset));

            case FixupTypeHandle:
                return "typeHandle";
            case FixupFieldHandle:
                return "fieldHandle";
            case FixupStringHandle:
                return "string";
            default:
                return null; // not a useful call/data target to name
        }
    }

    private static string? DecorateMethod(
        ReadyToRunMethodSignature sig, MetadataReader? metadata,
        IReadOnlyList<MethodDefInfo> methodDefs, string? modulePrefix)
    {
        var name = NameForToken(sig.MethodToken, metadata, methodDefs, modulePrefix);
        return name is not null && sig.InstantiationDisplay is { } instantiation
            ? name + instantiation
            : name;
    }

    private static string? NameForToken(
        int token, MetadataReader? metadata, IReadOnlyList<MethodDefInfo> methodDefs, string? modulePrefix)
    {
        if (token is 0) return null;

        // A resolved name (Type.Method) already identifies the target; the reference-context module is
        // the caller, not the definer, so prefixing it would mislead. Prefix only an unresolved token,
        // where knowing which module it lives in is the only handle available.
        var name = ResolveTokenName(token, metadata, methodDefs);
        if (name is not null)
            return name;
        return modulePrefix is null ? $"token 0x{token:X8}" : $"{modulePrefix}!token 0x{token:X8}";
    }

    private static string? ResolveTokenName(
        int token, MetadataReader? metadata, IReadOnlyList<MethodDefInfo> methodDefs)
    {
        if ((token & 0xFF00_0000) == 0x0600_0000)
        {
            foreach (var m in methodDefs)
                if (m.Token == token)
                    return m.DeclaringType is { } dt ? $"{dt}.{m.Name}" : m.Name;
        }

        if ((token & 0xFF00_0000) == 0x0A00_0000 && metadata is not null)
        {
            try
            {
                var handle = (MemberReferenceHandle)MetadataTokens.EntityHandle(token);
                var member = metadata.GetMemberReference(handle);
                var name = metadata.GetString(member.Name);
                var parent = member.Parent;
                var type = parent.Kind switch
                {
                    HandleKind.TypeReference => metadata.GetString(
                        metadata.GetTypeReference((TypeReferenceHandle)parent).Name),
                    HandleKind.TypeDefinition => metadata.GetString(
                        metadata.GetTypeDefinition((TypeDefinitionHandle)parent).Name),
                    _ => null,
                };
                return type is not null ? $"{type}.{name}" : name;
            }
            catch (Exception ex) when (ex is BadImageFormatException or ArgumentException)
            {
                // fall through to the token label
            }
        }

        return null; // unresolved — the caller renders a token label
    }

    private static string HelperName(uint id) => id switch
    {
        0x08 => "DelayLoad_MethodCall",
        0x10 => "DelayLoad_Helper",
        0x11 => "DelayLoad_Helper_Obj",
        0x12 => "DelayLoad_Helper_ObjObj",
        0x20 => "Throw",
        0x21 => "Rethrow",
        0x22 => "Overflow",
        0x23 => "RngChkFail",
        0x24 => "FailFast",
        0x25 => "ThrowNullRef",
        0x26 => "ThrowDivZero",
        0x30 => "WriteBarrier",
        0x31 => "CheckedWriteBarrier",
        0x33 => "BulkWriteBarrier",
        0x38 => "Stelem_Ref",
        0x39 => "Ldelema_Ref",
        0x3E => "MemZero",
        0x3F => "MemSet",
        0x41 => "MemCpy",
        0x42 => "PInvokeBegin",
        0x43 => "PInvokeEnd",
        0x44 => "GCPoll",
        _ => $"helper#0x{id:X2}",
    };
}
