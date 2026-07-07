---
title: "WasmMemoryInfo"
description: "One WebAssembly memory declaration."
slug: api/dotsider.core.analysis.models.wasmmemoryinfo
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One WebAssembly memory declaration.

```csharp
public sealed record WasmMemoryInfo : IEquatable<WasmMemoryInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **WasmMemoryInfo**

## Implements

- [IEquatable\<WasmMemoryInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### WasmMemoryInfo(int, ulong, ulong?, bool, bool)

One WebAssembly memory declaration.

**Parameters:**

- `Index` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The zero-based memory index.
- `MinimumPages` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)): The minimum memory page count.
- `MaximumPages` ([Nullable\<UInt64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The maximum memory page count when one is declared.
- `IsShared` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): Whether the memory is declared shared.
- `IsMemory64` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): Whether the memory uses 64-bit indices.

```csharp
public WasmMemoryInfo(int Index, ulong MinimumPages, ulong? MaximumPages, bool IsShared, bool IsMemory64)
```

## Properties

### Index

The zero-based memory index.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Index { get; init; }
```

### IsMemory64

Whether the memory uses 64-bit indices.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsMemory64 { get; init; }
```

### IsShared

Whether the memory is declared shared.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsShared { get; init; }
```

### MaximumPages

The maximum memory page count when one is declared.

**Returns:** [Nullable\<UInt64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public ulong? MaximumPages { get; init; }
```

### MinimumPages

The minimum memory page count.

**Returns:** [UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)

```csharp
public ulong MinimumPages { get; init; }
```

