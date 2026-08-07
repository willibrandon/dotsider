---
title: "CorrelationReportSymbol"
description: "One native symbol carrying a correlated method's compiled code, flattened for the programmatic surfaces (CLI, session, MCP) that report a correlation."
slug: api/dotsider.core.analysis.models.correlationreportsymbol
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One native symbol carrying a correlated method's compiled code, flattened for the
programmatic surfaces (CLI, session, MCP) that report a correlation.

```csharp
public sealed record CorrelationReportSymbol : IEquatable<CorrelationReportSymbol>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **CorrelationReportSymbol**

## Implements

- [IEquatable\<CorrelationReportSymbol\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### CorrelationReportSymbol(string, ulong, long?, long)

One native symbol carrying a correlated method's compiled code, flattened for the
programmatic surfaces (CLI, session, MCP) that report a correlation.

**Parameters:**

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The symbol's managed name when joined, otherwise its raw name.
- `VirtualAddress` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)): The symbol's virtual address.
- `FileOffset` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The file offset the code is backed by, or null when not file-backed.
- `Size` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): The symbol's size in bytes.

```csharp
public CorrelationReportSymbol(string Name, ulong VirtualAddress, long? FileOffset, long Size)
```

## Properties

### FileOffset

The file offset the code is backed by, or null when not file-backed.

**Returns:** [Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public long? FileOffset { get; init; }
```

### Name

The symbol's managed name when joined, otherwise its raw name.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### Size

The symbol's size in bytes.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long Size { get; init; }
```

### VirtualAddress

The symbol's virtual address.

**Returns:** [UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)

```csharp
public ulong VirtualAddress { get; init; }
```

## Methods

### Deconstruct(out string, out ulong, out long?, out long)

**Parameters:**

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `VirtualAddress` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64))
- `FileOffset` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `Size` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))

```csharp
public void Deconstruct(out string Name, out ulong VirtualAddress, out long? FileOffset, out long Size)
```

### Equals(CorrelationReportSymbol?)

**Parameters:**

- `other` ([CorrelationReportSymbol](/api/dotsider.core.analysis.models.correlationreportsymbol/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(CorrelationReportSymbol? other)
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

### operator !=(CorrelationReportSymbol?, CorrelationReportSymbol?)

**Parameters:**

- `left` ([CorrelationReportSymbol](/api/dotsider.core.analysis.models.correlationreportsymbol/))
- `right` ([CorrelationReportSymbol](/api/dotsider.core.analysis.models.correlationreportsymbol/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(CorrelationReportSymbol? left, CorrelationReportSymbol? right)
```

### operator ==(CorrelationReportSymbol?, CorrelationReportSymbol?)

**Parameters:**

- `left` ([CorrelationReportSymbol](/api/dotsider.core.analysis.models.correlationreportsymbol/))
- `right` ([CorrelationReportSymbol](/api/dotsider.core.analysis.models.correlationreportsymbol/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(CorrelationReportSymbol? left, CorrelationReportSymbol? right)
```
