---
title: "ExportedFunctionInfo"
description: "A single entry in the PE export table."
slug: api/dotsider.core.analysis.models.exportedfunctioninfo
sidebar:
  order: 2
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

## Methods

### Deconstruct(out int, out string?, out int, out string?)

**Parameters:**

- `Ordinal` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Rva` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `ForwardedTo` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out int Ordinal, out string? Name, out int Rva, out string? ForwardedTo)
```

### Equals(ExportedFunctionInfo?)

**Parameters:**

- `other` ([ExportedFunctionInfo](/api/dotsider.core.analysis.models.exportedfunctioninfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(ExportedFunctionInfo? other)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
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

### operator !=(ExportedFunctionInfo?, ExportedFunctionInfo?)

**Parameters:**

- `left` ([ExportedFunctionInfo](/api/dotsider.core.analysis.models.exportedfunctioninfo/))
- `right` ([ExportedFunctionInfo](/api/dotsider.core.analysis.models.exportedfunctioninfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(ExportedFunctionInfo? left, ExportedFunctionInfo? right)
```

### operator ==(ExportedFunctionInfo?, ExportedFunctionInfo?)

**Parameters:**

- `left` ([ExportedFunctionInfo](/api/dotsider.core.analysis.models.exportedfunctioninfo/))
- `right` ([ExportedFunctionInfo](/api/dotsider.core.analysis.models.exportedfunctioninfo/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(ExportedFunctionInfo? left, ExportedFunctionInfo? right)
```
