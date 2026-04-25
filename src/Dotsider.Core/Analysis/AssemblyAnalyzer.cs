using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Dotsider.Core.Analysis.Models;

namespace Dotsider.Core.Analysis;

/// <summary>
/// Core analyzer that reads a .NET assembly and extracts PE, metadata, IL, and string information.
/// Uses <see cref="PEReader"/> and <see cref="MetadataReader"/> from the BCL.
/// </summary>
public sealed class AssemblyAnalyzer : IDisposable
{
    private readonly Stream _stream;
    private readonly PEReader? _peReader;
    private readonly MetadataReader? _metadataReader;
    private readonly byte[] _rawBytes;
    private volatile bool _disposed;

    private IReadOnlyList<TypeDefInfo>? _typeDefs;
    private IReadOnlyList<MethodDefInfo>? _methodDefs;
    private IReadOnlyList<AssemblyRefInfo>? _assemblyRefs;
    private IReadOnlyList<TypeRefInfo>? _typeRefs;
    private IReadOnlyList<MemberRefInfo>? _memberRefs;
    private IReadOnlyList<FieldDefInfo>? _fieldDefs;
    private IReadOnlyList<CustomAttributeInfo>? _customAttributes;
    private IReadOnlyList<ResourceInfo>? _resources;
    private IReadOnlyList<SectionInfo>? _sections;
    private string? _preferredRuntimePack;

