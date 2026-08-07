---
title: "AssemblyAnalyzer"
description: "Core analyzer that reads .NET assemblies, Webcil app assemblies, native binaries, and raw Wasm modules. It uses BCL metadata/PE readers where possible and routes runtime-native formats through dotsider's format readers for IL, strings, symbols, disassembly, and size data."
slug: api/dotsider.core.analysis.assemblyanalyzer
sidebar:
  order: 0
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

Core analyzer that reads .NET assemblies, Webcil app assemblies, native binaries, and raw Wasm
modules. It uses BCL metadata/PE readers where possible and routes runtime-native formats
through dotsider's format readers for IL, strings, symbols, disassembly, and size data.

```csharp
public sealed class AssemblyAnalyzer : IDisposable
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **AssemblyAnalyzer**

## Implements

- [IDisposable](https://learn.microsoft.com/dotnet/api/system.idisposable)

## Constructors

### AssemblyAnalyzer(byte[], string, string?, string?, string?, string?)

Creates an analyzer from raw module bytes with resolution context inherited from its
manifest assembly.

**Parameters:**

- `bytes` ([Byte[]](https://learn.microsoft.com/dotnet/api/system.byte[])): The raw module bytes.
- `filePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The authenticated sibling-module path.
- `sourceBundlePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The source bundle path, or null.
- `displayName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The logical name of the analyzed module.
- `targetFrameworkOverride` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The manifest's target-framework context.
- `preferredRuntimePackOverride` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The manifest's preferred runtime pack.

**Exceptions:**

