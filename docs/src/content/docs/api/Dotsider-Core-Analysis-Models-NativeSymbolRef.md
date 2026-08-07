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
public readonly struct NativeSymbolRef : IEquatable<NativeSymbolRef>
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

## Methods

### Deconstruct(out ulong, out string, out NativeSymbolKind, out long)

**Parameters:**

- `Start` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64))
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Kind` ([NativeSymbolKind](/api/dotsider.core.analysis.models.nativesymbolkind/))
- `Offset` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))

```csharp
public void Deconstruct(out ulong Start, out string Name, out NativeSymbolKind Kind, out long Offset)
```

### Equals(NativeSymbolRef)

**Parameters:**

- `other` ([NativeSymbolRef](/api/dotsider.core.analysis.models.nativesymbolref/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(NativeSymbolRef other)
```

### Equals(object)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object obj)
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

### operator !=(NativeSymbolRef, NativeSymbolRef)

**Parameters:**

- `left` ([NativeSymbolRef](/api/dotsider.core.analysis.models.nativesymbolref/))
- `right` ([NativeSymbolRef](/api/dotsider.core.analysis.models.nativesymbolref/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(NativeSymbolRef left, NativeSymbolRef right)
```

### operator ==(NativeSymbolRef, NativeSymbolRef)

**Parameters:**

- `left` ([NativeSymbolRef](/api/dotsider.core.analysis.models.nativesymbolref/))
- `right` ([NativeSymbolRef](/api/dotsider.core.analysis.models.nativesymbolref/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(NativeSymbolRef left, NativeSymbolRef right)
```
