---
title: "WasmTagInfo"
description: "One WebAssembly exception tag declaration."
slug: api/dotsider.core.analysis.models.wasmtaginfo
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One WebAssembly exception tag declaration.

```csharp
public sealed record WasmTagInfo : IEquatable<WasmTagInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **WasmTagInfo**

## Implements

- [IEquatable\<WasmTagInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### WasmTagInfo(int, uint, int)

One WebAssembly exception tag declaration.

**Parameters:**

- `Index` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The zero-based tag index.
- `Attribute` ([UInt32](https://learn.microsoft.com/dotnet/api/system.uint32)): The tag attribute value.
- `TypeIndex` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The function type index used by the tag.

```csharp
public WasmTagInfo(int Index, uint Attribute, int TypeIndex)
```

## Properties

### Attribute

The tag attribute value.

**Returns:** [UInt32](https://learn.microsoft.com/dotnet/api/system.uint32)

```csharp
public uint Attribute { get; init; }
```

### Index

The zero-based tag index.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Index { get; init; }
```

### TypeIndex

The function type index used by the tag.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int TypeIndex { get; init; }
```