    /// <summary>
    /// Opens and analyzes the specified .NET assembly file.
    /// </summary>
    /// <param name="filePath">Absolute path to the assembly file.</param>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    public AssemblyAnalyzer(string filePath)
    {
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
        DisplayName = FileName;

        _rawBytes = File.ReadAllBytes(filePath);
        FileSize = _rawBytes.Length;

        var fileInfo = new FileInfo(filePath);
        LastModified = fileInfo.LastWriteTimeUtc;
        CreatedTime = fileInfo.CreationTimeUtc;
        IsReadOnly = fileInfo.IsReadOnly;

        _stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            _peReader = new PEReader(_stream);

            if (_peReader.HasMetadata)
            {
                _metadataReader = _peReader.GetMetadataReader();
                ReadAssemblyIdentity();
                ReadTargetFramework();
            }

            ReadPeHeaders();
            ReadClrHeader();
        }
        catch (BadImageFormatException) when (IsNativeExecutable(_rawBytes))
        {
            // Non-PE native binary (ELF or Mach-O on Linux/macOS), such as a
            // .NET apphost or NativeAOT output. Raw bytes are already loaded
            // for hex dump; PE-specific analysis will be empty.
            _peReader?.Dispose();
            _peReader = null;
        }
        catch
        {
            _peReader?.Dispose();
            _stream.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Creates an analyzer from raw bytes in memory. Used for bundle-extracted
    /// assemblies and as a last-resort fallback when disk I/O is unavailable
    /// after a save operation.
    /// </summary>
    /// <param name="bytes">The raw assembly bytes.</param>
    /// <param name="filePath">On-disk path for physical operations (tracing, save checks).</param>
    /// <param name="sourceBundlePath">
    /// If this assembly was extracted from a single-file bundle, the path to the bundle file.
    /// Used for assembly resolution context.
    /// </param>
    /// <param name="displayName">
    /// Logical name of the analyzed artifact for UI display (e.g. "SelfContainedConsole.dll"
    /// when the entry assembly is extracted from a bundle). If null, defaults to the file name
    /// portion of <paramref name="filePath"/>.
    /// </param>
    public AssemblyAnalyzer(byte[] bytes, string filePath, string? sourceBundlePath = null,
        string? displayName = null)
    {
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
        DisplayName = displayName ?? FileName;
        SourceBundlePath = sourceBundlePath;

        _rawBytes = bytes;
        FileSize = bytes.Length;

        LastModified = DateTime.UtcNow;
        CreatedTime = DateTime.UtcNow;

        _stream = new MemoryStream(bytes, writable: false);
        try
        {
            _peReader = new PEReader(_stream);

            if (_peReader.HasMetadata)
            {
                _metadataReader = _peReader.GetMetadataReader();
                ReadAssemblyIdentity();
                ReadTargetFramework();
            }

            ReadPeHeaders();
            ReadClrHeader();
        }
        catch (BadImageFormatException) when (IsNativeExecutable(_rawBytes))
        {
            _peReader?.Dispose();
            _peReader = null;
        }
        catch
        {
            _peReader?.Dispose();
            _stream.Dispose();
            throw;
        }
    }

    /// <summary>The full path to the analyzed assembly file.</summary>
    public string FilePath { get; }

    /// <summary>The file name without directory path.</summary>
    public string FileName { get; }

    /// <summary>
    /// Logical display name for the analyzed artifact. For bundle-backed analyzers this is
    /// the entry assembly file name (e.g. "SelfContainedConsole.dll") while <see cref="FilePath"/>
    /// points to the bundle executable on disk. For file-backed analyzers, equals <see cref="FileName"/>.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>The file size in bytes.</summary>
    public long FileSize { get; }

    /// <summary>The last modification time in UTC.</summary>
    public DateTime LastModified { get; }

    /// <summary>The creation time in UTC.</summary>
    public DateTime CreatedTime { get; }

    /// <summary>Whether the file is read-only on disk.</summary>
    public bool IsReadOnly { get; }

    /// <summary>The assembly simple name, or null if the file has no assembly manifest.</summary>
    public string? AssemblyName { get; private set; }

    /// <summary>The assembly version string, or null.</summary>
    public string? AssemblyVersion { get; private set; }

    /// <summary>The target framework moniker (e.g., ".NETCoreApp,Version=v10.0"), or null.</summary>
    public string? TargetFramework { get; private set; }

    /// <summary>The assembly culture, or null for culture-neutral assemblies.</summary>
    public string? Culture { get; private set; }

    /// <summary>The public key token as a hex string, or null.</summary>
    public string? PublicKeyToken { get; private set; }

    /// <summary>The PE architecture description (e.g., "AnyCPU", "x64", "ARM64").</summary>
    public string Architecture { get; private set; } = "Unknown";

    /// <summary>The parsed PE headers.</summary>
    public PeHeaders? PeHeaders { get; private set; }

    /// <summary>The parsed CLR header, or null if not a .NET assembly.</summary>
    public Models.ClrHeader? ClrHeader { get; private set; }

    /// <summary>Whether the PE file contains .NET metadata.</summary>
    public bool HasMetadata => _metadataReader is not null;

    /// <summary>
    /// If this assembly was loaded from a single-file bundle, the path to the bundle file.
    /// Used as resolution context when probing for referenced assemblies.
    /// </summary>
    public string? SourceBundlePath { get; }

    /// <summary>Whether this analyzer was created from bytes extracted from a single-file bundle.</summary>
    public bool IsBundleBacked => SourceBundlePath is not null;

    /// <summary>
    /// The path to launch when tracing this assembly. For bundle-backed analyzers this is
    /// the bundle executable; for file-backed analyzers this is <see cref="FilePath"/>.
    /// </summary>
    public string LaunchPath => SourceBundlePath ?? FilePath;

    /// <summary>
    /// Whether in-place hex save is supported. Returns <c>false</c> for bundle-backed analyzers
    /// because writing extracted entry bytes back over the bundle would corrupt it.
    /// </summary>
    public bool CanSaveInPlace => !IsBundleBacked;

    /// <summary>
    /// The preferred .NET runtime pack for this assembly, detected from its assembly references.
    /// Returns "Microsoft.WindowsDesktop.App" for WPF/WinForms assemblies,
    /// "Microsoft.AspNetCore.App" for ASP.NET Core assemblies,
    /// or "Microsoft.NETCore.App" otherwise.
    /// </summary>
    public string PreferredRuntimePack => _preferredRuntimePack ??= DetectRuntimePack();

    /// <summary>Gets the PE sections.</summary>
    public IReadOnlyList<SectionInfo> Sections
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _sections ??= ReadSections();
        }
    }

    /// <summary>Gets the TypeDef metadata table entries.</summary>
    public IReadOnlyList<TypeDefInfo> TypeDefs
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _typeDefs ??= ReadTypeDefs();
        }
    }

    /// <summary>Gets the MethodDef metadata table entries.</summary>
    public IReadOnlyList<MethodDefInfo> MethodDefs
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _methodDefs ??= ReadMethodDefs();
        }
    }

    /// <summary>Gets the AssemblyRef metadata table entries.</summary>
    public IReadOnlyList<AssemblyRefInfo> AssemblyRefs
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _assemblyRefs ??= ReadAssemblyRefs();
        }
    }

    /// <summary>Gets the TypeRef metadata table entries.</summary>
    public IReadOnlyList<TypeRefInfo> TypeRefs
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _typeRefs ??= ReadTypeRefs();
        }
    }

    /// <summary>Gets the MemberRef metadata table entries.</summary>
    public IReadOnlyList<MemberRefInfo> MemberRefs
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _memberRefs ??= ReadMemberRefs();
        }
    }

    /// <summary>Gets the FieldDef metadata table entries.</summary>
    public IReadOnlyList<FieldDefInfo> FieldDefs
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _fieldDefs ??= ReadFieldDefs();
        }
    }

    /// <summary>Gets the custom attributes applied to metadata entities.</summary>
    public IReadOnlyList<CustomAttributeInfo> CustomAttributes
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _customAttributes ??= ReadCustomAttributes();
        }
    }

    /// <summary>Gets the manifest resources defined in the assembly.</summary>
    public IReadOnlyList<ResourceInfo> Resources
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _resources ??= ReadResources();
        }
    }

    /// <summary>Gets the raw bytes of the file for hex editor display.</summary>
    public ReadOnlyMemory<byte> RawBytes => _rawBytes;

    /// <summary>
    /// Gets the method body bytes for IL disassembly.
    /// Returns null if the method has no IL body (abstract, extern, or native).
    /// </summary>
    /// <param name="method">The method definition to get the body for.</param>
    /// <returns>The method body block, or null.</returns>
    public MethodBodyBlock? GetMethodBody(MethodDefInfo method)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (method.Rva == 0 || _peReader is null) return null;
        return _peReader.GetMethodBody(method.Rva);
    }

    /// <summary>
    /// Gets the underlying <see cref="MetadataReader"/> for advanced queries.
    /// Returns null if the file has no .NET metadata.
    /// </summary>
    public MetadataReader? GetMetadataReader()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _metadataReader;
    }

    /// <summary>
    /// Resolves a metadata token to a human-readable name.
    /// </summary>
    /// <param name="token">The metadata token to resolve.</param>
    /// <returns>A display string for the token.</returns>
    public string ResolveToken(int token)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_metadataReader is null) return $"0x{token:X8}";

        try
        {
            var handle = MetadataTokens.EntityHandle(token);
            return handle.Kind switch
            {
                HandleKind.TypeReference => GetTypeRefName((TypeReferenceHandle)handle),
                HandleKind.TypeDefinition => GetTypeDefName((TypeDefinitionHandle)handle),
                HandleKind.MethodDefinition => GetMethodDefName((MethodDefinitionHandle)handle),
                HandleKind.MemberReference => GetMemberRefName((MemberReferenceHandle)handle),
                HandleKind.FieldDefinition => GetFieldDefName((FieldDefinitionHandle)handle),
                HandleKind.StandaloneSignature => $"StandaloneSig(0x{token:X8})",
                HandleKind.UserString => GetUserString(MetadataTokens.UserStringHandle(token & 0x00FFFFFF)),
                _ => $"0x{token:X8}"
            };
        }
        catch (Exception ex) when (ex is ArgumentException or BadImageFormatException)
        {
            return $"0x{token:X8}";
        }
    }

    /// <summary>
    /// Resolves a metadata token to a comparison-safe name that includes method/member
    /// signatures, handles MethodSpec/TypeSpec, decodes StandaloneSig blobs, and returns
    /// full untruncated user strings. Unlike <see cref="ResolveToken"/>, this produces
    /// names suitable for semantic cross-assembly comparison.
    /// </summary>
    /// <param name="token">The metadata token to resolve.</param>
    /// <returns>A comparison-safe string for the token.</returns>
    internal string ResolveTokenForComparison(int token)
    {
        if (_metadataReader is null) return $"0x{token:X8}";

        try
        {
            var handle = MetadataTokens.EntityHandle(token);
            return handle.Kind switch
            {
                HandleKind.TypeReference => GetTypeRefName((TypeReferenceHandle)handle),
                HandleKind.TypeDefinition => GetTypeDefName((TypeDefinitionHandle)handle),
                HandleKind.TypeSpecification => DecodeTypeSpec((TypeSpecificationHandle)handle),
                HandleKind.MethodDefinition => ResolveMethodDefForComparison((MethodDefinitionHandle)handle),
                HandleKind.MemberReference => ResolveMemberRefForComparison((MemberReferenceHandle)handle),
                HandleKind.MethodSpecification => ResolveMethodSpecForComparison((MethodSpecificationHandle)handle),
                HandleKind.FieldDefinition => GetFieldDefName((FieldDefinitionHandle)handle),
                HandleKind.StandaloneSignature => ResolveStandaloneSigForComparison((StandaloneSignatureHandle)handle),
                HandleKind.UserString => GetFullUserString(MetadataTokens.UserStringHandle(token & 0x00FFFFFF)),
                _ => $"0x{token:X8}"
            };
        }
        catch (Exception ex) when (ex is ArgumentException or BadImageFormatException)
        {
            return $"0x{token:X8}";
        }
    }

    private string ResolveMethodDefForComparison(MethodDefinitionHandle handle)
    {
        var md = _metadataReader!.GetMethodDefinition(handle);
        var typeName = GetTypeDefName(md.GetDeclaringType());
        var name = _metadataReader.GetString(md.Name);
        var sig = DecodeMethodSignature(md);
        return $"{typeName}::{name} {sig}";
    }

    private string ResolveMemberRefForComparison(MemberReferenceHandle handle)
    {
        var mr = _metadataReader!.GetMemberReference(handle);
        var name = _metadataReader.GetString(mr.Name);
        var parent = mr.Parent.Kind switch
        {
            HandleKind.TypeReference => GetTypeRefName((TypeReferenceHandle)mr.Parent),
            HandleKind.TypeDefinition => GetTypeDefName((TypeDefinitionHandle)mr.Parent),
            HandleKind.TypeSpecification => DecodeTypeSpec((TypeSpecificationHandle)mr.Parent),
            _ => "?"
        };

        try
        {
            var sig = mr.DecodeMethodSignature(new SignatureTypeProvider(), genericContext: default);
            var paramTypes = string.Join(", ", sig.ParameterTypes);
            return $"{parent}::{name} {sig.ReturnType}({paramTypes})";
        }
        catch
        {
            // Field reference — no method signature to decode
            return $"{parent}::{name}";
        }
    }

    private string ResolveMethodSpecForComparison(MethodSpecificationHandle handle)
    {
        var ms = _metadataReader!.GetMethodSpecification(handle);
        var baseMethod = ResolveTokenForComparison(MetadataTokens.GetToken(ms.Method));
        try
        {
            var typeArgs = ms.DecodeSignature(new SignatureTypeProvider(), genericContext: default);
            return $"{baseMethod}<{string.Join(", ", typeArgs)}>";
        }
        catch
        {
            return baseMethod;
        }
    }

    private string ResolveStandaloneSigForComparison(StandaloneSignatureHandle handle)
    {
        var sig = _metadataReader!.GetStandaloneSignature(handle);
        try
        {
            var methodSig = sig.DecodeMethodSignature(new SignatureTypeProvider(), genericContext: default);
            var paramTypes = string.Join(", ", methodSig.ParameterTypes);
            var conv = FormatCallingConvention(methodSig.Header);
            return $"method({conv}) {methodSig.ReturnType}({paramTypes})";
        }
        catch
        {
            try
            {
                var localTypes = sig.DecodeLocalSignature(new SignatureTypeProvider(), genericContext: default);
                return $"locals({string.Join(", ", localTypes)})";
            }
            catch
            {
                return $"StandaloneSig(0x{MetadataTokens.GetToken(handle):X8})";
            }
        }
    }

    private static string FormatCallingConvention(SignatureHeader header)
    {
        var conv = header.CallingConvention switch
        {
            SignatureCallingConvention.Default => "default",
            SignatureCallingConvention.CDecl => "cdecl",
            SignatureCallingConvention.StdCall => "stdcall",
            SignatureCallingConvention.ThisCall => "thiscall",
            SignatureCallingConvention.FastCall => "fastcall",
            SignatureCallingConvention.Unmanaged => "unmanaged",
            _ => $"0x{(byte)header.CallingConvention:X2}"
        };
        if (header.IsInstance) conv = "instance " + conv;
        if (header.HasExplicitThis) conv = "explicit " + conv;
        return conv;
    }

    private string GetFullUserString(UserStringHandle handle)
    {
        try
        {
            return $"\"{_metadataReader!.GetUserString(handle)}\"";
        }
        catch
        {
            return $"0x{MetadataTokens.GetToken(handle):X8}";
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposed = true;
        _peReader?.Dispose();
        _stream.Dispose();
    }

    /// <summary>
    /// Returns true if the raw bytes start with a recognized native executable
    /// magic (ELF or Mach-O). Used to distinguish legitimate non-PE binaries
    /// from corrupted or junk files.
    /// </summary>
    private static bool IsNativeExecutable(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 4) return false;

        // ELF: \x7fELF
        if (bytes[0] == 0x7F && bytes[1] == 0x45 && bytes[2] == 0x4C && bytes[3] == 0x46)
            return true;

        // Mach-O: four known magic values (big/little endian, 32/64-bit)
        uint magic = (uint)(bytes[0] << 24 | bytes[1] << 16 | bytes[2] << 8 | bytes[3]);
        return magic is 0xFEEDFACE or 0xFEEDFACF or 0xCEFAEDFE or 0xCFFAEDFE;
    }

    private void ReadAssemblyIdentity()
    {
        if (_metadataReader is null || !_metadataReader.IsAssembly) return;

        var asm = _metadataReader.GetAssemblyDefinition();
        AssemblyName = _metadataReader.GetString(asm.Name);
        AssemblyVersion = asm.Version.ToString();
        Culture = _metadataReader.GetString(asm.Culture);
        if (string.IsNullOrEmpty(Culture)) Culture = "neutral";

        var publicKey = _metadataReader.GetBlobBytes(asm.PublicKey);
        if (publicKey.Length > 0)
        {
            // Compute public key token (last 8 bytes of SHA1 hash, reversed)
            var hash = SHA1.HashData(publicKey);
            var tokenBytes = new byte[8];
            Array.Copy(hash, hash.Length - 8, tokenBytes, 0, 8);
            Array.Reverse(tokenBytes);
            PublicKeyToken = Convert.ToHexStringLower(tokenBytes);
        }
    }

    private void ReadTargetFramework()
    {
        if (_metadataReader is null) return;

        foreach (var attrHandle in _metadataReader.GetAssemblyDefinition().GetCustomAttributes())
        {
            var attr = _metadataReader.GetCustomAttribute(attrHandle);
            var ctorName = GetAttributeConstructorName(attr);
            if (ctorName?.Contains("TargetFrameworkAttribute") == true)
            {
                TargetFramework = DecodeAttributeString(attr);
                break;
            }
        }
    }

    private void ReadPeHeaders()
    {
        if (_peReader is null) return;
        var coffHeader = _peReader.PEHeaders.CoffHeader;
        var optionalHeader = _peReader.PEHeaders.PEHeader;

        if (optionalHeader is null) return;

        PeHeaders = new PeHeaders(
            Machine: coffHeader.Machine,
            Characteristics: coffHeader.Characteristics,
            TimeDateStamp: coffHeader.TimeDateStamp,
            Magic: optionalHeader.Magic,
            MajorLinkerVersion: optionalHeader.MajorLinkerVersion,
            MinorLinkerVersion: optionalHeader.MinorLinkerVersion,
            SizeOfCode: optionalHeader.SizeOfCode,
            EntryPointRva: optionalHeader.AddressOfEntryPoint,
            ImageBase: optionalHeader.ImageBase,
            SectionAlignment: optionalHeader.SectionAlignment,
            FileAlignment: optionalHeader.FileAlignment,
            SizeOfImage: optionalHeader.SizeOfImage,
            SizeOfHeaders: optionalHeader.SizeOfHeaders,
            Subsystem: optionalHeader.Subsystem,
            DllCharacteristics: optionalHeader.DllCharacteristics,
            NumberOfSections: _peReader.PEHeaders.SectionHeaders.Length);

        Architecture = (coffHeader.Machine, _peReader.PEHeaders.CorHeader?.Flags) switch
        {
            (Machine.Amd64, _) => "x64",
            (Machine.Arm64, _) => "ARM64",
            (Machine.Arm, _) => "ARM",
            (Machine.I386, var flags) when flags?.HasFlag(CorFlags.Requires32Bit) == true => "x86",
            (Machine.I386, var flags) when flags?.HasFlag(CorFlags.ILOnly) == true => "AnyCPU",
            (Machine.I386, _) => "AnyCPU (32-bit preferred)",
            _ => coffHeader.Machine.ToString()
        };
    }

    private void ReadClrHeader()
    {
        if (_peReader is null) return;
        var corHeader = _peReader.PEHeaders.CorHeader;
        if (corHeader is null) return;

        ClrHeader = new Models.ClrHeader(
            MajorRuntimeVersion: corHeader.MajorRuntimeVersion,
            MinorRuntimeVersion: corHeader.MinorRuntimeVersion,
            MetadataRva: corHeader.MetadataDirectory.RelativeVirtualAddress,
            MetadataSize: corHeader.MetadataDirectory.Size,
            Flags: corHeader.Flags,
            EntryPointToken: corHeader.EntryPointTokenOrRelativeVirtualAddress,
            ResourcesRva: corHeader.ResourcesDirectory.RelativeVirtualAddress,
            ResourcesSize: corHeader.ResourcesDirectory.Size,
            StrongNameSignatureRva: corHeader.StrongNameSignatureDirectory.RelativeVirtualAddress,
            StrongNameSignatureSize: corHeader.StrongNameSignatureDirectory.Size);
    }

    private List<SectionInfo> ReadSections()
    {
        if (_peReader is null) return [];
        return [.. _peReader.PEHeaders.SectionHeaders
            .Select(s => new SectionInfo(
                Name: s.Name,
                VirtualAddress: s.VirtualAddress,
                VirtualSize: s.VirtualSize,
                RawDataOffset: s.PointerToRawData,
                RawDataSize: s.SizeOfRawData,
                Characteristics: s.SectionCharacteristics))];
    }

    private List<TypeDefInfo> ReadTypeDefs()
    {
        if (_metadataReader is null) return [];

        var result = new List<TypeDefInfo>();
        foreach (var handle in _metadataReader.TypeDefinitions)
        {
            var td = _metadataReader.GetTypeDefinition(handle);
            var ns = _metadataReader.GetString(td.Namespace);
            var name = _metadataReader.GetString(td.Name);
            var fullName = GetTypeDefName(handle);

            string? baseType = null;
            if (!td.BaseType.IsNil)
            {
                baseType = td.BaseType.Kind switch
                {
                    HandleKind.TypeReference => GetTypeRefName((TypeReferenceHandle)td.BaseType),
                    HandleKind.TypeDefinition => GetTypeDefName((TypeDefinitionHandle)td.BaseType),
                    _ => $"0x{MetadataTokens.GetToken(td.BaseType):X8}"
                };
            }

            result.Add(new TypeDefInfo(
                Token: MetadataTokens.GetToken(handle),
                Namespace: ns,
                Name: name,
                FullName: fullName,
                Attributes: td.Attributes,
                BaseType: baseType,
                MethodCount: td.GetMethods().Count,
                FieldCount: td.GetFields().Count));
        }
        return result;
    }

    private List<MethodDefInfo> ReadMethodDefs()
    {
        if (_metadataReader is null) return [];

        var result = new List<MethodDefInfo>();
        foreach (var handle in _metadataReader.MethodDefinitions)
        {
            var md = _metadataReader.GetMethodDefinition(handle);
            var name = _metadataReader.GetString(md.Name);

            var declaringType = md.GetDeclaringType();
            var typeName = GetTypeDefName(declaringType);

            var signature = DecodeMethodSignature(md);

            result.Add(new MethodDefInfo(
                Token: MetadataTokens.GetToken(handle),
                DeclaringType: typeName,
                Name: name,
                Signature: signature,
                Attributes: md.Attributes,
                ImplAttributes: md.ImplAttributes,
                Rva: md.RelativeVirtualAddress));
        }

        return result;
    }

    private List<AssemblyRefInfo> ReadAssemblyRefs()
    {
        if (_metadataReader is null) return [];

        var result = new List<AssemblyRefInfo>();
        foreach (var handle in _metadataReader.AssemblyReferences)
        {
            var ar = _metadataReader.GetAssemblyReference(handle);
            var name = _metadataReader.GetString(ar.Name);
            var version = ar.Version.ToString();
            var culture = _metadataReader.GetString(ar.Culture);
            if (string.IsNullOrEmpty(culture)) culture = "neutral";

            string? publicKeyToken = null;
            var pkt = _metadataReader.GetBlobBytes(ar.PublicKeyOrToken);
            if (pkt.Length > 0)
            {
                publicKeyToken = Convert.ToHexStringLower(pkt);
            }

            result.Add(new AssemblyRefInfo(name, version, culture, publicKeyToken));
        }

        return result;
    }

    private List<TypeRefInfo> ReadTypeRefs()
    {
        if (_metadataReader is null) return [];

        var result = new List<TypeRefInfo>();
        foreach (var handle in _metadataReader.TypeReferences)
        {
            var tr = _metadataReader.GetTypeReference(handle);
            var ns = _metadataReader.GetString(tr.Namespace);
            var name = _metadataReader.GetString(tr.Name);
            var fullName = GetTypeRefName(handle);

            var scope = tr.ResolutionScope.Kind switch
            {
                HandleKind.AssemblyReference => _metadataReader.GetString(
                    _metadataReader.GetAssemblyReference((AssemblyReferenceHandle)tr.ResolutionScope).Name),
                HandleKind.TypeReference => GetTypeRefName((TypeReferenceHandle)tr.ResolutionScope),
                _ => tr.ResolutionScope.Kind.ToString()
            };

            var scopeId = ResolveScopeAssemblyIdentityId(tr.ResolutionScope);

            result.Add(new TypeRefInfo(
                MetadataTokens.GetToken(handle), ns, name, fullName, scope, scopeId));
        }

        return result;
    }

    private string ResolveScopeAssemblyIdentityId(EntityHandle scopeHandle)
    {
        if (_metadataReader is null) return string.Empty;

        var current = scopeHandle;
        while (current.Kind == HandleKind.TypeReference)
        {
            var parent = _metadataReader.GetTypeReference((TypeReferenceHandle)current);
            current = parent.ResolutionScope;
        }

        if (current.Kind != HandleKind.AssemblyReference)
            return string.Empty;

        var ar = _metadataReader.GetAssemblyReference((AssemblyReferenceHandle)current);
        var refName = _metadataReader.GetString(ar.Name);
        var refVersion = ar.Version.ToString();
        var refCulture = _metadataReader.GetString(ar.Culture);

        string? refPkt = null;
        var pktBytes = _metadataReader.GetBlobBytes(ar.PublicKeyOrToken);
        if (pktBytes.Length > 0)
        {
            refPkt = Convert.ToHexStringLower(pktBytes);
        }

        return AssemblyIdentityFormat.Format(refName, refVersion, refCulture, refPkt);
    }

    private List<MemberRefInfo> ReadMemberRefs()
    {
        if (_metadataReader is null) return [];

        var sigProvider = new SignatureTypeProvider();
        var result = new List<MemberRefInfo>();
        foreach (var handle in _metadataReader.MemberReferences)
        {
            var mr = _metadataReader.GetMemberReference(handle);
            var name = _metadataReader.GetString(mr.Name);
            var declaringType = mr.Parent.Kind switch
            {
                HandleKind.TypeReference => GetTypeRefName((TypeReferenceHandle)mr.Parent),
                HandleKind.TypeDefinition => GetTypeDefName((TypeDefinitionHandle)mr.Parent),
                _ => $"0x{MetadataTokens.GetToken(mr.Parent):X8}"
            };

            var kind = MemberRefKind.Method;
            var signature = "";
            try
            {
                var sigReader = _metadataReader.GetBlobReader(mr.Signature);
                var header = sigReader.ReadSignatureHeader();
                if (header.Kind == SignatureKind.Field)
                {
                    kind = MemberRefKind.Field;
                    signature = mr.DecodeFieldSignature(sigProvider, genericContext: default);
                }
                else
                {
                    var sig = mr.DecodeMethodSignature(sigProvider, genericContext: default);
                    signature = $"{sig.ReturnType}({string.Join(", ", sig.ParameterTypes)})";
                }
            }
            catch { /* exotic signatures */ }

            result.Add(new MemberRefInfo(
                MetadataTokens.GetToken(handle), declaringType, name, signature, kind));
        }

        return result;
    }

    private List<FieldDefInfo> ReadFieldDefs()
    {
        if (_metadataReader is null) return [];

        var sigProvider = new SignatureTypeProvider();
        var result = new List<FieldDefInfo>();
        foreach (var handle in _metadataReader.TypeDefinitions)
        {
            var typeName = GetTypeDefName(handle);
            var td = _metadataReader.GetTypeDefinition(handle);
            foreach (var fieldHandle in td.GetFields())
            {
                var fd = _metadataReader.GetFieldDefinition(fieldHandle);
                var name = _metadataReader.GetString(fd.Name);
                var fieldSig = "";
                try { fieldSig = fd.DecodeSignature(sigProvider, genericContext: default); }
                catch { /* signature decoding can fail */ }
                result.Add(new FieldDefInfo(
                    MetadataTokens.GetToken(fieldHandle), typeName, name, fd.Attributes, fieldSig));
            }
        }

        return result;
    }

    private List<CustomAttributeInfo> ReadCustomAttributes()
    {
        if (_metadataReader is null) return [];

        var result = new List<CustomAttributeInfo>();
        foreach (var handle in _metadataReader.CustomAttributes)
        {
            var attr = _metadataReader.GetCustomAttribute(handle);
            var parent = DescribeHandle(attr.Parent);
            var ctor = GetAttributeConstructorName(attr) ?? "Unknown";
            var value = DecodeAttributeString(attr);

            result.Add(new CustomAttributeInfo(parent, ctor, value));
        }

        return result;
    }

    private List<ResourceInfo> ReadResources()
    {
        if (_metadataReader is null) return [];

        var result = new List<ResourceInfo>();
        foreach (var handle in _metadataReader.ManifestResources)
        {
            var res = _metadataReader.GetManifestResource(handle);
            var name = _metadataReader.GetString(res.Name);
            var visibility = res.Attributes.HasFlag(ManifestResourceAttributes.Public) ? "Public" : "Private";
            var isLinked = !res.Implementation.IsNil;
            var offset = (int)res.Offset;

            long size = -1;
            if (!isLinked && ClrHeader is not null)
            {
                try
                {
                    var resourcesRva = ClrHeader.ResourcesRva;
                    var sectionData = _peReader!.GetSectionData(resourcesRva);
                    if (sectionData.Length > 0)
                    {
                        var reader = sectionData.GetReader();
                        reader.Offset += offset;
                        if (reader.RemainingBytes >= 4)
                        {
                            size = reader.ReadInt32();
                        }
                    }
                }
                catch
                {
                    // Size detection failed, leave as -1
                }
            }

            result.Add(new ResourceInfo(name, visibility, offset, size, isLinked));
        }

        return result;
    }

    private string GetTypeDefName(TypeDefinitionHandle handle)
    {
        if (_metadataReader is null) return handle.ToString()!;
        var td = _metadataReader.GetTypeDefinition(handle);
        var name = _metadataReader.GetString(td.Name);
        if (td.IsNested)
            return $"{GetTypeDefName(td.GetDeclaringType())}/{name}";
        var ns = _metadataReader.GetString(td.Namespace);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    private string GetTypeRefName(TypeReferenceHandle handle)
    {
        if (_metadataReader is null) return handle.ToString()!;
        var tr = _metadataReader.GetTypeReference(handle);
        var name = _metadataReader.GetString(tr.Name);
        if (tr.ResolutionScope.Kind == HandleKind.TypeReference)
            return $"{GetTypeRefName((TypeReferenceHandle)tr.ResolutionScope)}/{name}";
        var ns = _metadataReader.GetString(tr.Namespace);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    private string GetMethodDefName(MethodDefinitionHandle handle)
    {
        if (_metadataReader is null) return handle.ToString()!;
        var md = _metadataReader.GetMethodDefinition(handle);
        var typeName = GetTypeDefName(md.GetDeclaringType());
        var name = _metadataReader.GetString(md.Name);
        return $"{typeName}::{name}";
    }

    private string DecodeTypeSpec(TypeSpecificationHandle handle)
    {
        if (_metadataReader is null) return "TypeSpec";
        try
        {
            var ts = _metadataReader.GetTypeSpecification(handle);
            return ts.DecodeSignature(new SignatureTypeProvider(), genericContext: default);
        }
        catch { return "TypeSpec"; }
    }

    private string GetMemberRefName(MemberReferenceHandle handle)
    {
        if (_metadataReader is null) return handle.ToString()!;
        var mr = _metadataReader.GetMemberReference(handle);
        var name = _metadataReader.GetString(mr.Name);
        var parent = mr.Parent.Kind switch
        {
            HandleKind.TypeReference => GetTypeRefName((TypeReferenceHandle)mr.Parent),
            HandleKind.TypeDefinition => GetTypeDefName((TypeDefinitionHandle)mr.Parent),
            HandleKind.TypeSpecification => DecodeTypeSpec((TypeSpecificationHandle)mr.Parent),
            _ => "?"
        };
        return $"{parent}::{name}";
    }

    private string GetFieldDefName(FieldDefinitionHandle handle)
    {
        if (_metadataReader is null) return handle.ToString()!;
        var fd = _metadataReader.GetFieldDefinition(handle);
        var typeName = GetTypeDefName(fd.GetDeclaringType());
        var name = _metadataReader.GetString(fd.Name);
        return $"{typeName}::{name}";
    }

    private string GetUserString(UserStringHandle handle)
    {
        if (_metadataReader is null) return handle.ToString()!;
        var s = _metadataReader.GetUserString(handle);
        return s.Length > 50 ? $"\"{s[..50]}...\"" : $"\"{s}\"";
    }

    private static string DecodeMethodSignature(MethodDefinition md)
    {
        try
        {
            var sig = md.DecodeSignature(new SignatureTypeProvider(), genericContext: default);
            var paramTypes = string.Join(", ", sig.ParameterTypes);
            return $"{sig.ReturnType}({paramTypes})";
        }
        catch
        {
            return "(?)";
        }
    }

    private string? GetAttributeConstructorName(CustomAttribute attr)
    {
        try
        {
            return attr.Constructor.Kind switch
            {
                HandleKind.MethodDefinition => GetMethodDefName((MethodDefinitionHandle)attr.Constructor),
                HandleKind.MemberReference => GetMemberRefName((MemberReferenceHandle)attr.Constructor),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private string? DecodeAttributeString(CustomAttribute attr)
    {
        try
        {
            var value = _metadataReader!.GetBlobBytes(attr.Value);
            if (value.Length < 4) return null;
            // Custom attribute blob: prolog (2 bytes 0x0001) + fixed args + named args
            if (value[0] != 0x01 || value[1] != 0x00) return null;
            var offset = 2;
            // Try to read a SerString (PackedLen + UTF8 bytes)
            if (offset >= value.Length) return null;
            var firstByte = value[offset++];
            if (firstByte == 0xFF) return null; // null string
            var length = (int)firstByte;
            if (length > 127) return null; // Compressed integer - simplified handling
            if (offset + length > value.Length) return null;
            return System.Text.Encoding.UTF8.GetString(value, offset, length);
        }
        catch
        {
            return null;
        }
    }

    private string DescribeHandle(EntityHandle handle)
    {
        return handle.Kind switch
        {
            HandleKind.TypeDefinition => GetTypeDefName((TypeDefinitionHandle)handle),
            HandleKind.MethodDefinition => GetMethodDefName((MethodDefinitionHandle)handle),
            HandleKind.FieldDefinition => GetFieldDefName((FieldDefinitionHandle)handle),
            HandleKind.AssemblyDefinition => $"[assembly]",
            HandleKind.ModuleDefinition => $"[module]",
            _ => $"{handle.Kind}(0x{MetadataTokens.GetToken(handle):X8})"
        };
    }

    /// <summary>
    /// A minimal signature type provider that converts types to display strings.
    /// </summary>
    internal sealed class SignatureTypeProvider : ISignatureTypeProvider<string, object?>
    {
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
            _ => typeCode.ToString()
        };

        /// <inheritdoc/>
        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            var td = reader.GetTypeDefinition(handle);
            var name = reader.GetString(td.Name);
            if (td.IsNested)
                return $"{GetTypeFromDefinition(reader, td.GetDeclaringType(), 0)}/{name}";
            var ns = reader.GetString(td.Namespace);
            return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        }

        /// <inheritdoc/>
        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            var tr = reader.GetTypeReference(handle);
            var name = reader.GetString(tr.Name);
            if (tr.ResolutionScope.Kind == HandleKind.TypeReference)
                return $"{GetTypeFromReference(reader, (TypeReferenceHandle)tr.ResolutionScope, 0)}/{name}";
            var ns = reader.GetString(tr.Namespace);
            return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
        }

        /// <inheritdoc/>
        public string GetSZArrayType(string elementType) =>
            $"{elementType}[]";

        /// <inheritdoc/>
        public string GetArrayType(string elementType, ArrayShape shape) =>
            $"{elementType}[{new string(',', shape.Rank - 1)}]";

        /// <inheritdoc/>
        public string GetByReferenceType(string elementType) =>
            $"ref {elementType}";

        /// <inheritdoc/>
        public string GetPointerType(string elementType) =>
            $"{elementType}*";

        /// <inheritdoc/>
        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) =>
            $"{genericType}<{string.Join(", ", typeArguments)}>";

        /// <inheritdoc/>
        public string GetGenericMethodParameter(object? genericContext, int index) =>
            $"!!{index}";

        /// <inheritdoc/>
        public string GetGenericTypeParameter(object? genericContext, int index) =>
            $"!{index}";

        /// <inheritdoc/>
        public string GetPinnedType(string elementType) =>
            $"pinned {elementType}";

        /// <inheritdoc/>
        public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
        {
            try
            {
                return reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
            }
            catch
            {
                return "TypeSpec";
            }
        }

        /// <inheritdoc/>
        public string GetFunctionPointerType(MethodSignature<string> signature)
        {
            var conv = signature.Header.CallingConvention switch
            {
                SignatureCallingConvention.Default => "managed",
                SignatureCallingConvention.CDecl => "unmanaged[Cdecl]",
                SignatureCallingConvention.StdCall => "unmanaged[Stdcall]",
                SignatureCallingConvention.ThisCall => "unmanaged[Thiscall]",
                SignatureCallingConvention.FastCall => "unmanaged[Fastcall]",
                SignatureCallingConvention.Unmanaged => "unmanaged",
                _ => "managed"
            };
            var paramTypes = string.Join(", ", signature.ParameterTypes);
            return $"delegate* {conv} {signature.ReturnType}({paramTypes})";
        }

        /// <inheritdoc/>
        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) =>
            isRequired ? $"modreq({modifier}) {unmodifiedType}" : $"modopt({modifier}) {unmodifiedType}";
    }

    /// <summary>
    /// Resolves a referenced assembly name to a file on disk or bytes from a bundle.
    /// Probes: app-local, runtime directory, source bundle, host process bundle,
    /// adjacent bundles, and .NET shared framework.
    /// </summary>
    /// <param name="referencingAssemblyPath">Path of the assembly that references the target.</param>
    /// <param name="assemblyName">Assembly name without extension (e.g. "System.Runtime").</param>
    /// <param name="targetFramework">Target framework moniker for version-matched shared framework probing.</param>
    /// <param name="preferredRuntimePack">Preferred runtime pack to probe first (e.g. "Microsoft.AspNetCore.App").</param>
    /// <param name="sourceBundlePath">If the referencing assembly came from a bundle, the bundle path.</param>
    /// <returns>The resolved assembly, or <c>null</c> if not found.</returns>
    public static ResolvedAssembly? ResolveAssembly(
        string referencingAssemblyPath,
        string assemblyName,
        string? targetFramework = null,
        string? preferredRuntimePack = null,
        string? sourceBundlePath = null)
    {
        // For bundle-backed analyzers, referencingAssemblyPath is a virtual name —
        // use the bundle's directory for disk-based probing.
        var directory = sourceBundlePath is not null
            ? Path.GetDirectoryName(sourceBundlePath)!
            : Path.GetDirectoryName(referencingAssemblyPath)!;

        // 1. App-local directory
        var local = Path.Combine(directory, $"{assemblyName}.dll");
        if (File.Exists(local)) return new ResolvedAssembly.FromFile(local);

        local = Path.Combine(directory, $"{assemblyName}.exe");
        if (File.Exists(local)) return new ResolvedAssembly.FromFile(local);

        // 2. NuGet global packages folder via .deps.json — library projects do not copy
        // NuGet dependencies into bin, so deps.json is the authoritative mapping.
        var fromNuGet = NuGetDepsJsonResolver.TryResolve(referencingAssemblyPath, assemblyName);
        if (fromNuGet is not null) return fromNuGet;

        // 3. .NET runtime directory (BCL assemblies)
        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        var runtimeDll = Path.Combine(runtimeDir, $"{assemblyName}.dll");
        if (File.Exists(runtimeDll)) return new ResolvedAssembly.FromFile(runtimeDll);

        // 3. Source bundle — if the referencing assembly came from a bundle
        var fromSourceBundle = TryResolveFromBundle(sourceBundlePath, assemblyName);
        if (fromSourceBundle is not null) return fromSourceBundle;

        // 4. Host process bundle — if the current process is a single-file bundle
        var fromHostBundle = TryResolveFromBundle(Environment.ProcessPath, assemblyName);
        if (fromHostBundle is not null) return fromHostBundle;

        // 5. Adjacent bundles — scan same directory for other bundles
        var fromAdjacentBundle = TryResolveFromAdjacentBundles(directory, assemblyName);
        if (fromAdjacentBundle is not null) return fromAdjacentBundle;

        // 6. .NET shared framework discovery
        var sharedResult = DotNetRuntimeLocator.FindAssemblyInSharedFramework(
            assemblyName, targetFramework, preferredRuntimePack);
        if (sharedResult is not null) return new ResolvedAssembly.FromFile(sharedResult.Path);

        return null;
    }

    /// <summary>
    /// Backward-compatible wrapper that resolves to a file path only.
    /// Returns <c>null</c> for bundle-backed results.
    /// </summary>
    public static string? ResolveAssemblyPath(string referencingAssemblyPath, string assemblyName)
    {
        var resolved = ResolveAssembly(referencingAssemblyPath, assemblyName);
        return resolved is ResolvedAssembly.FromFile(var path) ? path : null;
    }

    /// <summary>
    /// Resolves a referenced assembly by full identity (name, version, culture, public key token).
    /// Probes every stage of <see cref="ResolveAssembly"/> and accepts only candidates whose
    /// manifest identity matches the requested identity exactly. If no probe produces a full
    /// match but at least one probe produces a simple-name match whose identity differs,
    /// returns <see cref="AssemblyProvenance.IdentityMismatch"/> with the path of that candidate —
    /// the graph does not expand from mismatched files.
    /// </summary>
    /// <param name="referencingAssemblyPath">Path of the assembly that references the target.</param>
    /// <param name="identity">The full identity the caller expects to resolve.</param>
    /// <param name="targetFramework">Target framework moniker for shared-framework probing.</param>
    /// <param name="preferredRuntimePack">Preferred runtime pack name.</param>
    /// <param name="sourceBundlePath">Bundle path, when the referencing assembly came from a bundle.</param>
    /// <param name="netFxBindingContext">
    /// Per-root .NET Framework binding context, or <see langword="null"/> for non-net48 roots.
    /// When supplied, the resolution routes through <see cref="NetFxBinder.Bind"/> instead of the
    /// .NET Core probe chain, faithfully modeling the CLR's framework unification + machine.config
    /// + publisher policy + app config + GAC + Framework[64] runtime + codeBase + appBase order.
    /// </param>
    /// <returns>
    /// An <see cref="AssemblyResolution"/> carrying the resolved assembly, provenance, optional
    /// candidate-probe path, and (for net48 roots) the applied policy and loaded identity.
    /// </returns>
    public static AssemblyResolution
        ResolveAssemblyByIdentity(
            string referencingAssemblyPath,
            AssemblyRefInfo identity,
            string? targetFramework = null,
            string? preferredRuntimePack = null,
            string? sourceBundlePath = null,
            NetFxBindingContext? netFxBindingContext = null)
    {
        if (netFxBindingContext is not null)
            return BindViaNetFxBinder(identity, netFxBindingContext);

        var directory = sourceBundlePath is not null
            ? Path.GetDirectoryName(sourceBundlePath)!
            : Path.GetDirectoryName(referencingAssemblyPath)!;

        string? mismatchPath = null;

        (ResolvedAssembly?, AssemblyProvenance)? TryFile(string path, AssemblyProvenance provenance)
        {
            if (!File.Exists(path)) return null;
            var actual = TryReadFileIdentity(path);
            if (actual is null) return null;
            if (IdentityEquals(identity, actual.Value))
                return (new ResolvedAssembly.FromFile(path), provenance);
            if (IsFrameworkRollForwardMatch(identity, actual.Value, provenance))
                return (new ResolvedAssembly.FromFile(path), provenance);
            mismatchPath ??= path;
            return null;
        }

        (ResolvedAssembly?, AssemblyProvenance)? TryBundle(
            ResolvedAssembly.FromBundle? candidate, AssemblyProvenance provenance)
        {
            if (candidate is null) return null;
            var actual = TryReadBundleIdentity(candidate.Bytes);
            if (actual is null) return null;
            if (IdentityEquals(identity, actual.Value))
                return (candidate, provenance);
            if (IsFrameworkRollForwardMatch(identity, actual.Value, provenance))
                return (candidate, provenance);
            mismatchPath ??= $"{candidate.BundlePath}:{candidate.Name}";
            return null;
        }

        (ResolvedAssembly?, AssemblyProvenance)? TryNuGet()
        {
            var resolved = NuGetDepsJsonResolver.TryResolve(referencingAssemblyPath, identity.Name);
            if (resolved is ResolvedAssembly.FromFile f)
                return TryFile(f.Path, AssemblyProvenance.NuGetPackageCache);
            return null;
        }

        var hit = TryFile(Path.Combine(directory, $"{identity.Name}.dll"), AssemblyProvenance.AppLocal)
                  ?? TryFile(Path.Combine(directory, $"{identity.Name}.exe"), AssemblyProvenance.AppLocal)
                  ?? TryNuGet()
                  ?? TryFile(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), $"{identity.Name}.dll"),
                             AssemblyProvenance.RuntimeDirectory)
                  ?? TryBundle(TryResolveFromBundle(sourceBundlePath, identity.Name),
                               AssemblyProvenance.SourceBundle)
                  ?? TryBundle(TryResolveFromBundle(Environment.ProcessPath, identity.Name),
                               AssemblyProvenance.HostBundle)
                  ?? TryBundle(TryResolveFromAdjacentBundles(directory, identity.Name),
                               AssemblyProvenance.AdjacentBundle);

        if (hit is null)
        {
            var shared = DotNetRuntimeLocator.FindAssemblyInSharedFramework(
                identity.Name, targetFramework, preferredRuntimePack);
            if (shared is not null)
                hit = TryFile(shared.Path, AssemblyProvenance.SharedFramework);
        }

        if (hit is { } h)
            return new AssemblyResolution(h.Item1, h.Item2, null);

        return mismatchPath is not null
            ? new AssemblyResolution(null, AssemblyProvenance.IdentityMismatch, mismatchPath)
            : new AssemblyResolution(null, AssemblyProvenance.Unresolved, null);
    }

    /// <summary>
    /// Routes an identity-based resolution through <see cref="NetFxBinder"/> for a .NET Framework
    /// root and adapts its <see cref="NetFxBindResult"/> into an <see cref="AssemblyResolution"/>.
    /// </summary>
    /// <param name="identity">The identity exactly as named by the metadata reference.</param>
    /// <param name="ctx">The binding context for the analyzed root.</param>
    /// <returns>The resolution.</returns>
    private static AssemblyResolution BindViaNetFxBinder(
        AssemblyRefInfo identity, NetFxBindingContext ctx)
    {
        var bind = NetFxBinder.Bind(identity, ctx);
        ResolvedAssembly? resolved = bind.LoadedPath is null ? null : new ResolvedAssembly.FromFile(bind.LoadedPath);
        var candidate = bind.Provenance switch
        {
            AssemblyProvenance.IdentityMismatch => bind.CandidateProbePath,
            AssemblyProvenance.CodeBaseMissing => bind.AppliedPolicy?.CodeBaseHref ?? bind.CandidateProbePath,
            _ => null,
        };
        return new AssemblyResolution(
            Resolved: resolved,
            Provenance: bind.Provenance,
            CandidateProbePath: candidate,
            AppliedPolicy: bind.AppliedPolicy,
            LoadedIdentity: bind.Loaded);
    }

    /// <summary>
    /// Classifies whether an assembly belongs to the .NET framework surface regardless of
    /// deployment model. Returns <see langword="true"/> when the node was located through the
    /// shared framework or runtime directory, or when its identity matches a well-known
    /// Microsoft framework public key token, or when the shared-framework locator recognizes
    /// its simple name for the supplied target framework. This classification is used by the
    /// TUI framework-filter toggle so framework assemblies shipped inside a self-contained
    /// publish or single-file bundle are filtered consistently with framework assemblies
    /// loaded from the shared runtime.
    /// </summary>
    /// <param name="provenance">How the node was located.</param>
    /// <param name="identity">The resolved assembly's identity.</param>
    /// <param name="targetFramework">The referencing assembly's target framework moniker.</param>
    /// <param name="preferredRuntimePack">The referencing assembly's preferred runtime pack.</param>
    /// <returns><see langword="true"/> if the node represents a framework assembly.</returns>
    public static bool IsFrameworkAssembly(
        AssemblyProvenance provenance,
        AssemblyRefInfo identity,
        string? targetFramework,
        string? preferredRuntimePack)
    {
        if (provenance is AssemblyProvenance.SharedFramework
                       or AssemblyProvenance.RuntimeDirectory
                       or AssemblyProvenance.FrameworkRuntimeDirectory)
            return true;

        // The GAC also hosts third-party strong-named libraries — filtering all GAC hits as
        // framework would hide user dependencies in the dep graph. Only treat a GAC node as
        // framework when its PKT matches a well-known Microsoft framework key.
        if (provenance is AssemblyProvenance.Gac
            && identity.PublicKeyToken is string gacPkt
            && WellKnownFrameworkPublicKeyTokens.Contains(gacPkt))
            return true;

        if (identity.PublicKeyToken is string pkt && WellKnownFrameworkPublicKeyTokens.Contains(pkt))
            return true;

        var shared = DotNetRuntimeLocator.FindAssemblyInSharedFramework(
            identity.Name, targetFramework, preferredRuntimePack);
        return shared is not null;
    }

    /// <summary>
    /// Public key tokens that mark an assembly as a Microsoft framework or NuGet-shim assembly.
    /// Used by <see cref="IsFrameworkAssembly"/> for the dep-graph framework-filter toggle so
    /// BCL assemblies and the System.* / Microsoft.Extensions.* compatibility-pack shims are
    /// hidden together. Broader than the unification set on purpose.
    /// </summary>
    internal static readonly HashSet<string> WellKnownFrameworkPublicKeyTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "b77a5c561934e089",
        "b03f5f7f11d50a3a",
        "31bf3856ad364e35",
        "7cec85d7bea7798e",
        "cc7b13ffcd2ddd51",
        "adb9793829ddae60",
    };

    /// <summary>
    /// Public key tokens whose assemblies the .NET Framework unification table covers — the
    /// in-box BCL and Microsoft tooling keys. The compatibility-pack tokens
    /// <c>cc7b13ffcd2ddd51</c> (System.Memory family) and <c>adb9793829ddae60</c>
    /// (Microsoft.Extensions.*) are deliberately excluded: the CLR does not unify those, so
    /// references like <c>System.ValueTuple, Version=4.1.0.0</c> against the in-box
    /// <c>4.0.0.0</c> file must still fail without an explicit binding redirect.
    /// </summary>
    internal static readonly HashSet<string> FrameworkUnificationPublicKeyTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "b77a5c561934e089",
        "b03f5f7f11d50a3a",
        "31bf3856ad364e35",
        "7cec85d7bea7798e",
    };

    private static (string Name, string Version, string Culture, string? PublicKeyToken)? TryReadFileIdentity(string path)
    {
        try
        {
            using var analyzer = new AssemblyAnalyzer(path);
            if (!analyzer.HasMetadata || analyzer.AssemblyName is null)
                return null;
            return (analyzer.AssemblyName,
                    analyzer.AssemblyVersion ?? string.Empty,
                    analyzer.Culture ?? "neutral",
                    analyzer.PublicKeyToken);
        }
        catch
        {
            return null;
        }
    }

    private static (string Name, string Version, string Culture, string? PublicKeyToken)? TryReadBundleIdentity(byte[] bytes)
    {
        try
        {
            using var analyzer = new AssemblyAnalyzer(bytes, filePath: "<bundle>");
            if (!analyzer.HasMetadata || analyzer.AssemblyName is null)
                return null;
            return (analyzer.AssemblyName,
                    analyzer.AssemblyVersion ?? string.Empty,
                    analyzer.Culture ?? "neutral",
                    analyzer.PublicKeyToken);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Whether a probe candidate qualifies as a .NET host-style framework roll-forward of the
    /// requested reference. Matches the binding behavior the .NET host applies when a binary
    /// compiled against an older framework version runs against a newer shared framework:
    /// if the candidate came from the shared framework, runtime directory, or NuGet package
    /// cache, shares the simple name and public key token, and carries a well-known Microsoft
    /// framework public key token, accept the version difference. Without this, every
    /// framework-referencing package (net6-targeted third-party libraries running on net10)
    /// would fill the graph with identity-mismatched leaves even though the .NET host itself
    /// binds them happily.
    /// </summary>
    private static bool IsFrameworkRollForwardMatch(
        AssemblyRefInfo requested,
        (string Name, string Version, string Culture, string? PublicKeyToken) actual,
        AssemblyProvenance provenance)
    {
        if (provenance is not (AssemblyProvenance.SharedFramework
                              or AssemblyProvenance.RuntimeDirectory
                              or AssemblyProvenance.NuGetPackageCache))
            return false;
        if (!string.Equals(requested.Name, actual.Name, StringComparison.OrdinalIgnoreCase))
            return false;
        if (string.IsNullOrEmpty(requested.PublicKeyToken) || string.IsNullOrEmpty(actual.PublicKeyToken))
            return false;
        if (!string.Equals(requested.PublicKeyToken, actual.PublicKeyToken, StringComparison.OrdinalIgnoreCase))
            return false;
        return WellKnownFrameworkPublicKeyTokens.Contains(requested.PublicKeyToken);
    }

    private static bool IdentityEquals(
        AssemblyRefInfo requested,
        (string Name, string Version, string Culture, string? PublicKeyToken) actual)
    {
        if (!string.Equals(requested.Name, actual.Name, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!string.Equals(requested.Version, actual.Version, StringComparison.Ordinal))
            return false;

        var requestedCulture = string.IsNullOrEmpty(requested.Culture) ? "neutral" : requested.Culture;
        var actualCulture = string.IsNullOrEmpty(actual.Culture) ? "neutral" : actual.Culture;
        if (!string.Equals(requestedCulture, actualCulture, StringComparison.OrdinalIgnoreCase))
            return false;

        var requestedPkt = requested.PublicKeyToken ?? string.Empty;
        var actualPkt = actual.PublicKeyToken ?? string.Empty;
        return string.Equals(requestedPkt, actualPkt, StringComparison.OrdinalIgnoreCase);
    }

    private static ResolvedAssembly.FromBundle? TryResolveFromBundle(string? bundlePath, string assemblyName)
    {
        if (bundlePath is null)
            return null;

        if (!SingleFileBundleReader.IsBundle(bundlePath, out var headerOffset))
            return null;

        try
        {
            var manifest = SingleFileBundleReader.ReadManifest(bundlePath, headerOffset);
            var bytes = SingleFileBundleReader.ReadAssembly(bundlePath, manifest, assemblyName);
            if (bytes is not null)
                return new ResolvedAssembly.FromBundle(bytes, $"{assemblyName}.dll", bundlePath);
        }
        catch
        {
            // Bundle not readable
        }

        return null;
    }

    private static ResolvedAssembly.FromBundle? TryResolveFromAdjacentBundles(
        string directory, string assemblyName)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(directory))
            {
                // Skip files we've already checked (source bundle, host process)
                if (string.Equals(file, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                // Only check executable-looking files (no extension or .exe)
                var ext = Path.GetExtension(file);
                if (ext.Length > 0 && !ext.Equals(".exe", StringComparison.OrdinalIgnoreCase))
                    continue;

                var result = TryResolveFromBundle(file, assemblyName);
                if (result is not null)
                    return result;
            }
        }
        catch
        {
            // Directory not accessible
        }

        return null;
    }

    private string DetectRuntimePack()
    {
        if (_metadataReader is null)
            return "Microsoft.NETCore.App";

        foreach (var h in _metadataReader.AssemblyReferences)
        {
            var r = _metadataReader.GetAssemblyReference(h);
            var name = _metadataReader.GetString(r.Name);

            if (name is "WindowsBase" or "PresentationFramework" or "PresentationCore")
                return "Microsoft.WindowsDesktop.App";

            if (name.StartsWith("Microsoft.AspNetCore.", StringComparison.Ordinal))
                return "Microsoft.AspNetCore.App";
        }

        return "Microsoft.NETCore.App";
    }
}
