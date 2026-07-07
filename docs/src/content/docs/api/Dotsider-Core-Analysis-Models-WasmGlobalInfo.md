---
title: "WasmGlobalInfo"
description: "One WebAssembly global declaration."
slug: api/dotsider.core.analysis.models.wasmglobalinfo
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One WebAssembly global declaration.

```csharp
public sealed record WasmGlobalInfo : IEquatable<WasmGlobalInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **WasmGlobalInfo**

## Implements

- [IEquatable\<WasmGlobalInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### WasmGlobalInfo(int, byte, string, bool)

One WebAssembly global declaration.

**Parameters:**

- `Index` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The zero-based global index.
- `ValueType` ([Byte](https://learn.microsoft.com/dotnet/api/system.byte)): The raw WebAssembly value-type byte.
- `ValueTypeName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The display name for the value type.
- `IsMutable` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): Whether the global is mutable.

```csharp
public WasmGlobalInfo(int Index, byte ValueType, string ValueTypeName, bool IsMutable)
```

## Properties

### Index

The zero-based global index.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Index { get; init; }
```

### IsMutable

Whether the global is mutable.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsMutable { get; init; }
```

### ValueType

The raw WebAssembly value-type byte.

**Returns:** [Byte](https://learn.microsoft.com/dotnet/api/system.byte)

```csharp
public byte ValueType { get; init; }
```

### ValueTypeName

The display name for the value type.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string ValueTypeName { get; init; }
```

