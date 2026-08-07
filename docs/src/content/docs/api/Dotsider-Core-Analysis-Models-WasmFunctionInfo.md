---
title: "WasmFunctionInfo"
description: "A WebAssembly function, imported or defined in the code section."
slug: api/dotsider.core.analysis.models.wasmfunctioninfo
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A WebAssembly function, imported or defined in the code section.

```csharp
public sealed record WasmFunctionInfo : IEquatable<WasmFunctionInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **WasmFunctionInfo**

## Implements

- [IEquatable\<WasmFunctionInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### WasmFunctionInfo(int, int?, string, string, bool, string?, string?, bool, IReadOnlyList\<string\>, long?, int, long?, int, IReadOnlyList\<WasmLocalInfo\>, IReadOnlyList\<byte\>, IReadOnlyList\<byte\>)

A WebAssembly function, imported or defined in the code section.

**Parameters:**

- `Index` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The module-wide function index, including imported functions.
- `TypeIndex` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The function type index, when known.
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The best display name for the function.
- `NameSource` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Where the display name came from.
- `IsImported` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): Whether the function is imported and has no body in this module.
- `ImportModule` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The import module for imported functions.
- `ImportName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The import name for imported functions.
- `IsExported` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): Whether the function is exported.
- `ExportNames` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): All export names that point at this function.
- `BodyOffset` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The file offset of the function body payload, including local declarations.
- `BodySize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The body payload size in bytes.
- `CodeOffset` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The file offset of the first instruction byte after local declarations.
- `CodeSize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The instruction byte count after local declarations.
- `Locals` ([IReadOnlyList\<WasmLocalInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The function's run-length encoded local declarations.
- `ParamTypes` ([IReadOnlyList\<Byte\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The raw Wasm parameter type bytes.
- `ResultTypes` ([IReadOnlyList\<Byte\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The raw Wasm result type bytes.

```csharp
public WasmFunctionInfo(int Index, int? TypeIndex, string Name, string NameSource, bool IsImported, string? ImportModule, string? ImportName, bool IsExported, IReadOnlyList<string> ExportNames, long? BodyOffset, int BodySize, long? CodeOffset, int CodeSize, IReadOnlyList<WasmLocalInfo> Locals, IReadOnlyList<byte> ParamTypes, IReadOnlyList<byte> ResultTypes)
```

## Properties

### BodyOffset

The file offset of the function body payload, including local declarations.

**Returns:** [Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public long? BodyOffset { get; init; }
```

### BodySize

The body payload size in bytes.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int BodySize { get; init; }
```

### CodeOffset

The file offset of the first instruction byte after local declarations.

**Returns:** [Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public long? CodeOffset { get; init; }
```

### CodeSize

The instruction byte count after local declarations.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int CodeSize { get; init; }
```

### ExportNames

All export names that point at this function.

**Returns:** [IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<string> ExportNames { get; init; }
```

### ImportModule

The import module for imported functions.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? ImportModule { get; init; }
```

### ImportName

The import name for imported functions.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? ImportName { get; init; }
```

### Index

The module-wide function index, including imported functions.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Index { get; init; }
```

### IsExported

Whether the function is exported.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsExported { get; init; }
```

### IsImported

Whether the function is imported and has no body in this module.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsImported { get; init; }
```

### Locals

The function's run-length encoded local declarations.

**Returns:** [IReadOnlyList\<WasmLocalInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<WasmLocalInfo> Locals { get; init; }
```

### Name

The best display name for the function.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### NameSource

Where the display name came from.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string NameSource { get; init; }
```

### ParamTypes

The raw Wasm parameter type bytes.

**Returns:** [IReadOnlyList\<Byte\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<byte> ParamTypes { get; init; }
```

### ResultTypes

The raw Wasm result type bytes.

**Returns:** [IReadOnlyList\<Byte\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<byte> ResultTypes { get; init; }
```

### TypeIndex

The function type index, when known.

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? TypeIndex { get; init; }
```

## Methods

### Deconstruct(out int, out int?, out string, out string, out bool, out string?, out string?, out bool, out IReadOnlyList\<string\>, out long?, out int, out long?, out int, out IReadOnlyList\<WasmLocalInfo\>, out IReadOnlyList\<byte\>, out IReadOnlyList\<byte\>)

**Parameters:**

- `Index` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `TypeIndex` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `NameSource` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `IsImported` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `ImportModule` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `ImportName` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `IsExported` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `ExportNames` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `BodyOffset` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `BodySize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `CodeOffset` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `CodeSize` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Locals` ([IReadOnlyList\<WasmLocalInfo\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `ParamTypes` ([IReadOnlyList\<Byte\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `ResultTypes` ([IReadOnlyList\<Byte\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public void Deconstruct(out int Index, out int? TypeIndex, out string Name, out string NameSource, out bool IsImported, out string? ImportModule, out string? ImportName, out bool IsExported, out IReadOnlyList<string> ExportNames, out long? BodyOffset, out int BodySize, out long? CodeOffset, out int CodeSize, out IReadOnlyList<WasmLocalInfo> Locals, out IReadOnlyList<byte> ParamTypes, out IReadOnlyList<byte> ResultTypes)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(WasmFunctionInfo?)

**Parameters:**

- `other` ([WasmFunctionInfo](/api/dotsider.core.analysis.models.wasmfunctioninfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(WasmFunctionInfo? other)
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

### operator !=(WasmFunctionInfo?, WasmFunctionInfo?)

**Parameters:**

- `left` ([WasmFunctionInfo](/api/dotsider.core.analysis.models.wasmfunctioninfo/))
- `right` ([WasmFunctionInfo](/api/dotsider.core.analysis.models.wasmfunctioninfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(WasmFunctionInfo? left, WasmFunctionInfo? right)
```

### operator ==(WasmFunctionInfo?, WasmFunctionInfo?)

**Parameters:**

- `left` ([WasmFunctionInfo](/api/dotsider.core.analysis.models.wasmfunctioninfo/))
- `right` ([WasmFunctionInfo](/api/dotsider.core.analysis.models.wasmfunctioninfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(WasmFunctionInfo? left, WasmFunctionInfo? right)
```
