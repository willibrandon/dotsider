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

## Methods

### Deconstruct(out int, out uint, out int)

**Parameters:**

- `Index` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Attribute` ([UInt32](https://learn.microsoft.com/dotnet/api/system.uint32))
- `TypeIndex` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))

```csharp
public void Deconstruct(out int Index, out uint Attribute, out int TypeIndex)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(WasmTagInfo?)

**Parameters:**

- `other` ([WasmTagInfo](/api/dotsider.core.analysis.models.wasmtaginfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(WasmTagInfo? other)
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

### operator !=(WasmTagInfo?, WasmTagInfo?)

**Parameters:**

- `left` ([WasmTagInfo](/api/dotsider.core.analysis.models.wasmtaginfo/))
- `right` ([WasmTagInfo](/api/dotsider.core.analysis.models.wasmtaginfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(WasmTagInfo? left, WasmTagInfo? right)
```

### operator ==(WasmTagInfo?, WasmTagInfo?)

**Parameters:**

- `left` ([WasmTagInfo](/api/dotsider.core.analysis.models.wasmtaginfo/))
- `right` ([WasmTagInfo](/api/dotsider.core.analysis.models.wasmtaginfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(WasmTagInfo? left, WasmTagInfo? right)
```
