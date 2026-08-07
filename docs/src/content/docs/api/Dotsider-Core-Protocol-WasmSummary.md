---
title: "WasmSummary"
description: "Compact WebAssembly module facts. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.wasmsummary
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

Compact WebAssembly module facts.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record WasmSummary : IEquatable<WasmSummary>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **WasmSummary**

## Implements

- [IEquatable\<WasmSummary\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### WasmSummary(int, int, int, int, int, int, int, int, long, int, int, int, int, int, long, int, int?, int?, string?, string, int, IReadOnlyList\<string\>, IReadOnlyList\<string\>, string?)

Compact WebAssembly module facts.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `Version` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `SectionCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `TypeCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `ImportCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `ExportCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `FunctionCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `ImportedFunctionCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `DefinedFunctionCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `CodeSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `TableCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `MemoryCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `GlobalCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `ElementSegmentCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `DataSegmentCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `DataSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `TagCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `StartFunctionIndex` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `DataCount` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `SymbolMapPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `SymbolMapStatus` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `SymbolMapEntryCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `TargetFeatures` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `ProducerFields` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `Diagnostic` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public WasmSummary(int Version, int SectionCount, int TypeCount, int ImportCount, int ExportCount, int FunctionCount, int ImportedFunctionCount, int DefinedFunctionCount, long CodeSize, int TableCount, int MemoryCount, int GlobalCount, int ElementSegmentCount, int DataSegmentCount, long DataSize, int TagCount, int? StartFunctionIndex, int? DataCount, string? SymbolMapPath, string SymbolMapStatus, int SymbolMapEntryCount, IReadOnlyList<string> TargetFeatures, IReadOnlyList<string> ProducerFields, string? Diagnostic)
```

## Properties

### CodeSize

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long CodeSize { get; init; }
```

### DataCount

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? DataCount { get; init; }
```

### DataSegmentCount

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int DataSegmentCount { get; init; }
```

### DataSize

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long DataSize { get; init; }
```

### DefinedFunctionCount

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int DefinedFunctionCount { get; init; }
```

### Diagnostic

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Diagnostic { get; init; }
```

### ElementSegmentCount

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int ElementSegmentCount { get; init; }
```

### ExportCount

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int ExportCount { get; init; }
```

### FunctionCount

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int FunctionCount { get; init; }
```

### GlobalCount

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int GlobalCount { get; init; }
```

### ImportCount

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int ImportCount { get; init; }
```

### ImportedFunctionCount

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int ImportedFunctionCount { get; init; }
```

### MemoryCount

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int MemoryCount { get; init; }
```

### ProducerFields

**Returns:** [IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<string> ProducerFields { get; init; }
```

### SectionCount

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int SectionCount { get; init; }
```

### StartFunctionIndex

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? StartFunctionIndex { get; init; }
```

### SymbolMapEntryCount

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int SymbolMapEntryCount { get; init; }
```

### SymbolMapPath

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? SymbolMapPath { get; init; }
```

### SymbolMapStatus

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string SymbolMapStatus { get; init; }
```

### TableCount

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int TableCount { get; init; }
```

### TagCount

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int TagCount { get; init; }
```

### TargetFeatures

**Returns:** [IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<string> TargetFeatures { get; init; }
```

### TypeCount

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int TypeCount { get; init; }
```

### Version

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Version { get; init; }
```

## Methods

### Deconstruct(out int, out int, out int, out int, out int, out int, out int, out int, out long, out int, out int, out int, out int, out int, out long, out int, out int?, out int?, out string?, out string, out int, out IReadOnlyList\<string\>, out IReadOnlyList\<string\>, out string?)

**Parameters:**

- `Version` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `SectionCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `TypeCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `ImportCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `ExportCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `FunctionCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `ImportedFunctionCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `DefinedFunctionCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `CodeSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `TableCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `MemoryCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `GlobalCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `ElementSegmentCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `DataSegmentCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `DataSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `TagCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `StartFunctionIndex` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `DataCount` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `SymbolMapPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `SymbolMapStatus` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `SymbolMapEntryCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `TargetFeatures` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `ProducerFields` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `Diagnostic` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out int Version, out int SectionCount, out int TypeCount, out int ImportCount, out int ExportCount, out int FunctionCount, out int ImportedFunctionCount, out int DefinedFunctionCount, out long CodeSize, out int TableCount, out int MemoryCount, out int GlobalCount, out int ElementSegmentCount, out int DataSegmentCount, out long DataSize, out int TagCount, out int? StartFunctionIndex, out int? DataCount, out string? SymbolMapPath, out string SymbolMapStatus, out int SymbolMapEntryCount, out IReadOnlyList<string> TargetFeatures, out IReadOnlyList<string> ProducerFields, out string? Diagnostic)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(WasmSummary?)

**Parameters:**

- `other` ([WasmSummary](/api/dotsider.core.protocol.wasmsummary/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(WasmSummary? other)
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

### operator !=(WasmSummary?, WasmSummary?)

**Parameters:**

- `left` ([WasmSummary](/api/dotsider.core.protocol.wasmsummary/))
- `right` ([WasmSummary](/api/dotsider.core.protocol.wasmsummary/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(WasmSummary? left, WasmSummary? right)
```

### operator ==(WasmSummary?, WasmSummary?)

**Parameters:**

- `left` ([WasmSummary](/api/dotsider.core.protocol.wasmsummary/))
- `right` ([WasmSummary](/api/dotsider.core.protocol.wasmsummary/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(WasmSummary? left, WasmSummary? right)
```
