---
title: "NativeSymbolRef"
description: "A resolved reference to the symbol containing a target address, with the offset into it — so a call or branch landing inside a function displays honestly as Foo+0x12 rather than failing or pretending an exact hit."
slug: api/dotsider.core.analysis.models.nativesymbolref
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A resolved reference to the symbol containing a target address, with the offset into it — so a
call or branch landing inside a function displays honestly as `Foo+0x12` rather than
failing or pretending an exact hit.

```csharp
public readonly record struct NativeSymbolRef : IEquatable<NativeSymbolRef>
```

## Implements

- [IEquatable\<NativeSymbolRef\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### NativeSymbolRef(ulong, string, NativeSymbolKind, long)

A resolved reference to the symbol containing a target address, with the offset into it — so a
call or branch landing inside a function displays honestly as `Foo+0x12` rather than
failing or pretending an exact hit.

**Parameters:**

- `Start` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)): The containing symbol's start virtual address.
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The symbol's display name (managed name where available, else the raw name).
- `Kind` ([NativeSymbolKind](/api/dotsider.core.analysis.models.nativesymbolkind/)): The symbol's kind.
- `Offset` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): The target's byte offset from Start (0 when it is the symbol's entry).

```csharp
public NativeSymbolRef(ulong Start, string Name, NativeSymbolKind Kind, long Offset)
```

## Properties

### Kind

The symbol's kind.

**Returns:** [NativeSymbolKind](/api/dotsider.core.analysis.models.nativesymbolkind/)

```csharp
public NativeSymbolKind Kind { get; init; }
```

### Name

The symbol's display name (managed name where available, else the raw name).

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### Offset

The target's byte offset from Start (0 when it is the symbol's entry).

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long Offset { get; init; }
```

### Start

The containing symbol's start virtual address.

**Returns:** [UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)

```csharp
public ulong Start { get; init; }
```

