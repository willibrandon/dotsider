---
title: "WasmSectionInfo"
description: "One section in a WebAssembly module, including custom sections."
slug: api/dotsider.core.analysis.models.wasmsectioninfo
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One section in a WebAssembly module, including custom sections.

```csharp
public sealed record WasmSectionInfo : IEquatable<WasmSectionInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **WasmSectionInfo**

## Implements

- [IEquatable\<WasmSectionInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### WasmSectionInfo(byte, string, long, int)

One section in a WebAssembly module, including custom sections.

**Parameters:**

- `Id` ([Byte](https://learn.microsoft.com/dotnet/api/system.byte)): The numeric section id.
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The standard section name, or the custom section name for id 0.
- `FileOffset` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): The file offset where section payload bytes begin.
- `Size` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The section payload size in bytes.

```csharp
public WasmSectionInfo(byte Id, string Name, long FileOffset, int Size)
```

## Properties

### FileOffset

The file offset where section payload bytes begin.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long FileOffset { get; init; }
```

### Id

The numeric section id.

**Returns:** [Byte](https://learn.microsoft.com/dotnet/api/system.byte)

```csharp
public byte Id { get; init; }
```

### Name

The standard section name, or the custom section name for id 0.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### Size

The section payload size in bytes.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Size { get; init; }
```

