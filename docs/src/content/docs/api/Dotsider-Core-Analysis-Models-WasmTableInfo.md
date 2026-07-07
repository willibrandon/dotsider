---
title: "WasmTableInfo"
description: "One WebAssembly table declaration."
slug: api/dotsider.core.analysis.models.wasmtableinfo
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One WebAssembly table declaration.

```csharp
public sealed record WasmTableInfo : IEquatable<WasmTableInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **WasmTableInfo**

## Implements

- [IEquatable\<WasmTableInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### WasmTableInfo(int, string, ulong, ulong?)

One WebAssembly table declaration.

**Parameters:**

- `Index` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The zero-based table index.
- `RefType` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The table reference type.
- `Minimum` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)): The minimum element count.
- `Maximum` ([Nullable\<UInt64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The maximum element count when one is declared.

```csharp
public WasmTableInfo(int Index, string RefType, ulong Minimum, ulong? Maximum)
```

## Properties

### Index

The zero-based table index.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Index { get; init; }
```

### Maximum

The maximum element count when one is declared.

**Returns:** [Nullable\<UInt64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public ulong? Maximum { get; init; }
```

### Minimum

The minimum element count.

**Returns:** [UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)

```csharp
public ulong Minimum { get; init; }
```

### RefType

The table reference type.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string RefType { get; init; }
```

