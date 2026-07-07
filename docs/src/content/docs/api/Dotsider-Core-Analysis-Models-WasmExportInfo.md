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

