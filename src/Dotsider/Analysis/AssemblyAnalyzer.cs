using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Dotsider.Analysis.Models;

namespace Dotsider.Analysis;

/// <summary>
/// Core analyzer that reads a .NET assembly and extracts PE, metadata, IL, and string information.
/// Uses <see cref="PEReader"/> and <see cref="MetadataReader"/> from the BCL.
/// </summary>
public sealed class AssemblyAnalyzer : IDisposable
{
    private readonly FileStream _stream;
    private readonly PEReader _peReader;
    private readonly MetadataReader? _metadataReader;
    private readonly byte[] _rawBytes;

    private IReadOnlyList<TypeDefInfo>? _typeDefs;
    private IReadOnlyList<MethodDefInfo>? _methodDefs;
    private IReadOnlyList<AssemblyRefInfo>? _assemblyRefs;
    private IReadOnlyList<TypeRefInfo>? _typeRefs;
    private IReadOnlyList<MemberRefInfo>? _memberRefs;
    private IReadOnlyList<CustomAttributeInfo>? _customAttributes;
    private IReadOnlyList<ResourceInfo>? _resources;
    private IReadOnlyList<SectionInfo>? _sections;

    /// <summary>
    /// Opens and analyzes the specified .NET assembly file.
    /// </summary>
    /// <param name="filePath">Absolute path to the assembly file.</param>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="BadImageFormatException">The file is not a valid PE image.</exception>
    public AssemblyAnalyzer(string filePath)
    {
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);

        _rawBytes = File.ReadAllBytes(filePath);
        FileSize = _rawBytes.Length;

        var fileInfo = new FileInfo(filePath);
        LastModified = fileInfo.LastWriteTimeUtc;
        CreatedTime = fileInfo.CreationTimeUtc;
        IsReadOnly = fileInfo.IsReadOnly;

        _stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
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

    /// <summary>The full path to the analyzed assembly file.</summary>
    public string FilePath { get; }

    /// <summary>The file name without directory path.</summary>
    public string FileName { get; }

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

    /// <summary>Gets the PE sections.</summary>
    public IReadOnlyList<SectionInfo> Sections => _sections ??= ReadSections();

    /// <summary>Gets the TypeDef metadata table entries.</summary>
    public IReadOnlyList<TypeDefInfo> TypeDefs => _typeDefs ??= ReadTypeDefs();

    /// <summary>Gets the MethodDef metadata table entries.</summary>
    public IReadOnlyList<MethodDefInfo> MethodDefs => _methodDefs ??= ReadMethodDefs();

    /// <summary>Gets the AssemblyRef metadata table entries.</summary>
    public IReadOnlyList<AssemblyRefInfo> AssemblyRefs => _assemblyRefs ??= ReadAssemblyRefs();

    /// <summary>Gets the TypeRef metadata table entries.</summary>
    public IReadOnlyList<TypeRefInfo> TypeRefs => _typeRefs ??= ReadTypeRefs();

    /// <summary>Gets the MemberRef metadata table entries.</summary>
    public IReadOnlyList<MemberRefInfo> MemberRefs => _memberRefs ??= ReadMemberRefs();

    /// <summary>Gets the custom attributes applied to metadata entities.</summary>
    public IReadOnlyList<CustomAttributeInfo> CustomAttributes => _customAttributes ??= ReadCustomAttributes();

