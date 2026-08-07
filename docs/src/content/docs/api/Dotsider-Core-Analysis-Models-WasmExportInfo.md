---
title: "WasmExportInfo"
description: "One WebAssembly export entry."
slug: api/dotsider.core.analysis.models.wasmexportinfo
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One WebAssembly export entry.

```csharp
public sealed record WasmExportInfo : IEquatable<WasmExportInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **WasmExportInfo**

## Implements

- [IEquatable\<WasmExportInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### WasmExportInfo(string, WasmExternalKind, int)

One WebAssembly export entry.

**Parameters:**

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The exported name.
- `Kind` ([WasmExternalKind](/api/dotsider.core.analysis.models.wasmexternalkind/)): The exported external kind.
- `Index` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The exported index in the kind's index space.

```csharp
public WasmExportInfo(string Name, WasmExternalKind Kind, int Index)
```

## Properties

### Index

The exported index in the kind's index space.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Index { get; init; }
```

### Kind

The exported external kind.

**Returns:** [WasmExternalKind](/api/dotsider.core.analysis.models.wasmexternalkind/)

```csharp
public WasmExternalKind Kind { get; init; }
```

### Name

The exported name.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

## Methods

### Deconstruct(out string, out WasmExternalKind, out int)

**Parameters:**

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Kind` ([WasmExternalKind](/api/dotsider.core.analysis.models.wasmexternalkind/))
- `Index` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))

```csharp
public void Deconstruct(out string Name, out WasmExternalKind Kind, out int Index)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(WasmExportInfo?)

**Parameters:**

- `other` ([WasmExportInfo](/api/dotsider.core.analysis.models.wasmexportinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(WasmExportInfo? other)
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

### operator !=(WasmExportInfo?, WasmExportInfo?)

**Parameters:**

- `left` ([WasmExportInfo](/api/dotsider.core.analysis.models.wasmexportinfo/))
- `right` ([WasmExportInfo](/api/dotsider.core.analysis.models.wasmexportinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(WasmExportInfo? left, WasmExportInfo? right)
```

### operator ==(WasmExportInfo?, WasmExportInfo?)

**Parameters:**

- `left` ([WasmExportInfo](/api/dotsider.core.analysis.models.wasmexportinfo/))
- `right` ([WasmExportInfo](/api/dotsider.core.analysis.models.wasmexportinfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(WasmExportInfo? left, WasmExportInfo? right)
```
