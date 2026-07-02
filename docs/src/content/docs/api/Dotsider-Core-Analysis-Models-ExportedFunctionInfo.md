---
title: "ExportedFunctionInfo"
description: "A single entry in the PE export table."
slug: api/dotsider.core.analysis.models.exportedfunctioninfo
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A single entry in the PE export table.

```csharp
public sealed record ExportedFunctionInfo : IEquatable<ExportedFunctionInfo>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **ExportedFunctionInfo**

## Implements

- [IEquatable\<ExportedFunctionInfo\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### ExportedFunctionInfo(int, string?, int, string?)

A single entry in the PE export table.

**Parameters:**

- `Ordinal` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The biased export ordinal (ordinal base applied).
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The exported name, or null for ordinal-only exports.
- `Rva` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The RVA of the exported symbol, or of the forwarder string.
- `ForwardedTo` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The forwarder target (e.g. "NTDLL.RtlAllocateHeap") when the export forwards to
another module, or null for regular exports.

```csharp
public ExportedFunctionInfo(int Ordinal, string? Name, int Rva, string? ForwardedTo)
```

## Properties

### ForwardedTo

The forwarder target (e.g. "NTDLL.RtlAllocateHeap") when the export forwards to
another module, or null for regular exports.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? ForwardedTo { get; init; }
```

### Name

The exported name, or null for ordinal-only exports.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Name { get; init; }
```

### Ordinal

The biased export ordinal (ordinal base applied).

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Ordinal { get; init; }
```

### Rva

The RVA of the exported symbol, or of the forwarder string.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Rva { get; init; }
```

