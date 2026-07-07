---
title: "WasmLocalInfo"
description: "A run-length encoded local declaration inside a WebAssembly function body."
slug: api/dotsider.core.analysis.models.wasmlocalinfo
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A run-length encoded local declaration inside a WebAssembly function body.

```csharp
public sealed record WasmLocalInfo : IEquatable<WasmLocalInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **WasmLocalInfo**

## Implements

- [IEquatable\<WasmLocalInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### WasmLocalInfo(uint, byte, string)

A run-length encoded local declaration inside a WebAssembly function body.

**Parameters:**

- `Count` ([UInt32](https://learn.microsoft.com/dotnet/api/system.uint32)): The number of locals in this run.
- `ValueType` ([Byte](https://learn.microsoft.com/dotnet/api/system.byte)): The raw Wasm value type byte.
- `DisplayType` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The display name of the value type.

```csharp
public WasmLocalInfo(uint Count, byte ValueType, string DisplayType)
```

## Properties

### Count

The number of locals in this run.

**Returns:** [UInt32](https://learn.microsoft.com/dotnet/api/system.uint32)

```csharp
public uint Count { get; init; }
```

### DisplayType

The display name of the value type.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string DisplayType { get; init; }
```

### ValueType

The raw Wasm value type byte.

**Returns:** [Byte](https://learn.microsoft.com/dotnet/api/system.byte)

```csharp
public byte ValueType { get; init; }
```

