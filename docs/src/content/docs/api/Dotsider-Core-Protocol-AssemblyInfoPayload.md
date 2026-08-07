---
title: "AssemblyInfoPayload"
description: "Assembly identity and analysis capabilities exposed by protocol surfaces. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.assemblyinfopayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

Assembly identity and analysis capabilities exposed by protocol surfaces.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record AssemblyInfoPayload : IEquatable<AssemblyInfoPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **AssemblyInfoPayload**

## Implements

- [IEquatable\<AssemblyInfoPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### AssemblyInfoPayload(string?, string, string, long, string?, string?, string?, string?, string?, string, bool, BinaryKind, NativeAotInfo?, string, string?, bool, string, string, bool, PdbProvenance, SourceLinkInfo, int, int, int, int, int, int, int, NativeSymbolSource?, NativeSymbolStatus?, PreIlcSummary?, ReadyToRunSummary?, WebcilSummary?, WasmSummary?)

Assembly identity and analysis capabilities exposed by protocol surfaces.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `Mode` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `FilePath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `FileName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `FileSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `AssemblyVersion` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `TargetFramework` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Culture` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `PublicKeyToken` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Architecture` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `HasMetadata` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `BinaryKind` ([BinaryKind](/api/dotsider.core.analysis.models.binarykind/))
- `NativeAotInfo` ([NativeAotInfo](/api/dotsider.core.analysis.models.nativeaotinfo/))
- `DisplayName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `SourceBundlePath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `IsBundleBacked` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `PreferredRuntimePack` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `LaunchPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `CanSaveInPlace` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `PdbProvenance` ([PdbProvenance](/api/dotsider.core.analysis.models.pdbprovenance/))
- `SourceLink` ([SourceLinkInfo](/api/dotsider.core.analysis.models.sourcelinkinfo/))
- `TypeCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `MethodCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `AssemblyRefCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `ReadyToRunSectionCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `RecoveredTypeCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `FrozenStringCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `NativeSymbolCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `NativeSymbolSource` ([Nullable\<NativeSymbolSource\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `NativeSymbolStatus` ([Nullable\<NativeSymbolStatus\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `PreIlc` ([PreIlcSummary](/api/dotsider.core.protocol.preilcsummary/))
- `ReadyToRun` ([ReadyToRunSummary](/api/dotsider.core.protocol.readytorunsummary/))
- `Webcil` ([WebcilSummary](/api/dotsider.core.protocol.webcilsummary/))
- `Wasm` ([WasmSummary](/api/dotsider.core.protocol.wasmsummary/))

```csharp
public AssemblyInfoPayload(string? Mode, string FilePath, string FileName, long FileSize, string? AssemblyName, string? AssemblyVersion, string? TargetFramework, string? Culture, string? PublicKeyToken, string Architecture, bool HasMetadata, BinaryKind BinaryKind, NativeAotInfo? NativeAotInfo, string DisplayName, string? SourceBundlePath, bool IsBundleBacked, string PreferredRuntimePack, string LaunchPath, bool CanSaveInPlace, PdbProvenance PdbProvenance, SourceLinkInfo SourceLink, int TypeCount, int MethodCount, int AssemblyRefCount, int ReadyToRunSectionCount, int RecoveredTypeCount, int FrozenStringCount, int NativeSymbolCount, NativeSymbolSource? NativeSymbolSource, NativeSymbolStatus? NativeSymbolStatus, PreIlcSummary? PreIlc, ReadyToRunSummary? ReadyToRun, WebcilSummary? Webcil, WasmSummary? Wasm)
```

## Properties

### Architecture

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Architecture { get; init; }
```

### AssemblyName

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? AssemblyName { get; init; }
```

### AssemblyRefCount

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int AssemblyRefCount { get; init; }
```

### AssemblyVersion

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? AssemblyVersion { get; init; }
```

### BinaryKind

**Returns:** [BinaryKind](/api/dotsider.core.analysis.models.binarykind/)

```csharp
public BinaryKind BinaryKind { get; init; }
```

### CanSaveInPlace

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool CanSaveInPlace { get; init; }
```

### Culture

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Culture { get; init; }
```

### DisplayName

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string DisplayName { get; init; }
```

### FileName

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string FileName { get; init; }
```

### FilePath

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string FilePath { get; init; }
```

### FileSize

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long FileSize { get; init; }
```

### FrozenStringCount

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int FrozenStringCount { get; init; }
```

### HasMetadata

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool HasMetadata { get; init; }
```

### IsBundleBacked

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsBundleBacked { get; init; }
```

### LaunchPath

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string LaunchPath { get; init; }
```

### MethodCount

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int MethodCount { get; init; }
```

### Mode

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Mode { get; init; }
```

### NativeAotInfo

**Returns:** [NativeAotInfo](/api/dotsider.core.analysis.models.nativeaotinfo/)

```csharp
public NativeAotInfo? NativeAotInfo { get; init; }
```

### NativeSymbolCount

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int NativeSymbolCount { get; init; }
```

### NativeSymbolSource

**Returns:** [Nullable\<NativeSymbolSource\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public NativeSymbolSource? NativeSymbolSource { get; init; }
```

### NativeSymbolStatus

**Returns:** [Nullable\<NativeSymbolStatus\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public NativeSymbolStatus? NativeSymbolStatus { get; init; }
```

### PdbProvenance

**Returns:** [PdbProvenance](/api/dotsider.core.analysis.models.pdbprovenance/)

```csharp
public PdbProvenance PdbProvenance { get; init; }
```

### PreferredRuntimePack

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string PreferredRuntimePack { get; init; }
```

### PreIlc

**Returns:** [PreIlcSummary](/api/dotsider.core.protocol.preilcsummary/)

```csharp
public PreIlcSummary? PreIlc { get; init; }
```

### PublicKeyToken

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? PublicKeyToken { get; init; }
```

### ReadyToRun

**Returns:** [ReadyToRunSummary](/api/dotsider.core.protocol.readytorunsummary/)

```csharp
public ReadyToRunSummary? ReadyToRun { get; init; }
```

### ReadyToRunSectionCount

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int ReadyToRunSectionCount { get; init; }
```

### RecoveredTypeCount

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int RecoveredTypeCount { get; init; }
```

### SourceBundlePath

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? SourceBundlePath { get; init; }
```

### SourceLink

**Returns:** [SourceLinkInfo](/api/dotsider.core.analysis.models.sourcelinkinfo/)

```csharp
public SourceLinkInfo SourceLink { get; init; }
```

### TargetFramework

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? TargetFramework { get; init; }
```

### TypeCount

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int TypeCount { get; init; }
```

### Wasm

**Returns:** [WasmSummary](/api/dotsider.core.protocol.wasmsummary/)

```csharp
public WasmSummary? Wasm { get; init; }
```

### Webcil

**Returns:** [WebcilSummary](/api/dotsider.core.protocol.webcilsummary/)

```csharp
public WebcilSummary? Webcil { get; init; }
```

## Methods

### Deconstruct(out string?, out string, out string, out long, out string?, out string?, out string?, out string?, out string?, out string, out bool, out BinaryKind, out NativeAotInfo?, out string, out string?, out bool, out string, out string, out bool, out PdbProvenance, out SourceLinkInfo, out int, out int, out int, out int, out int, out int, out int, out NativeSymbolSource?, out NativeSymbolStatus?, out PreIlcSummary?, out ReadyToRunSummary?, out WebcilSummary?, out WasmSummary?)

**Parameters:**

- `Mode` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `FilePath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `FileName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `FileSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `AssemblyVersion` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `TargetFramework` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Culture` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `PublicKeyToken` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Architecture` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `HasMetadata` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `BinaryKind` ([BinaryKind](/api/dotsider.core.analysis.models.binarykind/))
- `NativeAotInfo` ([NativeAotInfo](/api/dotsider.core.analysis.models.nativeaotinfo/))
- `DisplayName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `SourceBundlePath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `IsBundleBacked` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `PreferredRuntimePack` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `LaunchPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `CanSaveInPlace` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `PdbProvenance` ([PdbProvenance](/api/dotsider.core.analysis.models.pdbprovenance/))
- `SourceLink` ([SourceLinkInfo](/api/dotsider.core.analysis.models.sourcelinkinfo/))
- `TypeCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `MethodCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `AssemblyRefCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `ReadyToRunSectionCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `RecoveredTypeCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `FrozenStringCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `NativeSymbolCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `NativeSymbolSource` ([Nullable\<NativeSymbolSource\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `NativeSymbolStatus` ([Nullable\<NativeSymbolStatus\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `PreIlc` ([PreIlcSummary](/api/dotsider.core.protocol.preilcsummary/))
- `ReadyToRun` ([ReadyToRunSummary](/api/dotsider.core.protocol.readytorunsummary/))
- `Webcil` ([WebcilSummary](/api/dotsider.core.protocol.webcilsummary/))
- `Wasm` ([WasmSummary](/api/dotsider.core.protocol.wasmsummary/))

```csharp
public void Deconstruct(out string? Mode, out string FilePath, out string FileName, out long FileSize, out string? AssemblyName, out string? AssemblyVersion, out string? TargetFramework, out string? Culture, out string? PublicKeyToken, out string Architecture, out bool HasMetadata, out BinaryKind BinaryKind, out NativeAotInfo? NativeAotInfo, out string DisplayName, out string? SourceBundlePath, out bool IsBundleBacked, out string PreferredRuntimePack, out string LaunchPath, out bool CanSaveInPlace, out PdbProvenance PdbProvenance, out SourceLinkInfo SourceLink, out int TypeCount, out int MethodCount, out int AssemblyRefCount, out int ReadyToRunSectionCount, out int RecoveredTypeCount, out int FrozenStringCount, out int NativeSymbolCount, out NativeSymbolSource? NativeSymbolSource, out NativeSymbolStatus? NativeSymbolStatus, out PreIlcSummary? PreIlc, out ReadyToRunSummary? ReadyToRun, out WebcilSummary? Webcil, out WasmSummary? Wasm)
```

### Equals(AssemblyInfoPayload?)

**Parameters:**

- `other` ([AssemblyInfoPayload](/api/dotsider.core.protocol.assemblyinfopayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(AssemblyInfoPayload? other)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### GetHashCode()

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public override int GetHashCode()
```

### ToString()

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public override string ToString()
```

## Members

### operator !=(AssemblyInfoPayload?, AssemblyInfoPayload?)

**Parameters:**

- `left` ([AssemblyInfoPayload](/api/dotsider.core.protocol.assemblyinfopayload/))
- `right` ([AssemblyInfoPayload](/api/dotsider.core.protocol.assemblyinfopayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(AssemblyInfoPayload? left, AssemblyInfoPayload? right)
```

### operator ==(AssemblyInfoPayload?, AssemblyInfoPayload?)

**Parameters:**

- `left` ([AssemblyInfoPayload](/api/dotsider.core.protocol.assemblyinfopayload/))
- `right` ([AssemblyInfoPayload](/api/dotsider.core.protocol.assemblyinfopayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(AssemblyInfoPayload? left, AssemblyInfoPayload? right)
```
