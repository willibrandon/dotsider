---
title: "WasmImportInfo"
description: "One WebAssembly import entry."
slug: api/dotsider.core.analysis.models.wasmimportinfo
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One WebAssembly import entry.

```csharp
public sealed record WasmImportInfo : IEquatable<WasmImportInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **WasmImportInfo**

## Implements

- [IEquatable\<WasmImportInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### WasmImportInfo(string, string, WasmExternalKind, int, int?)

One WebAssembly import entry.

**Parameters:**

- `ModuleName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The imported module name.
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The imported item name.
- `Kind` ([WasmExternalKind](/api/dotsider.core.analysis.models.wasmexternalkind/)): The imported external kind.
- `Index` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The import's index within its index space when applicable.
- `TypeIndex` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The function type index for function imports, or null.

```csharp
public WasmImportInfo(string ModuleName, string Name, WasmExternalKind Kind, int Index, int? TypeIndex)
```

## Properties

### Index

The import's index within its index space when applicable.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Index { get; init; }
```

### Kind

The imported external kind.

**Returns:** [WasmExternalKind](/api/dotsider.core.analysis.models.wasmexternalkind/)

```csharp
public WasmExternalKind Kind { get; init; }
```

### ModuleName

The imported module name.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string ModuleName { get; init; }
```

### Name

The imported item name.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### TypeIndex

The function type index for function imports, or null.

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? TypeIndex { get; init; }
```

