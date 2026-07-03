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
    private MetadataReaderProvider? _pdbReaderProvider;
    private MetadataReader? _pdbReader;
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
    private IReadOnlyList<DebugDirectoryInfo>? _debugDirectory;
    private SourceLinkInfo? _sourceLink;
    private string? _preferredRuntimePack;
    private NativeAotInfo? _nativeAotInfo;
    private bool _nativeAotProbed;
    private IReadOnlyList<ImportedModuleInfo>? _imports;
    private IReadOnlyList<ExportedFunctionInfo>? _exports;
    private LoadConfigInfo? _loadConfig;
    private bool _loadConfigProbed;
    private NativeAddressSpace? _addressSpace;
    private bool _addressSpaceProbed;
    private IReadOnlyList<RtrSection>? _readyToRunSections;
    private IReadOnlyList<StringEntry>? _frozenStrings;
    private IReadOnlyList<RecoveredType>? _recoveredTypes;
    private MstatData? _mstat;
    private bool _mstatProbed;
    private DgmlGraph? _dgml;
    private bool _dgmlProbed;

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
            ReadDebugInformation();
        }
        catch (BadImageFormatException) when (IsNativeExecutable(_rawBytes))
        {
            // Non-PE native binary (ELF or Mach-O on Linux/macOS), such as a
            // .NET apphost or NativeAOT output. Raw bytes are already loaded
            // for hex dump; PE-specific analysis will be empty.
            _peReader?.Dispose();
            _peReader = null;
            Architecture = GetNativeArchitecture(_rawBytes);
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
            ReadDebugInformation();
        }
        catch (BadImageFormatException) when (IsNativeExecutable(_rawBytes))
        {
            _peReader?.Dispose();
            _peReader = null;
            Architecture = GetNativeArchitecture(_rawBytes);
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
    /// Facts from the embedded ReadyToRun header when this is a Native AOT binary,
    /// or null. Only probed for metadata-less files — a managed ReadyToRun assembly
    /// also embeds the header, but there it accompanies metadata rather than
    /// replacing it.
    /// </summary>
    public NativeAotInfo? NativeAotInfo
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_nativeAotProbed)
            {
                _nativeAotInfo = HasMetadata ? null : NativeAotDetector.Detect(_rawBytes);
                _nativeAotProbed = true;
            }

            return _nativeAotInfo;
        }
    }

    /// <summary>Coarse classification of the analyzed binary.</summary>
    public BinaryKind BinaryKind =>
        HasMetadata ? BinaryKind.Managed
        : NativeAotInfo is not null ? BinaryKind.NativeAot
        : BinaryKind.Native;

    /// <summary>
    /// The ReadyToRun section table of a Native AOT binary, or an empty list when this is
    /// not a Native AOT binary or the table cannot be parsed.
    /// </summary>
    public IReadOnlyList<RtrSection> ReadyToRunSections
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_readyToRunSections is not null) return _readyToRunSections;

            if (NativeAotInfo is { } info && AddressSpace is { } space)
                _readyToRunSections = ReadyToRunReader.ReadSections(_rawBytes, info, space);
            else
                _readyToRunSections = [];

            return _readyToRunSections;
        }
    }

    /// <summary>
    /// Frozen <see cref="string"/> literals recovered from a Native AOT binary's frozen
    /// object region — the AOT counterpart of the #US heap. Empty when this is not a
    /// Native AOT binary, or on Linux where the region is filled at startup and has no
    /// file backing (the raw UTF-16 scan surfaces that text instead).
    /// </summary>
    public IReadOnlyList<StringEntry> FrozenStrings
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_frozenStrings is not null) return _frozenStrings;

            _frozenStrings = AddressSpace is { } space && ReadyToRunSections.Count > 0
                ? FrozenObjectReader.ReadStrings(_rawBytes, ReadyToRunSections, space)
                : [];

            return _frozenStrings;
        }
    }

    /// <summary>
    /// Types and method names recovered from a Native AOT binary's embedded NativeFormat
    /// metadata (ReadyToRun section 313, or the reduced stack-trace metadata in 326). Empty
    /// when this is not a Native AOT binary or the binary carries no readable metadata.
    /// </summary>
    public IReadOnlyList<RecoveredType> RecoveredTypes
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_recoveredTypes is not null) return _recoveredTypes;

            _recoveredTypes = ReadyToRunSections.Count > 0
                ? NativeMetadataReader.ReadTypes(_rawBytes, ReadyToRunSections)
                : [];

            return _recoveredTypes;
        }
    }

    /// <summary>
    /// The ILC size report found next to a Native AOT binary, or null when this is not a
    /// Native AOT binary or no readable <c>.mstat</c> sidecar sits beside it. The value is
    /// assigned before the probed flag, so a rare concurrent first read costs at most a
    /// second parse of immutable data.
    /// </summary>
    public MstatData? Mstat
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_mstatProbed)
            {
                _mstat = MstatPath is { } path ? MstatReader.Read(path) : null;
                _mstatProbed = true;
            }

            return _mstat;
        }
    }

    /// <summary>
    /// The path of the <c>.mstat</c> sidecar next to a Native AOT binary, or null when this
    /// is not a Native AOT binary or the file is absent.
    /// </summary>
    public string? MstatPath =>
        BinaryKind == BinaryKind.NativeAot ? FindSidecar(".mstat") : null;

    /// <summary>
    /// The ILC dependency graph found next to a Native AOT binary, or null when this is not
    /// a Native AOT binary or no readable DGML sidecar sits beside it. Graphs run to
    /// hundreds of thousands of links, so touch this only when a dependency question is
    /// actually being asked. The value is assigned before the probed flag, so a rare
    /// concurrent first read costs at most a second parse of immutable data.
    /// </summary>
    public DgmlGraph? Dgml
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_dgmlProbed)
            {
                _dgml = DgmlPath is { } path ? DgmlReader.Read(path) : null;
                _dgmlProbed = true;
            }

            return _dgml;
        }
    }

    /// <summary>
    /// The path of the DGML sidecar next to a Native AOT binary — the codegen graph when
    /// present (its node names match the mstat's exactly), else the scan graph — or null
    /// when this is not a Native AOT binary or neither file is present.
    /// </summary>
    public string? DgmlPath =>
        BinaryKind == BinaryKind.NativeAot
            ? FindSidecar(".codegen.dgml.xml") ?? FindSidecar(".scan.dgml.xml")
            : null;

    /// <summary>
    /// Probes for a sidecar next to the analyzed binary: the binary's extension is replaced
    /// (or the suffix appended for extensionless Linux and macOS binaries) and the file must
    /// exist in the same directory.
    /// </summary>
    private string? FindSidecar(string suffix)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (string.IsNullOrEmpty(directory)) return null;

        var stem = Path.GetFileName(FilePath);
        if (stem.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            stem = stem[..^4];

        var candidate = Path.Combine(directory, stem + suffix);
        return File.Exists(candidate) ? candidate : null;
    }

    /// <summary>
    /// The virtual-address to file-offset map for a native image, or null when the format
    /// is unrecognized. Shared by the Native AOT section and object readers.
    /// </summary>
    private NativeAddressSpace? AddressSpace
    {
        get
        {
            if (!_addressSpaceProbed)
            {
                _addressSpace = NativeAddressSpace.Create(_rawBytes);
                _addressSpaceProbed = true;
            }

            return _addressSpace;
        }
    }

    /// <summary>Portable PDB provenance for the analyzed assembly.</summary>
    public PdbProvenance PdbProvenance { get; private set; } =
        new(PdbProvenanceKind.NoDebugDirectory);

    /// <summary>Gets whether a portable PDB was opened.</summary>
    public bool HasPortablePdb => _pdbReader is not null;

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

    /// <summary>Gets the PE debug directory entries.</summary>
    public IReadOnlyList<DebugDirectoryInfo> DebugDirectory
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _debugDirectory ??= ReadDebugDirectory();
        }
    }

    /// <summary>
    /// Gets the native import table: PE import descriptors, ELF needed libraries and
    /// undefined dynamic symbols, or Mach-O loaded dylibs and undefined symbols.
    /// Needs no CLR header.
    /// </summary>
    public IReadOnlyList<ImportedModuleInfo> Imports
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _imports ??= ReadNativeImports();
        }
    }

    /// <summary>
    /// Gets the native export table: PE exports, or the defined global symbols of an
    /// ELF or Mach-O image. Needs no CLR header; empty when the image exports nothing.
    /// </summary>
    public IReadOnlyList<ExportedFunctionInfo> Exports
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _exports ??= ReadNativeExports();
        }
    }

    /// <summary>
    /// Gets the parsed load configuration directory, or null when absent or not a PE.
    /// </summary>
    public LoadConfigInfo? LoadConfig
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_loadConfigProbed)
            {
                _loadConfig = _peReader is null ? null : PeDirectoryReader.ReadLoadConfig(_peReader);
                _loadConfigProbed = true;
            }

            return _loadConfig;
        }
    }

    /// <summary>Gets decoded Source Link information from the portable PDB.</summary>
    public SourceLinkInfo SourceLink
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _sourceLink ??= PortablePdbUtilities.ReadSourceLink(_pdbReader);
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
    /// Gets the portable PDB <see cref="MetadataReader"/>, or null when no portable PDB is available.
    /// </summary>
    public MetadataReader? GetPdbReader()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _pdbReader;
    }

    /// <summary>
    /// Gets portable PDB debug information for a method definition.
    /// </summary>
    /// <param name="method">The method definition to inspect.</param>
    /// <returns>Decoded portable PDB information, or an empty result when no portable PDB is available.</returns>
    public MethodDebugInfo GetMethodDebugInfo(MethodDefInfo method)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_pdbReader is null)
            return new MethodDebugInfo(method.Token, PdbProvenance, [], []);

        try
        {
            var methodHandle = (MethodDefinitionHandle)MetadataTokens.EntityHandle(method.Token);
            var methodDebug = _pdbReader.GetMethodDebugInformation(methodHandle);
            var sequencePoints = ReadSequencePoints(methodDebug);
            var locals = ReadLocalSlots(methodHandle);
            return new MethodDebugInfo(method.Token, PdbProvenance, sequencePoints, locals);
        }
        catch (Exception ex) when (ex is ArgumentException or BadImageFormatException)
        {
            return new MethodDebugInfo(method.Token, PdbProvenance, [], []);
        }
    }

    /// <summary>
    /// Resolves a portable PDB document path through Source Link mappings.
    /// </summary>
    /// <param name="documentPath">The document path from the portable PDB.</param>
    /// <returns>The resolved Source Link URL, or null when no mapping applies.</returns>
    public string? ResolveSourceLinkUrl(string documentPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return PortablePdbUtilities.ResolveSourceLinkUrl(SourceLink, documentPath);
    }

    /// <summary>
    /// Gets embedded source for a portable PDB document path.
    /// </summary>
    /// <param name="documentPath">The document path from the portable PDB.</param>
    /// <returns>The decoded embedded source, or null when the document has none.</returns>
    public EmbeddedSourceInfo? GetEmbeddedSource(string documentPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return PortablePdbUtilities.ReadEmbeddedSource(_pdbReader, documentPath);
    }

    /// <summary>
    /// Gets the first embedded source document referenced by a method's sequence points.
    /// </summary>
    /// <param name="method">The method whose source should be resolved.</param>
    /// <returns>The decoded embedded source, or null when none is available.</returns>
    public EmbeddedSourceInfo? GetEmbeddedSource(MethodDefInfo method)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var debugInfo = GetMethodDebugInfo(method);
        foreach (var sequencePoint in debugInfo.SequencePoints)
        {
            if (sequencePoint.Document is { Length: > 0 } document
                && sequencePoint.HasEmbeddedSource
                && GetEmbeddedSource(document) is { } source)
            {
                return source;
            }
        }

        return null;
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
        _pdbReaderProvider?.Dispose();
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

    private IReadOnlyList<ImportedModuleInfo> ReadNativeImports()
    {
        if (_peReader is not null) return PeDirectoryReader.ReadImports(_peReader);
        if (ElfImageReader.IsElf(_rawBytes)) return ElfImageReader.ReadImports(_rawBytes);
        if (MachOImageReader.IsMachO(_rawBytes)) return MachOImageReader.ReadImports(_rawBytes);
        return [];
    }

    private IReadOnlyList<ExportedFunctionInfo> ReadNativeExports()
    {
        if (_peReader is not null) return PeDirectoryReader.ReadExports(_peReader);
        if (ElfImageReader.IsElf(_rawBytes)) return ElfImageReader.ReadExports(_rawBytes);
        if (MachOImageReader.IsMachO(_rawBytes)) return MachOImageReader.ReadExports(_rawBytes);
        return [];
    }

    /// <summary>
    /// Reads the target architecture from an ELF or Mach-O header. The bytes have
    /// already passed <see cref="IsNativeExecutable"/>.
    /// </summary>
    private static string GetNativeArchitecture(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 8) return "Unknown";

        // ELF: e_machine is a u16 at offset 18; EI_DATA at offset 5 selects endianness
        if (bytes[0] == 0x7F && bytes[1] == 0x45 && bytes[2] == 0x4C && bytes[3] == 0x46)
        {
            if (bytes.Length < 20) return "Unknown";
            var bigEndian = bytes[5] == 2;
            int machine = bigEndian
                ? bytes[18] << 8 | bytes[19]
                : bytes[19] << 8 | bytes[18];
            return machine switch
            {
                0x3E => "x64",
                0xB7 => "ARM64",
                0x03 => "x86",
                0x28 => "ARM",
                0xF3 => "RISC-V",
                _ => "Unknown",
            };
        }

        // Mach-O: cputype is an i32 at offset 4; 0xCxFAEDFE magics are byte-swapped
        uint magic = (uint)(bytes[0] << 24 | bytes[1] << 16 | bytes[2] << 8 | bytes[3]);
        var swapped = magic is 0xCEFAEDFE or 0xCFFAEDFE;
        uint cpuType = swapped
            ? (uint)(bytes[7] << 24 | bytes[6] << 16 | bytes[5] << 8 | bytes[4])
            : (uint)(bytes[4] << 24 | bytes[5] << 16 | bytes[6] << 8 | bytes[7]);
        return cpuType switch
        {
            0x01000007 => "x64",
            0x0100000C => "ARM64",
            0x00000007 => "x86",
            0x0000000C => "ARM",
            _ => "Unknown",
        };
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

    private void ReadDebugInformation()
    {
        if (_peReader is null)
        {
            _debugDirectory = [];
            PdbProvenance = new PdbProvenance(PdbProvenanceKind.NoDebugDirectory);
            return;
        }

        _debugDirectory = ReadDebugDirectory();
        OpenPortablePdb();
        _sourceLink = PortablePdbUtilities.ReadSourceLink(_pdbReader);
    }

    private List<DebugDirectoryInfo> ReadDebugDirectory()
    {
        if (_peReader is null) return [];

        var entries = _peReader.ReadDebugDirectory();
        return [.. entries.Select(entry => new DebugDirectoryInfo(
            Type: entry.Type,
            Stamp: entry.Stamp,
            MajorVersion: entry.MajorVersion,
            MinorVersion: entry.MinorVersion,
            DataSize: entry.DataSize,
            AddressOfRawData: entry.DataRelativeVirtualAddress,
            PointerToRawData: entry.DataPointer,
            Payload: FormatDebugDirectoryPayload(entry)))];
    }

    private string FormatDebugDirectoryPayload(DebugDirectoryEntry entry)
    {
        if (_peReader is null) return "";

        try
        {
            return entry.Type switch
            {
                DebugDirectoryEntryType.CodeView => FormatCodeViewPayload(entry),
                DebugDirectoryEntryType.Reproducible => "present",
                DebugDirectoryEntryType.PdbChecksum => FormatPdbChecksumPayload(entry),
                DebugDirectoryEntryType.EmbeddedPortablePdb => FormatEmbeddedPortablePdbPayload(entry),
                _ => ""
            };
        }
        catch (Exception ex) when (ex is ArgumentException or BadImageFormatException or IOException)
        {
            return $"unreadable: {ex.Message}";
        }
    }

    private string FormatCodeViewPayload(DebugDirectoryEntry entry)
    {
        var data = _peReader!.ReadCodeViewDebugDirectoryData(entry);
        var format = entry.IsPortableCodeView ? "Portable PDB" : "Windows/non-portable PDB";
        return $"{format}; PDB GUID: {data.Guid}; age: {data.Age}; path: {data.Path}";
    }

    private string FormatPdbChecksumPayload(DebugDirectoryEntry entry)
    {
        var data = _peReader!.ReadPdbChecksumDebugDirectoryData(entry);
        return $"Algorithm: {data.AlgorithmName}; checksum: {Convert.ToHexString([.. data.Checksum])}";
    }

    private string FormatEmbeddedPortablePdbPayload(DebugDirectoryEntry entry)
    {
        var uncompressedSize = entry.DataPointer >= 0 && entry.DataPointer + 4 <= _rawBytes.Length
            ? BitConverter.ToInt32(_rawBytes, entry.DataPointer)
            : -1;
        return uncompressedSize >= 0
            ? $"present; uncompressed size: {uncompressedSize} bytes"
            : "present";
    }

    private void OpenPortablePdb()
    {
        if (_peReader is null)
        {
            PdbProvenance = new PdbProvenance(PdbProvenanceKind.NoDebugDirectory);
            return;
        }

        var entries = _peReader.ReadDebugDirectory();
        if (entries.Length == 0)
        {
            PdbProvenance = new PdbProvenance(PdbProvenanceKind.NoDebugDirectory);
            return;
        }

        if (!IsBundleBacked && TryOpenAssociatedPortablePdb())
            return;

        var embeddedEntry = entries.FirstOrDefault(e => e.Type == DebugDirectoryEntryType.EmbeddedPortablePdb);
        if (embeddedEntry.Type == DebugDirectoryEntryType.EmbeddedPortablePdb
            && TryOpenEmbeddedPortablePdb(embeddedEntry))
        {
            return;
        }

        var codeViewEntry = entries.FirstOrDefault(e => e.Type == DebugDirectoryEntryType.CodeView);
        if (codeViewEntry.Type != DebugDirectoryEntryType.CodeView)
        {
            PdbProvenance = new PdbProvenance(PdbProvenanceKind.NoDebugDirectory);
            return;
        }

        CodeViewDebugDirectoryData codeViewData;
        try
        {
            codeViewData = _peReader.ReadCodeViewDebugDirectoryData(codeViewEntry);
        }
        catch (Exception ex) when (ex is BadImageFormatException or IOException)
        {
            PdbProvenance = new PdbProvenance(
                PdbProvenanceKind.UnsupportedWindowsPdb,
                Details: $"UnsupportedWindowsPdb ({ex.Message})");
            return;
        }

        if (!codeViewEntry.IsPortableCodeView)
        {
            // A native (Windows) PDB. Dotsider can read these; if a matching one sits beside the
            // binary, mark it so — the full symbol parse stays lazy on the NativeSymbols property.
            if (!IsBundleBacked && TryMatchNativePdb(codeViewData) is { } nativePath)
            {
                PdbProvenance = new PdbProvenance(
                    PdbProvenanceKind.NativePdb,
                    nativePath,
                    $"NativePdb (GUID matched, {nativePath})");
                return;
            }

            PdbProvenance = new PdbProvenance(
                PdbProvenanceKind.UnsupportedWindowsPdb,
                codeViewData.Path,
                $"UnsupportedWindowsPdb ({codeViewData.Path})");
            return;
        }

        if (IsBundleBacked)
        {
            PdbProvenance = new PdbProvenance(
                PdbProvenanceKind.BundleSidecarSkipped,
                codeViewData.Path,
                "BundleSidecarSkipped (sidecar probing skipped for bundle-backed assembly)");
            return;
        }

        var probePaths = GetSidecarProbePaths(codeViewData.Path);
        var foundPath = probePaths.FirstOrDefault(File.Exists);
        if (foundPath is null)
        {
            var expected = probePaths.Count > 0 ? probePaths[0] : codeViewData.Path;
            PdbProvenance = new PdbProvenance(
                PdbProvenanceKind.CodeViewSidecarMissing,
                expected,
                $"CodeViewSidecarMissing ({expected})");
            return;
        }

        if (!TryOpenPortablePdbFile(foundPath, codeViewData.Guid, codeViewData.Age, out var mismatch))
        {
            PdbProvenance = mismatch
                ? new PdbProvenance(
                    PdbProvenanceKind.CodeViewSidecarMismatched,
                    foundPath,
                    $"CodeViewSidecarMismatched ({foundPath})")
                : new PdbProvenance(
                    PdbProvenanceKind.UnsupportedWindowsPdb,
                    foundPath,
                    $"UnsupportedWindowsPdb ({foundPath})");
        }
    }

    /// <summary>
    /// Looks for a Windows native PDB beside the binary whose GUID and age match the CodeView
    /// entry, using the cheap block-level probe. Probes the binary's own directory only — like
    /// every other sidecar — so the result does not depend on a build-time absolute path that
    /// happens to still resolve on the current machine. Returns the matching path, or null.
    /// </summary>
    private string? TryMatchNativePdb(CodeViewDebugDirectoryData codeViewData)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (string.IsNullOrEmpty(directory)) return null;

        // The CodeView entry's own file name (its directory is discarded), then <stem>.pdb.
        var candidates = new List<string>(2);
        if (!string.IsNullOrEmpty(codeViewData.Path))
            candidates.Add(Path.Combine(directory, Path.GetFileName(codeViewData.Path)));
        candidates.Add(Path.Combine(directory, Path.GetFileNameWithoutExtension(FilePath) + ".pdb"));

        foreach (var path in candidates)
        {
            if (!File.Exists(path)) continue;
            if (NativePdb.NativePdbReader.TryReadPdbId(path, out var guid, out var age)
                && guid == codeViewData.Guid && age == codeViewData.Age)
            {
                return path;
            }
        }

        return null;
    }

    private bool TryOpenAssociatedPortablePdb()
    {
        if (_peReader is null) return false;

        try
        {
            if (!_peReader.TryOpenAssociatedPortablePdb(
                    FilePath,
                    path => File.Exists(path) ? File.OpenRead(path) : null,
                    out var provider,
                    out var pdbPath))
            {
                return false;
            }

            _pdbReaderProvider = provider;
            if (_pdbReaderProvider is null)
                return false;

            _pdbReader = _pdbReaderProvider.GetMetadataReader();
            PdbProvenance = pdbPath is null
                ? new PdbProvenance(PdbProvenanceKind.Embedded)
                : new PdbProvenance(PdbProvenanceKind.Sidecar, pdbPath);
            return true;
        }
        catch (Exception ex) when (ex is BadImageFormatException or IOException or InvalidOperationException)
        {
            return false;
        }
    }

    private bool TryOpenEmbeddedPortablePdb(DebugDirectoryEntry entry)
    {
        if (_peReader is null) return false;

        try
        {
            _pdbReaderProvider = _peReader.ReadEmbeddedPortablePdbDebugDirectoryData(entry);
            _pdbReader = _pdbReaderProvider.GetMetadataReader();
            PdbProvenance = new PdbProvenance(PdbProvenanceKind.Embedded);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or BadImageFormatException or InvalidOperationException)
        {
            return false;
        }
    }

    private bool TryOpenPortablePdbFile(string path, Guid expectedGuid, int expectedAge, out bool mismatched)
    {
        mismatched = false;
        FileStream? stream = null;
        MetadataReaderProvider? provider = null;
        try
        {
            stream = File.OpenRead(path);
            provider = MetadataReaderProvider.FromPortablePdbStream(stream);
            var reader = provider.GetMetadataReader();
            if (!PortablePdbUtilities.PortablePdbIdMatches(reader, expectedGuid, expectedAge))
            {
                mismatched = true;
                provider.Dispose();
                return false;
            }

            _pdbReaderProvider = provider;
            _pdbReader = reader;
            PdbProvenance = new PdbProvenance(PdbProvenanceKind.Sidecar, path);
            return true;
        }
        catch (BadImageFormatException)
        {
            provider?.Dispose();
            stream?.Dispose();
            return false;
        }
        catch (IOException)
        {
            provider?.Dispose();
            stream?.Dispose();
            return false;
        }
    }

    private List<string> GetSidecarProbePaths(string codeViewPath)
    {
        var paths = new List<string>();
        var assemblyDirectory = Path.GetDirectoryName(FilePath);
        var codeViewFileName = Path.GetFileName(codeViewPath);

        if (!string.IsNullOrEmpty(assemblyDirectory) && !string.IsNullOrEmpty(codeViewFileName))
            paths.Add(Path.Combine(assemblyDirectory, codeViewFileName));

        var defaultPath = Path.ChangeExtension(FilePath, ".pdb");
        if (!paths.Contains(defaultPath, StringComparer.OrdinalIgnoreCase))
            paths.Add(defaultPath);

        if (Path.IsPathFullyQualified(codeViewPath)
            && !paths.Contains(codeViewPath, StringComparer.OrdinalIgnoreCase))
        {
            paths.Add(codeViewPath);
        }

        return paths;
    }

    private List<SequencePointInfo> ReadSequencePoints(MethodDebugInformation methodDebug)
    {
        if (_pdbReader is null) return [];

        var points = new List<SequencePointInfo>();
        foreach (var point in methodDebug.GetSequencePoints())
        {
            var documentHandle = point.Document.IsNil ? methodDebug.Document : point.Document;
            string? documentPath = null;
            if (!documentHandle.IsNil)
            {
                var document = _pdbReader.GetDocument(documentHandle);
                documentPath = _pdbReader.GetString(document.Name);
            }

            var sourceLinkUrl = documentPath is null ? null : ResolveSourceLinkUrl(documentPath);
            var hasEmbeddedSource = PortablePdbUtilities.HasEmbeddedSource(_pdbReader, documentPath);
            points.Add(new SequencePointInfo(
                Offset: point.Offset,
                Document: documentPath,
                StartLine: point.StartLine,
                StartColumn: point.StartColumn,
                EndLine: point.EndLine,
                EndColumn: point.EndColumn,
                IsHidden: point.IsHidden,
                SourceLinkUrl: sourceLinkUrl,
                HasEmbeddedSource: hasEmbeddedSource));
        }

        return points;
    }

    private IReadOnlyList<LocalSlotInfo> ReadLocalSlots(MethodDefinitionHandle methodHandle)
    {
        if (_pdbReader is null) return [];

        var locals = new List<LocalSlotInfo>();
        foreach (var scopeHandle in _pdbReader.GetLocalScopes(methodHandle))
        {
            var scope = _pdbReader.GetLocalScope(scopeHandle);
            foreach (var variableHandle in scope.GetLocalVariables())
            {
                var variable = _pdbReader.GetLocalVariable(variableHandle);
                var name = _pdbReader.GetString(variable.Name);
                if (string.IsNullOrEmpty(name)) continue;

                locals.Add(new LocalSlotInfo(
                    Slot: variable.Index,
                    Name: name,
                    StartOffset: scope.StartOffset,
                    EndOffset: scope.StartOffset + scope.Length,
                    IsDebuggerHidden: variable.Attributes.HasFlag(LocalVariableAttributes.DebuggerHidden)));
            }
        }

        return [.. locals
            .OrderBy(local => local.Slot)
            .ThenBy(local => local.EndOffset - local.StartOffset)
            .ThenBy(local => local.StartOffset)];
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

        (ResolvedAssembly?, AssemblyProvenance, AssemblyRefInfo?)? TryFile(string path, AssemblyProvenance provenance)
        {
            if (!File.Exists(path)) return null;
            var actual = TryReadFileIdentity(path);
            if (actual is null) return null;
            if (IdentityEquals(identity, actual.Value))
                return (new ResolvedAssembly.FromFile(path), provenance, null);
            if (IsFrameworkRollForwardMatch(identity, actual.Value, provenance))
                return (new ResolvedAssembly.FromFile(path), provenance, ToAssemblyRefInfo(actual.Value));
            mismatchPath ??= path;
            return null;
        }

        (ResolvedAssembly?, AssemblyProvenance, AssemblyRefInfo?)? TryBundle(
            ResolvedAssembly.FromBundle? candidate, AssemblyProvenance provenance)
        {
            if (candidate is null) return null;
            var actual = TryReadBundleIdentity(candidate.Bytes);
            if (actual is null) return null;
            if (IdentityEquals(identity, actual.Value))
                return (candidate, provenance, null);
            if (IsFrameworkRollForwardMatch(identity, actual.Value, provenance))
                return (candidate, provenance, ToAssemblyRefInfo(actual.Value));
            mismatchPath ??= $"{candidate.BundlePath}:{candidate.Name}";
            return null;
        }

        (ResolvedAssembly?, AssemblyProvenance, AssemblyRefInfo?)? TryNuGet()
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
            return new AssemblyResolution(h.Item1, h.Item2, null, LoadedIdentity: h.Item3);

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
    /// if the candidate came from app-local, the shared framework, runtime directory, or NuGet
    /// package cache, shares the simple name and public key token, carries a well-known Microsoft
    /// framework public key token, and is at an equal-or-higher assembly version, accept the
    /// version difference. The version floor matches the default <c>AssemblyLoadContext</c>
    /// equal-or-higher rule; downgrades remain a true <see cref="AssemblyProvenance.IdentityMismatch"/>.
    /// </summary>
    private static bool IsFrameworkRollForwardMatch(
        AssemblyRefInfo requested,
        (string Name, string Version, string Culture, string? PublicKeyToken) actual,
        AssemblyProvenance provenance)
    {
        if (provenance is not (AssemblyProvenance.AppLocal
                              or AssemblyProvenance.SharedFramework
                              or AssemblyProvenance.RuntimeDirectory
                              or AssemblyProvenance.NuGetPackageCache))
            return false;
        if (!string.Equals(requested.Name, actual.Name, StringComparison.OrdinalIgnoreCase))
            return false;
        if (string.IsNullOrEmpty(requested.PublicKeyToken) || string.IsNullOrEmpty(actual.PublicKeyToken))
            return false;
        if (!string.Equals(requested.PublicKeyToken, actual.PublicKeyToken, StringComparison.OrdinalIgnoreCase))
            return false;
        if (!WellKnownFrameworkPublicKeyTokens.Contains(requested.PublicKeyToken))
            return false;
        return ParseVersionOrZero(actual.Version) >= ParseVersionOrZero(requested.Version);
    }

    private static Version ParseVersionOrZero(string s) =>
        Version.TryParse(s, out var v) ? v : new Version(0, 0, 0, 0);

    private static AssemblyRefInfo ToAssemblyRefInfo(
        (string Name, string Version, string Culture, string? PublicKeyToken) actual)
        => new(actual.Name, actual.Version, actual.Culture, actual.PublicKeyToken);

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