    /// <summary>Gets the manifest resources defined in the assembly.</summary>
    public IReadOnlyList<ResourceInfo> Resources => _resources ??= ReadResources();

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
        if (method.Rva == 0) return null;
        return _peReader.GetMethodBody(method.Rva);
    }

    /// <summary>
    /// Gets the underlying <see cref="MetadataReader"/> for advanced queries.
    /// Returns null if the file has no .NET metadata.
    /// </summary>
    public MetadataReader? GetMetadataReader() => _metadataReader;

    /// <summary>
    /// Resolves a metadata token to a human-readable name.
    /// </summary>
    /// <param name="token">The metadata token to resolve.</param>
    /// <returns>A display string for the token.</returns>
    public string ResolveToken(int token)
    {
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
        catch (ArgumentException)
        {
            return $"0x{token:X8}";
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _peReader.Dispose();
        _stream.Dispose();
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
            var hash = System.Security.Cryptography.SHA1.HashData(publicKey);
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

    private IReadOnlyList<SectionInfo> ReadSections()
    {
        return _peReader.PEHeaders.SectionHeaders
            .Select(s => new SectionInfo(
                Name: s.Name,
                VirtualAddress: s.VirtualAddress,
                VirtualSize: s.VirtualSize,
                RawDataOffset: s.PointerToRawData,
                RawDataSize: s.SizeOfRawData,
                Characteristics: s.SectionCharacteristics))
            .ToList();
    }

    private IReadOnlyList<TypeDefInfo> ReadTypeDefs()
    {
        if (_metadataReader is null) return [];

        var result = new List<TypeDefInfo>();
        foreach (var handle in _metadataReader.TypeDefinitions)
        {
            var td = _metadataReader.GetTypeDefinition(handle);
            var ns = _metadataReader.GetString(td.Namespace);
            var name = _metadataReader.GetString(td.Name);
            var fullName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";

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

    private IReadOnlyList<MethodDefInfo> ReadMethodDefs()
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

    private IReadOnlyList<AssemblyRefInfo> ReadAssemblyRefs()
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

    private IReadOnlyList<TypeRefInfo> ReadTypeRefs()
    {
        if (_metadataReader is null) return [];

        var result = new List<TypeRefInfo>();
        foreach (var handle in _metadataReader.TypeReferences)
        {
            var tr = _metadataReader.GetTypeReference(handle);
            var ns = _metadataReader.GetString(tr.Namespace);
            var name = _metadataReader.GetString(tr.Name);
            var fullName = string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";

            var scope = tr.ResolutionScope.Kind switch
            {
                HandleKind.AssemblyReference => _metadataReader.GetString(
                    _metadataReader.GetAssemblyReference((AssemblyReferenceHandle)tr.ResolutionScope).Name),
                HandleKind.TypeReference => GetTypeRefName((TypeReferenceHandle)tr.ResolutionScope),
                _ => tr.ResolutionScope.Kind.ToString()
            };

            result.Add(new TypeRefInfo(
                MetadataTokens.GetToken(handle), ns, name, fullName, scope));
        }
        return result;
    }

    private IReadOnlyList<MemberRefInfo> ReadMemberRefs()
    {
        if (_metadataReader is null) return [];

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

            result.Add(new MemberRefInfo(
                MetadataTokens.GetToken(handle), declaringType, name, ""));
        }
        return result;
    }

    private IReadOnlyList<CustomAttributeInfo> ReadCustomAttributes()
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

    private IReadOnlyList<ResourceInfo> ReadResources()
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
                    var sectionData = _peReader.GetSectionData(resourcesRva);
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
        var ns = _metadataReader.GetString(td.Namespace);
        var name = _metadataReader.GetString(td.Name);
        return string.IsNullOrEmpty(ns) ? name : $"{ns}.{name}";
    }

    private string GetTypeRefName(TypeReferenceHandle handle)
    {
        if (_metadataReader is null) return handle.ToString()!;
        var tr = _metadataReader.GetTypeReference(handle);
        var ns = _metadataReader.GetString(tr.Namespace);
        var name = _metadataReader.GetString(tr.Name);
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

    private string GetMemberRefName(MemberReferenceHandle handle)
    {
        if (_metadataReader is null) return handle.ToString()!;
        var mr = _metadataReader.GetMemberReference(handle);
        var name = _metadataReader.GetString(mr.Name);
        var parent = mr.Parent.Kind switch
        {
            HandleKind.TypeReference => GetTypeRefName((TypeReferenceHandle)mr.Parent),
            HandleKind.TypeDefinition => GetTypeDefName((TypeDefinitionHandle)mr.Parent),
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

    private string DecodeMethodSignature(MethodDefinition md)
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
    private sealed class SignatureTypeProvider : ISignatureTypeProvider<string, object?>
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
            return reader.GetString(td.Name);
        }

        /// <inheritdoc/>
        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
        {
            var tr = reader.GetTypeReference(handle);
            return reader.GetString(tr.Name);
        }

        /// <inheritdoc/>
        public string GetSZArrayType(string elementType) => $"{elementType}[]";
        /// <inheritdoc/>
        public string GetArrayType(string elementType, ArrayShape shape) => $"{elementType}[{new string(',', shape.Rank - 1)}]";
        /// <inheritdoc/>
        public string GetByReferenceType(string elementType) => $"ref {elementType}";
        /// <inheritdoc/>
        public string GetPointerType(string elementType) => $"{elementType}*";
        /// <inheritdoc/>
        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) =>
            $"{genericType}<{string.Join(", ", typeArguments)}>";
        /// <inheritdoc/>
        public string GetGenericMethodParameter(object? genericContext, int index) => $"!!{index}";
        /// <inheritdoc/>
        public string GetGenericTypeParameter(object? genericContext, int index) => $"!{index}";
        /// <inheritdoc/>
        public string GetPinnedType(string elementType) => $"pinned {elementType}";
        /// <inheritdoc/>
        public string GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind) => "TypeSpec";
        /// <inheritdoc/>
        public string GetFunctionPointerType(MethodSignature<string> signature) => "delegate*";
        /// <inheritdoc/>
        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;
    }

    /// <summary>
    /// Attempts to resolve a referenced assembly name to a file path on disk.
    /// Searches the same directory as the referencing assembly, then .NET runtime dirs.
    /// </summary>
    public static string? ResolveAssemblyPath(string referencingAssemblyPath, string assemblyName)
    {
        var directory = Path.GetDirectoryName(referencingAssemblyPath)!;

        // Same directory (app-local deps)
        var local = Path.Combine(directory, $"{assemblyName}.dll");
        if (File.Exists(local)) return local;

        local = Path.Combine(directory, $"{assemblyName}.exe");
        if (File.Exists(local)) return local;

        // .NET runtime directory (BCL assemblies)
        var coreLocation = typeof(object).Assembly.Location;
        var runtimeDir = string.IsNullOrEmpty(coreLocation)
            ? AppContext.BaseDirectory
            : Path.GetDirectoryName(coreLocation);
        if (runtimeDir is not null)
        {
            var runtimeDll = Path.Combine(runtimeDir, $"{assemblyName}.dll");
            if (File.Exists(runtimeDll)) return runtimeDll;
        }

        return null;
    }
}
