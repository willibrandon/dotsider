---
title: "WasmModuleInfo"
description: "Parsed facts for a WebAssembly module, including its functions and optional .NET symbol map."
slug: api/dotsider.core.analysis.models.wasmmoduleinfo
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Parsed facts for a WebAssembly module, including its functions and optional .NET symbol map.

```csharp
public sealed record WasmModuleInfo : IEquatable<WasmModuleInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **WasmModuleInfo**

## Implements

- [IEquatable\<WasmModuleInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### WasmModuleInfo(int, IReadOnlyList\<WasmSectionInfo\>, IReadOnlyList\<WasmTypeInfo\>, IReadOnlyList\<WasmImportInfo\>, IReadOnlyList\<WasmExportInfo\>, IReadOnlyList\<WasmFunctionInfo\>, IReadOnlyList\<WasmTableInfo\>, IReadOnlyList\<WasmMemoryInfo\>, IReadOnlyList\<WasmGlobalInfo\>, IReadOnlyList\<WasmElementSegmentInfo\>, IReadOnlyList\<WasmDataSegmentInfo\>, IReadOnlyList\<WasmTagInfo\>, int?, int?, IReadOnlyList\<string\>, IReadOnlyList\<string\>, string?, WasmSymbolMapStatus, int, string?)

Parsed facts for a WebAssembly module, including its functions and optional .NET symbol map.

**Parameters:**

- `Version` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The WebAssembly binary version.
- `Sections` ([IReadOnlyList\<WasmSectionInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The section table in file order.
- `Types` ([IReadOnlyList\<WasmTypeInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The parsed function type entries.
- `Imports` ([IReadOnlyList\<WasmImportInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The parsed import entries.
- `Exports` ([IReadOnlyList\<WasmExportInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The parsed export entries.
- `Functions` ([IReadOnlyList\<WasmFunctionInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Imported and defined functions in function-index order.
- `Tables` ([IReadOnlyList\<WasmTableInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The parsed table declarations.
- `Memories` ([IReadOnlyList\<WasmMemoryInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The parsed memory declarations.
- `Globals` ([IReadOnlyList\<WasmGlobalInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The parsed global declarations.
- `Elements` ([IReadOnlyList\<WasmElementSegmentInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The parsed element segments.
- `DataSegments` ([IReadOnlyList\<WasmDataSegmentInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The parsed data segments.
- `Tags` ([IReadOnlyList\<WasmTagInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The parsed exception tag declarations.
- `StartFunctionIndex` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The start function index, when present.
- `DataCount` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The data-count section value, when present.
- `TargetFeatures` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Feature names from the `target_features` custom section.
- `ProducerFields` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Producer strings from the `producers` custom section.
- `SymbolMapPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The symbol-map sidecar path, when loaded.
- `SymbolMapStatus` ([WasmSymbolMapStatus](/api/dotsider.core.analysis.models.wasmsymbolmapstatus/)): The symbol-map probe outcome.
- `SymbolMapEntryCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The number of parsed sidecar entries.
- `Diagnostic` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The reason standard-section parsing stopped after the safely decoded prefix, or null when all
standard sections were decoded.

```csharp
public WasmModuleInfo(int Version, IReadOnlyList<WasmSectionInfo> Sections, IReadOnlyList<WasmTypeInfo> Types, IReadOnlyList<WasmImportInfo> Imports, IReadOnlyList<WasmExportInfo> Exports, IReadOnlyList<WasmFunctionInfo> Functions, IReadOnlyList<WasmTableInfo> Tables, IReadOnlyList<WasmMemoryInfo> Memories, IReadOnlyList<WasmGlobalInfo> Globals, IReadOnlyList<WasmElementSegmentInfo> Elements, IReadOnlyList<WasmDataSegmentInfo> DataSegments, IReadOnlyList<WasmTagInfo> Tags, int? StartFunctionIndex, int? DataCount, IReadOnlyList<string> TargetFeatures, IReadOnlyList<string> ProducerFields, string? SymbolMapPath, WasmSymbolMapStatus SymbolMapStatus, int SymbolMapEntryCount, string? Diagnostic)
```

## Properties

### CodeSize

The total byte count of all defined function instruction streams.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long CodeSize { get; }
```

### DataCount

The data-count section value, when present.

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? DataCount { get; init; }
```

### DataSegments

The parsed data segments.

**Returns:** [IReadOnlyList\<WasmDataSegmentInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<WasmDataSegmentInfo> DataSegments { get; init; }
```

### DataSize

The total byte count of all data segments.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long DataSize { get; }
```

### DefinedFunctionCount

The number of defined functions with code bodies in the module.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int DefinedFunctionCount { get; }
```

### Diagnostic

The reason standard-section parsing stopped after the safely decoded prefix, or null when all
standard sections were decoded.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Diagnostic { get; init; }
```

### Elements

The parsed element segments.

**Returns:** [IReadOnlyList\<WasmElementSegmentInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<WasmElementSegmentInfo> Elements { get; init; }
```

### Exports

The parsed export entries.

**Returns:** [IReadOnlyList\<WasmExportInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<WasmExportInfo> Exports { get; init; }
```

### Functions

Imported and defined functions in function-index order.

**Returns:** [IReadOnlyList\<WasmFunctionInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<WasmFunctionInfo> Functions { get; init; }
```

### Globals

The parsed global declarations.

**Returns:** [IReadOnlyList\<WasmGlobalInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<WasmGlobalInfo> Globals { get; init; }
```

### ImportedFunctionCount

The number of imported functions in the module.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int ImportedFunctionCount { get; }
```

### Imports

The parsed import entries.

**Returns:** [IReadOnlyList\<WasmImportInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<WasmImportInfo> Imports { get; init; }
```

### Memories

The parsed memory declarations.

**Returns:** [IReadOnlyList\<WasmMemoryInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<WasmMemoryInfo> Memories { get; init; }
```

### ProducerFields

Producer strings from the `producers` custom section.

**Returns:** [IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<string> ProducerFields { get; init; }
```

### Sections

The section table in file order.

**Returns:** [IReadOnlyList\<WasmSectionInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<WasmSectionInfo> Sections { get; init; }
```

### StartFunctionIndex

The start function index, when present.

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? StartFunctionIndex { get; init; }
```

### SymbolMapEntryCount

The number of parsed sidecar entries.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int SymbolMapEntryCount { get; init; }
```

### SymbolMapPath

The symbol-map sidecar path, when loaded.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? SymbolMapPath { get; init; }
```

### SymbolMapStatus

The symbol-map probe outcome.

**Returns:** [WasmSymbolMapStatus](/api/dotsider.core.analysis.models.wasmsymbolmapstatus/)

```csharp
public WasmSymbolMapStatus SymbolMapStatus { get; init; }
```

### Tables

The parsed table declarations.

**Returns:** [IReadOnlyList\<WasmTableInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<WasmTableInfo> Tables { get; init; }
```

### Tags

The parsed exception tag declarations.

**Returns:** [IReadOnlyList\<WasmTagInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<WasmTagInfo> Tags { get; init; }
```

### TargetFeatures

Feature names from the `target_features` custom section.

**Returns:** [IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<string> TargetFeatures { get; init; }
```

### Types

The parsed function type entries.

**Returns:** [IReadOnlyList\<WasmTypeInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<WasmTypeInfo> Types { get; init; }
```

### Version

The WebAssembly binary version.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Version { get; init; }
```