- [BadImageFormatException](https://learn.microsoft.com/dotnet/api/system.badimageformatexception): bytes contains a recognized managed PE or Webcil image that is malformed.

```csharp
public AssemblyAnalyzer(byte[] bytes, string filePath, string? sourceBundlePath, string? displayName, string? targetFrameworkOverride, string? preferredRuntimePackOverride)
```

### AssemblyAnalyzer(byte[], string, string?, string?)

Creates an analyzer from raw bytes in memory. Used for bundle-extracted
assemblies and as a last-resort fallback when disk I/O is unavailable
after a save operation.

**Parameters:**

- `bytes` ([Byte[]](https://learn.microsoft.com/dotnet/api/system.byte[])): The raw assembly bytes.
- `filePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): On-disk path for physical operations (tracing, save checks).
- `sourceBundlePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): If this assembly was extracted from a single-file bundle, the path to the bundle file.
Used for assembly resolution context.
- `displayName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Logical name of the analyzed artifact for UI display (e.g. "SelfContainedConsole.dll"
when the entry assembly is extracted from a bundle). If null, defaults to the file name
portion of filePath.

**Exceptions:**

- [BadImageFormatException](https://learn.microsoft.com/dotnet/api/system.badimageformatexception): bytes contains a recognized managed PE or Webcil image that is malformed.

```csharp
public AssemblyAnalyzer(byte[] bytes, string filePath, string? sourceBundlePath = null, string? displayName = null)
```

### AssemblyAnalyzer(string)

Opens and analyzes the specified .NET assembly file.

**Parameters:**

- `filePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Absolute path to the assembly file.

**Exceptions:**

- [FileNotFoundException](https://learn.microsoft.com/dotnet/api/system.io.filenotfoundexception): The file does not exist.
- [BadImageFormatException](https://learn.microsoft.com/dotnet/api/system.badimageformatexception): The file contains a recognized managed PE or Webcil image that is malformed.

```csharp
public AssemblyAnalyzer(string filePath)
```

## Properties

### Architecture

The PE architecture description (e.g., "AnyCPU", "x64", "ARM64").

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Architecture { get; }
```

### AssemblyName

The assembly simple name, or null if the file has no assembly manifest.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? AssemblyName { get; }
```

### AssemblyRefs

Gets the AssemblyRef metadata table entries.

**Returns:** [IReadOnlyList\<AssemblyRefInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<AssemblyRefInfo> AssemblyRefs { get; }
```

### AssemblyVersion

The assembly version string, or null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? AssemblyVersion { get; }
```

### BinaryKind

Coarse classification of the analyzed binary.

**Returns:** [BinaryKind](/api/dotsider.core.analysis.models.binarykind/)

```csharp
public BinaryKind BinaryKind { get; }
```

### CanSaveInPlace

Whether in-place hex save is supported. Returns `false` for bundle-backed analyzers
because writing extracted entry bytes back over the bundle would corrupt it.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool CanSaveInPlace { get; }
```

### ClrHeader

The parsed CLR header, or null if not a .NET assembly.

**Returns:** [ClrHeader](/api/dotsider.core.analysis.models.clrheader/)

```csharp
public ClrHeader? ClrHeader { get; }
```

### CreatedTime

The creation time in UTC.

**Returns:** [DateTime](https://learn.microsoft.com/dotnet/api/system.datetime)

```csharp
public DateTime CreatedTime { get; }
```

### Culture

The assembly culture, or null for culture-neutral assemblies.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Culture { get; }
```

### CustomAttributes

Gets the custom attributes applied to metadata entities.

**Returns:** [IReadOnlyList\<CustomAttributeInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<CustomAttributeInfo> CustomAttributes { get; }
```

### DebugDirectory

Gets the PE debug directory entries.

**Returns:** [IReadOnlyList\<DebugDirectoryInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<DebugDirectoryInfo> DebugDirectory { get; }
```

### Dgml

The ILC dependency graph found next to a Native AOT binary, or null when this is not
a Native AOT binary or no readable DGML sidecar sits beside it. Graphs run to
hundreds of thousands of links, so touch this only when a dependency question is
actually being asked. The value is assigned before the probed flag, so a rare
concurrent first read costs at most a second parse of immutable data.

**Returns:** [DgmlGraph](/api/dotsider.core.analysis.models.dgmlgraph/)

```csharp
public DgmlGraph? Dgml { get; }
```

### DgmlPath

The path of the DGML sidecar next to a Native AOT binary — the codegen graph when
present (its node names match the mstat's exactly), else the scan graph — or null
when this is not a Native AOT binary or neither file is present.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? DgmlPath { get; }
```

### DisplayName

Logical display name for the analyzed artifact. For bundle-backed analyzers this is
the entry assembly file name (e.g. "SelfContainedConsole.dll") while [FilePath](/api/dotsider.core.analysis.assemblyanalyzer.filepath/)
points to the bundle executable on disk. For file-backed analyzers, equals [FileName](/api/dotsider.core.analysis.assemblyanalyzer.filename/).

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string DisplayName { get; }
```

### Exports

Gets the native export table: PE exports, or the defined global symbols of an
ELF or Mach-O image. Needs no CLR header; empty when the image exports nothing.

**Returns:** [IReadOnlyList\<ExportedFunctionInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<ExportedFunctionInfo> Exports { get; }
```

### FieldDefs

Gets the FieldDef metadata table entries.

**Returns:** [IReadOnlyList\<FieldDefInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<FieldDefInfo> FieldDefs { get; }
```

### FileName

The file name without directory path.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string FileName { get; }
```

### FilePath

The full path to the analyzed assembly file.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string FilePath { get; }
```

### FileSize

The file size in bytes.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long FileSize { get; }
```

### FrozenStrings

Frozen [String](https://learn.microsoft.com/dotnet/api/system.string) literals recovered from a Native AOT binary's frozen
object region — the AOT counterpart of the #US heap. Empty when this is not a
Native AOT binary, or on Linux where the region is filled at startup and has no
file backing (the raw UTF-16 scan surfaces that text instead).

**Returns:** [IReadOnlyList\<StringEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<StringEntry> FrozenStrings { get; }
```

### HasEmbeddedNativeCode

Whether this image has precompiled native method bodies mapped to managed methods.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool HasEmbeddedNativeCode { get; }
```

### HasManagedMetadata

Whether this image carries ECMA-335 metadata (managed or ReadyToRun).

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool HasManagedMetadata { get; }
```

### HasMetadata

Whether the PE file contains .NET metadata.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool HasMetadata { get; }
```

### HasPortablePdb

Gets whether a portable PDB was opened.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool HasPortablePdb { get; }
```

### Imports

Gets the native import table: PE import descriptors, ELF needed libraries and
undefined dynamic symbols, or Mach-O loaded dylibs and undefined symbols.
ELF symbols whose GNU version requirements are absent or malformed are grouped
under `(unversioned)` rather than attributed to untrusted metadata.
Needs no CLR header.

**Returns:** [IReadOnlyList\<ImportedModuleInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<ImportedModuleInfo> Imports { get; }
```

### IsBundleBacked

Whether this analyzer was created from bytes extracted from a single-file bundle.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsBundleBacked { get; }
```

### IsReadOnly

Whether the file is read-only on disk.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsReadOnly { get; }
```

### IsReadyToRun

Whether this is a crossgen2 ReadyToRun image.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsReadyToRun { get; }
```

### LastModified

The last modification time in UTC.

**Returns:** [DateTime](https://learn.microsoft.com/dotnet/api/system.datetime)

```csharp
public DateTime LastModified { get; }
```

### LaunchPath

The path to launch when tracing this assembly. For bundle-backed analyzers this is
the bundle executable; for file-backed analyzers this is [FilePath](/api/dotsider.core.analysis.assemblyanalyzer.filepath/).

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string LaunchPath { get; }
```

### LoadConfig

Gets the parsed load configuration directory, or null when absent or not a PE.

**Returns:** [LoadConfigInfo](/api/dotsider.core.analysis.models.loadconfiginfo/)

```csharp
public LoadConfigInfo? LoadConfig { get; }
```

### ManagedNativeIndex

The managed↔native correlation index over the attached companion set, built lazily
on first access; null before [AttachPreIlcCompanions](/api/dotsider.core.analysis.assemblyanalyzer.attachpreilccompanions/). A build that
races a detach or dispose abandons its result: it captures the generation up front,
materializes inputs under an [ObjectDisposedException](https://learn.microsoft.com/dotnet/api/system.objectdisposedexception) guard, and
publishes only when the generation is unchanged.

**Returns:** [ManagedNativeIndex](/api/dotsider.core.analysis.managednativeindex/)

```csharp
public ManagedNativeIndex? ManagedNativeIndex { get; }
```

### MemberRefs

Gets the MemberRef metadata table entries.

**Returns:** [IReadOnlyList\<MemberRefInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<MemberRefInfo> MemberRefs { get; }
```

### MethodDefs

Gets the MethodDef metadata table entries.

**Returns:** [IReadOnlyList\<MethodDefInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<MethodDefInfo> MethodDefs { get; }
```

### Mstat

The ILC size report found next to a Native AOT binary, or null when this is not a
Native AOT binary or no readable `.mstat` sidecar sits beside it. The value is
assigned before the probed flag, so a rare concurrent first read costs at most a
second parse of immutable data.

**Returns:** [MstatData](/api/dotsider.core.analysis.models.mstatdata/)

```csharp
public MstatData? Mstat { get; }
```

### MstatPath

The path of the `.mstat` sidecar next to a Native AOT binary, or null when this
is not a Native AOT binary or the file is absent.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? MstatPath { get; }
```

### NativeAotInfo

Facts from the embedded ReadyToRun header when this is a Native AOT binary,
or null. Only probed for metadata-less files — a managed ReadyToRun assembly
also embeds the header, but there it accompanies metadata rather than
replacing it.

**Returns:** [NativeAotInfo](/api/dotsider.core.analysis.models.nativeaotinfo/)

```csharp
public NativeAotInfo? NativeAotInfo { get; }
```

### NativeSymbols

The native symbols of this binary — function names, addresses, and sizes read from its
PDB, DWARF, or dSYM, or function boundaries from unwind data when no symbols exist. Null
for managed assemblies. Parsed on demand; the value is assigned before the probed flag, so
a rare concurrent first read costs at most a second parse of immutable data.

**Returns:** [NativeSymbolInfo](/api/dotsider.core.analysis.models.nativesymbolinfo/)

```csharp
public NativeSymbolInfo? NativeSymbols { get; }
```

### NativeSymbolsPath

The symbol file the native symbols were read from (PDB, .dbg, or dSYM), or null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? NativeSymbolsPath { get; }
```

### PdbProvenance

Portable PDB provenance for the analyzed assembly.

**Returns:** [PdbProvenance](/api/dotsider.core.analysis.models.pdbprovenance/)

```csharp
public PdbProvenance PdbProvenance { get; }
```

### PeHeaders

The parsed PE headers.

**Returns:** [PeHeaders](/api/dotsider.core.analysis.models.peheaders/)

```csharp
public PeHeaders? PeHeaders { get; }
```

### PreferredRuntimePack

The preferred .NET runtime pack for this assembly, detected from its assembly references.
Returns "Microsoft.WindowsDesktop.App" for WPF/WinForms assemblies,
"Microsoft.AspNetCore.App" for ASP.NET Core assemblies,
or "Microsoft.NETCore.App" otherwise.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string PreferredRuntimePack { get; }
```

### PreIlcCompanions

The attached pre-ILC companion set, or null before [AttachPreIlcCompanions](/api/dotsider.core.analysis.assemblyanalyzer.attachpreilccompanions/)
succeeds. Owned by this analyzer — see [PreIlcCompanionSet](/api/dotsider.core.analysis.models.preilccompanionset/) for the
ownership contract.

**Returns:** [PreIlcCompanionSet](/api/dotsider.core.analysis.models.preilccompanionset/)

```csharp
public PreIlcCompanionSet? PreIlcCompanions { get; }
```

### PreIlcSidecars

The pre-ILC build outputs probed for a Native AOT binary — its managed input,
portable PDB, and intermediate-tree mstat/DGML sidecars — or null when this is not
a Native AOT binary or nothing was found. The value is assigned before the probed
flag, so a rare concurrent first read costs at most a second probe.

**Returns:** [PreIlcSidecars](/api/dotsider.core.analysis.models.preilcsidecars/)

```csharp
public PreIlcSidecars? PreIlcSidecars { get; }
```

### PublicKeyToken

The public key token as a hex string, or null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? PublicKeyToken { get; }
```

### RawBytes

Gets the raw bytes of the file for hex editor display.

**Returns:** [ReadOnlyMemory\<Byte\>](https://learn.microsoft.com/dotnet/api/system.readonlymemory-1)

```csharp
public ReadOnlyMemory<byte> RawBytes { get; }
```

### ReadyToRunCodeImage

The analyzer whose bytes hold this image's precompiled native code — itself for a
non-composite or composite image, or the resolved owner composite for a composite
component. Null when this is not a ReadyToRun image or the code image cannot be resolved.

**Returns:** [AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)

```csharp
public AssemblyAnalyzer? ReadyToRunCodeImage { get; }
```

### ReadyToRunComponents

The component assemblies of a composite ReadyToRun image, each with its resolution state, or
an empty list for a non-composite image or before resolution.

**Returns:** [IReadOnlyList\<ReadyToRunComponent\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<ReadyToRunComponent> ReadyToRunComponents { get; }
```

### ReadyToRunIndex

The queryable index over this image's precompiled methods, or null when the image is not
ReadyToRun or its method-map tables are unavailable, malformed, or over budget. Built
lazily from [ReadyToRunMethods](/api/dotsider.core.analysis.assemblyanalyzer.readytorunmethods/).

**Returns:** [ReadyToRunIndex](/api/dotsider.core.analysis.readytorunindex/)

```csharp
public ReadyToRunIndex? ReadyToRunIndex { get; }
```

### ReadyToRunInfo

The crossgen2 ReadyToRun header facts, or null when the image does not claim to be
ReadyToRun. Present (with a diagnostic [Status](/api/dotsider.core.analysis.models.readytoruninfo.status/)) even for a
corrupt or unsupported header, so a broken image is surfaced rather than hidden. Probed
lazily regardless of [HasMetadata](/api/dotsider.core.analysis.assemblyanalyzer.hasmetadata/) (composite images have no own metadata).

**Returns:** [ReadyToRunInfo](/api/dotsider.core.analysis.models.readytoruninfo/)

```csharp
public ReadyToRunInfo? ReadyToRunInfo { get; }
```

### ReadyToRunMetadataProviders

The distinct metadata providers backing this ReadyToRun image — itself for a non-composite one,
or the resolved component assemblies for a composite. Used to find a method that is present in a
component's metadata but absent from the precompiled map. Empty when this is not a ReadyToRun image.

**Returns:** [IReadOnlyList\<AssemblyAnalyzer\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<AssemblyAnalyzer> ReadyToRunMetadataProviders { get; }
```

### ReadyToRunMethods

The precompiled methods of a ReadyToRun image joined to their native code ranges, or an
empty list when this is not a usable ReadyToRun image or its method-map tables are malformed
or exceed their traversal budget. The remaining ReadyToRun header, section, and managed
metadata remain available. Built lazily from the entry-point tables. For a non-composite
image the code lives in this file; composite resolution is layered on in
ReadyToRunImageReader.

**Returns:** [IReadOnlyList\<ReadyToRunMethodEntry\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<ReadyToRunMethodEntry> ReadyToRunMethods { get; }
```

### ReadyToRunSections

The ReadyToRun section table — the Native AOT module sections for a Native AOT binary, or
the crossgen2 sections (ids 100–126) for a classic ReadyToRun image — or an empty list
otherwise. Both feed the PE/Metadata "R2R Sections" tab.

**Returns:** [IReadOnlyList\<RtrSection\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<RtrSection> ReadyToRunSections { get; }
```

### RecoveredTypes

Types and method names recovered from a Native AOT binary's embedded NativeFormat
metadata (ReadyToRun section 313, or the reduced stack-trace metadata in 326). Empty
when this is not a Native AOT binary or the binary carries no readable metadata.
Malformed, cyclic, or excessively large metadata yields the fully decoded prefix.

**Returns:** [IReadOnlyList\<RecoveredType\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<RecoveredType> RecoveredTypes { get; }
```

### Resources

Gets the manifest resources defined in the assembly.

**Returns:** [IReadOnlyList\<ResourceInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<ResourceInfo> Resources { get; }
```

### Sections

Gets the PE sections.

**Returns:** [IReadOnlyList\<SectionInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<SectionInfo> Sections { get; }
```

### SourceBundlePath

If this assembly was loaded from a single-file bundle, the path to the bundle file.
Used as resolution context when probing for referenced assemblies.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? SourceBundlePath { get; }
```

### SourceLink

Gets decoded Source Link information from the portable PDB.

**Returns:** [SourceLinkInfo](/api/dotsider.core.analysis.models.sourcelinkinfo/)

```csharp
public SourceLinkInfo SourceLink { get; }
```

### TargetFramework

The target framework moniker (e.g., ".NETCoreApp,Version=v10.0"), or null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? TargetFramework { get; }
```

### TypeDefs

Gets the TypeDef metadata table entries.

**Returns:** [IReadOnlyList\<TypeDefInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<TypeDefInfo> TypeDefs { get; }
```

### TypeRefs

Gets the TypeRef metadata table entries.

**Returns:** [IReadOnlyList\<TypeRefInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<TypeRefInfo> TypeRefs { get; }
```

### WasmModuleInfo

Parsed WebAssembly module facts when this file is a raw `.wasm` module, or null for
PE, ELF, and Mach-O inputs. The main .NET browser-wasm native module is
`dotnet.native.wasm`. Malformed modules preserve the safely decoded prefix and report
the reason through [Diagnostic](/api/dotsider.core.analysis.models.wasmmoduleinfo.diagnostic/).

**Returns:** [WasmModuleInfo](/api/dotsider.core.analysis.models.wasmmoduleinfo/)

```csharp
public WasmModuleInfo? WasmModuleInfo { get; }
```

### WebcilInfo

Parsed Webcil provenance when this analyzer opened a Webcil managed assembly directly or
unwrapped one from a WebAssembly container. Null for PE, raw Wasm, ELF, and Mach-O inputs.

**Returns:** [WebcilInfo](/api/dotsider.core.analysis.models.webcilinfo/)

```csharp
public WebcilInfo? WebcilInfo { get; }
```

## Methods

### AttachPreIlcCompanions()

Opens the probed pre-ILC managed input (and validated local references) as an
attached companion set. Idempotent — a second call returns the existing set.
Returns null when there is nothing attachable or the companion cannot be opened.
The set is owned by this analyzer and disposed with it.

**Returns:** [PreIlcCompanionSet](/api/dotsider.core.analysis.models.preilccompanionset/)

```csharp
public PreIlcCompanionSet? AttachPreIlcCompanions()
```

### DetachPreIlcCompanions()

Detaches and disposes the pre-ILC companion set and drops the correlation index.
A concurrent index build observes the generation change and never publishes.

```csharp
public void DetachPreIlcCompanions()
```

### Dispose()

```csharp
public void Dispose()
```

### GetEmbeddedSource(MethodDefInfo)

Gets the first embedded source document referenced by a method's sequence points.

**Parameters:**

- `method` ([MethodDefInfo](/api/dotsider.core.analysis.models.methoddefinfo/)): The method whose source should be resolved.

**Returns:** [EmbeddedSourceInfo](/api/dotsider.core.analysis.models.embeddedsourceinfo/)

The decoded embedded source, or null when none is available or its data is malformed
or exceeds the supported size limit.

```csharp
public EmbeddedSourceInfo? GetEmbeddedSource(MethodDefInfo method)
```

### GetEmbeddedSource(string)

Gets embedded source for a portable PDB document path.

**Parameters:**

- `documentPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The document path from the portable PDB.

**Returns:** [EmbeddedSourceInfo](/api/dotsider.core.analysis.models.embeddedsourceinfo/)

The decoded embedded source, or null when the document has none or its data is malformed
or exceeds the supported size limit.

```csharp
public EmbeddedSourceInfo? GetEmbeddedSource(string documentPath)
```

### GetMetadataReader()

Gets the underlying [MetadataReader](https://learn.microsoft.com/dotnet/api/system.reflection.metadata.metadatareader) for advanced queries.
Returns null if the file has no .NET metadata.

**Returns:** [MetadataReader](https://learn.microsoft.com/dotnet/api/system.reflection.metadata.metadatareader)

```csharp
public MetadataReader? GetMetadataReader()
```

### GetMethodBody(MethodDefInfo)

Gets the method body bytes for IL disassembly.
Returns null if the method has no IL body (abstract, extern, or native).

**Parameters:**

- `method` ([MethodDefInfo](/api/dotsider.core.analysis.models.methoddefinfo/)): The method definition to get the body for.

**Returns:** [MethodBodyBlock](https://learn.microsoft.com/dotnet/api/system.reflection.metadata.methodbodyblock)

The method body block, or null. The returned block references analyzer-owned storage and
must not be used after this analyzer is disposed.

**Exceptions:**

- [BadImageFormatException](https://learn.microsoft.com/dotnet/api/system.badimageformatexception): The method RVA maps to a malformed or truncated method body.
- [ObjectDisposedException](https://learn.microsoft.com/dotnet/api/system.objectdisposedexception): This analyzer has been disposed.

```csharp
public MethodBodyBlock? GetMethodBody(MethodDefInfo method)
```

### GetMethodDebugInfo(MethodDefInfo)

Gets portable PDB debug information for a method definition.

**Parameters:**

- `method` ([MethodDefInfo](/api/dotsider.core.analysis.models.methoddefinfo/)): The method definition to inspect.

**Returns:** [MethodDebugInfo](/api/dotsider.core.analysis.models.methoddebuginfo/)

Decoded portable PDB information, or an empty result when no portable PDB is available.

```csharp
public MethodDebugInfo GetMethodDebugInfo(MethodDefInfo method)
```

### GetPdbReader()

Gets the portable PDB [MetadataReader](https://learn.microsoft.com/dotnet/api/system.reflection.metadata.metadatareader), or null when no portable PDB is available.

**Returns:** [MetadataReader](https://learn.microsoft.com/dotnet/api/system.reflection.metadata.metadatareader)

```csharp
public MetadataReader? GetPdbReader()
```

### IsFrameworkAssembly(AssemblyProvenance, AssemblyRefInfo, string?, string?)

Classifies whether an assembly belongs to the .NET framework surface regardless of
deployment model. Returns true when the node was located through the
shared framework or runtime directory, or when its identity matches a well-known
Microsoft framework public key token, or when the shared-framework locator recognizes
its simple name for the supplied target framework. This classification is used by the
TUI framework-filter toggle so framework assemblies shipped inside a self-contained
publish or single-file bundle are filtered consistently with framework assemblies
loaded from the shared runtime.

**Parameters:**

- `provenance` ([AssemblyProvenance](/api/dotsider.core.analysis.models.assemblyprovenance/)): How the node was located.
- `identity` ([AssemblyRefInfo](/api/dotsider.core.analysis.models.assemblyrefinfo/)): The resolved assembly's identity.
- `targetFramework` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The referencing assembly's target framework moniker.
- `preferredRuntimePack` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The referencing assembly's preferred runtime pack.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

true if the node represents a framework assembly.

```csharp
public static bool IsFrameworkAssembly(AssemblyProvenance provenance, AssemblyRefInfo identity, string? targetFramework, string? preferredRuntimePack)
```

### ReadyToRunMetadataProviderFor(Guid)

The analyzer whose ECMA-335 metadata backs the given module — this image for a non-composite
one, or the resolved component assembly for a composite. Falls back to this analyzer.

**Parameters:**

- `mvid` ([Guid](https://learn.microsoft.com/dotnet/api/system.guid)): The module version id of the owning assembly.

**Returns:** [AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)

```csharp
public AssemblyAnalyzer ReadyToRunMetadataProviderFor(Guid mvid)
```

### ResolveAssembly(string, string, string?, string?, string?)

Resolves a referenced assembly name to a file on disk or bytes from a bundle.
Probes: app-local, contained NuGet package assets named by `.deps.json`,
runtime directory, source bundle, host process bundle, adjacent bundles, and
.NET shared framework.

**Parameters:**

- `referencingAssemblyPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Path of the assembly that references the target.
- `assemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Assembly name without extension (e.g. "System.Runtime").
- `targetFramework` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Target framework moniker for version-matched shared framework probing.
- `preferredRuntimePack` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Preferred runtime pack to probe first (e.g. "Microsoft.AspNetCore.App").
- `sourceBundlePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): If the referencing assembly came from a bundle, the bundle path.

**Returns:** [ResolvedAssembly](/api/dotsider.core.analysis.models.resolvedassembly/)

The resolved assembly, or `null` if not found.

```csharp
public static ResolvedAssembly? ResolveAssembly(string referencingAssemblyPath, string assemblyName, string? targetFramework = null, string? preferredRuntimePack = null, string? sourceBundlePath = null)
```

### ResolveAssemblyByIdentity(string, AssemblyRefInfo, string?, string?, string?, NetFxBindingContext?)

Resolves a referenced assembly by full identity (name, version, culture, public key token).
Probes every stage of String) and accepts only candidates whose
manifest identity matches the requested identity exactly. If no probe produces a full
match but at least one probe produces a simple-name match whose identity differs,
returns [IdentityMismatch](/api/dotsider.core.analysis.models.assemblyprovenance.identitymismatch/) with the path of that candidate —
the graph does not expand from mismatched files.

**Parameters:**

- `referencingAssemblyPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Path of the assembly that references the target.
- `identity` ([AssemblyRefInfo](/api/dotsider.core.analysis.models.assemblyrefinfo/)): The full identity the caller expects to resolve.
- `targetFramework` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Target framework moniker for shared-framework probing.
- `preferredRuntimePack` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Preferred runtime pack name.
- `sourceBundlePath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Bundle path, when the referencing assembly came from a bundle.
- `netFxBindingContext` ([NetFxBindingContext](/api/dotsider.core.analysis.models.netfxbindingcontext/)): Per-root .NET Framework binding context, or null for non-net48 roots.
When supplied, the resolution routes through NetFxBindingContext) instead of the
.NET Core probe chain, faithfully modeling the CLR's framework unification + machine.config
+ publisher policy + app config + GAC + Framework[64] runtime + codeBase + appBase order.

**Returns:** [AssemblyResolution](/api/dotsider.core.analysis.models.assemblyresolution/)

An [AssemblyResolution](/api/dotsider.core.analysis.models.assemblyresolution/) carrying the resolved assembly, provenance, optional
candidate-probe path, and (for net48 roots) the applied policy and loaded identity.

```csharp
public static AssemblyResolution ResolveAssemblyByIdentity(string referencingAssemblyPath, AssemblyRefInfo identity, string? targetFramework = null, string? preferredRuntimePack = null, string? sourceBundlePath = null, NetFxBindingContext? netFxBindingContext = null)
```

### ResolveAssemblyPath(string, string)

Backward-compatible wrapper that resolves to a file path only.
Returns `null` for bundle-backed results.

**Parameters:**

- `referencingAssemblyPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `assemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string))

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public static string? ResolveAssemblyPath(string referencingAssemblyPath, string assemblyName)
```

### ResolveSourceLinkUrl(string)

Resolves a portable PDB document path through Source Link mappings.

**Parameters:**

- `documentPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The document path from the portable PDB.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

The resolved Source Link URL, or null when no mapping applies.

```csharp
public string? ResolveSourceLinkUrl(string documentPath)
```

### ResolveToken(int)

Resolves a metadata token to a human-readable name.

**Parameters:**

- `token` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The metadata token to resolve.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

A display string for the token. Constructed generic methods include their decoded type
arguments; malformed or unsupported metadata is returned as the original hexadecimal token.

```csharp
public string ResolveToken(int token)
```
