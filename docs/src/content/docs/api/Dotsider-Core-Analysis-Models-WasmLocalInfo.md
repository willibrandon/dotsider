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

## Methods

### Deconstruct(out uint, out byte, out string)

**Parameters:**

- `Count` ([UInt32](https://learn.microsoft.com/dotnet/api/system.uint32))
- `ValueType` ([Byte](https://learn.microsoft.com/dotnet/api/system.byte))
- `DisplayType` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out uint Count, out byte ValueType, out string DisplayType)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(WasmLocalInfo?)

**Parameters:**

- `other` ([WasmLocalInfo](/api/dotsider.core.analysis.models.wasmlocalinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(WasmLocalInfo? other)
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

### operator !=(WasmLocalInfo?, WasmLocalInfo?)

**Parameters:**

- `left` ([WasmLocalInfo](/api/dotsider.core.analysis.models.wasmlocalinfo/))
- `right` ([WasmLocalInfo](/api/dotsider.core.analysis.models.wasmlocalinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(WasmLocalInfo? left, WasmLocalInfo? right)
```

### operator ==(WasmLocalInfo?, WasmLocalInfo?)

**Parameters:**

- `left` ([WasmLocalInfo](/api/dotsider.core.analysis.models.wasmlocalinfo/))
- `right` ([WasmLocalInfo](/api/dotsider.core.analysis.models.wasmlocalinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(WasmLocalInfo? left, WasmLocalInfo? right)
```
