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

## Methods

### Deconstruct(out int, out string, out ulong, out ulong?)

**Parameters:**

- `Index` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `RefType` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Minimum` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64))
- `Maximum` ([Nullable\<UInt64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))

```csharp
public void Deconstruct(out int Index, out string RefType, out ulong Minimum, out ulong? Maximum)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(WasmTableInfo?)

**Parameters:**

- `other` ([WasmTableInfo](/api/dotsider.core.analysis.models.wasmtableinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(WasmTableInfo? other)
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

### operator !=(WasmTableInfo?, WasmTableInfo?)

**Parameters:**

- `left` ([WasmTableInfo](/api/dotsider.core.analysis.models.wasmtableinfo/))
- `right` ([WasmTableInfo](/api/dotsider.core.analysis.models.wasmtableinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(WasmTableInfo? left, WasmTableInfo? right)
```

### operator ==(WasmTableInfo?, WasmTableInfo?)

**Parameters:**

- `left` ([WasmTableInfo](/api/dotsider.core.analysis.models.wasmtableinfo/))
- `right` ([WasmTableInfo](/api/dotsider.core.analysis.models.wasmtableinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(WasmTableInfo? left, WasmTableInfo? right)
```
