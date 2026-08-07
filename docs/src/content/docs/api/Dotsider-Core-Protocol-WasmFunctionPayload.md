---
title: "WasmFunctionPayload"
description: "A WebAssembly function row. Defines a stable contract for command-line and MCP protocol responses. Uses an explicit shape that source-generated JSON preserves in Native AOT."
slug: api/dotsider.core.protocol.wasmfunctionpayload
sidebar:
  order: 4
---

**Namespace:** `Dotsider.Core.Protocol`

**Assembly:** Dotsider.Core.dll

A WebAssembly function row.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

```csharp
public sealed record WasmFunctionPayload : IEquatable<WasmFunctionPayload>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **WasmFunctionPayload**

## Implements

- [IEquatable\<WasmFunctionPayload\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### WasmFunctionPayload(int, string, string, bool, string?, string?, bool, IReadOnlyList\<string\>, int?, long?, int, long?, int, IReadOnlyList\<string\>, IReadOnlyList\<string\>)

A WebAssembly function row.
Defines a stable contract for command-line and MCP protocol responses.
Uses an explicit shape that source-generated JSON preserves in Native AOT.

**Parameters:**

- `Index` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `NameSource` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `IsImported` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `ImportModule` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `ImportName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `IsExported` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `ExportNames` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `TypeIndex` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `BodyOffset` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `BodySize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `CodeOffset` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `CodeSize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `ParamTypes` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `ResultTypes` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public WasmFunctionPayload(int Index, string Name, string NameSource, bool IsImported, string? ImportModule, string? ImportName, bool IsExported, IReadOnlyList<string> ExportNames, int? TypeIndex, long? BodyOffset, int BodySize, long? CodeOffset, int CodeSize, IReadOnlyList<string> ParamTypes, IReadOnlyList<string> ResultTypes)
```

## Properties

### BodyOffset

**Returns:** [Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public long? BodyOffset { get; init; }
```

### BodySize

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int BodySize { get; init; }
```

### CodeOffset

**Returns:** [Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public long? CodeOffset { get; init; }
```

### CodeSize

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int CodeSize { get; init; }
```

### ExportNames

**Returns:** [IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<string> ExportNames { get; init; }
```

### ImportModule

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? ImportModule { get; init; }
```

### ImportName

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? ImportName { get; init; }
```

### Index

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Index { get; init; }
```

### IsExported

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsExported { get; init; }
```

### IsImported

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsImported { get; init; }
```

### Name

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### NameSource

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string NameSource { get; init; }
```

### ParamTypes

**Returns:** [IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<string> ParamTypes { get; init; }
```

### ResultTypes

**Returns:** [IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<string> ResultTypes { get; init; }
```

### TypeIndex

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? TypeIndex { get; init; }
```

## Methods

### Deconstruct(out int, out string, out string, out bool, out string?, out string?, out bool, out IReadOnlyList\<string\>, out int?, out long?, out int, out long?, out int, out IReadOnlyList\<string\>, out IReadOnlyList\<string\>)

**Parameters:**

- `Index` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `NameSource` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `IsImported` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `ImportModule` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `ImportName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `IsExported` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `ExportNames` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `TypeIndex` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `BodyOffset` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `BodySize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `CodeOffset` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `CodeSize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `ParamTypes` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `ResultTypes` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public void Deconstruct(out int Index, out string Name, out string NameSource, out bool IsImported, out string? ImportModule, out string? ImportName, out bool IsExported, out IReadOnlyList<string> ExportNames, out int? TypeIndex, out long? BodyOffset, out int BodySize, out long? CodeOffset, out int CodeSize, out IReadOnlyList<string> ParamTypes, out IReadOnlyList<string> ResultTypes)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(WasmFunctionPayload?)

**Parameters:**

- `other` ([WasmFunctionPayload](/api/dotsider.core.protocol.wasmfunctionpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(WasmFunctionPayload? other)
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

### operator !=(WasmFunctionPayload?, WasmFunctionPayload?)

**Parameters:**

- `left` ([WasmFunctionPayload](/api/dotsider.core.protocol.wasmfunctionpayload/))
- `right` ([WasmFunctionPayload](/api/dotsider.core.protocol.wasmfunctionpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(WasmFunctionPayload? left, WasmFunctionPayload? right)
```

### operator ==(WasmFunctionPayload?, WasmFunctionPayload?)

**Parameters:**

- `left` ([WasmFunctionPayload](/api/dotsider.core.protocol.wasmfunctionpayload/))
- `right` ([WasmFunctionPayload](/api/dotsider.core.protocol.wasmfunctionpayload/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(WasmFunctionPayload? left, WasmFunctionPayload? right)
```
