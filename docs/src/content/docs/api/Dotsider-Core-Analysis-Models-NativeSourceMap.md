---
title: "NativeSourceMap"
description: "An address-sorted map from virtual address to source file and line, aggregated from a native binary's debug sidecar. Int32%40) resolves an instruction address to its source location the way NativeSymbol%40) resolves an address to a symbol, letting the disassembler annotate the listing with // file:line where the sidecar has data."
slug: api/dotsider.core.analysis.models.nativesourcemap
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

An address-sorted map from virtual address to source file and line, aggregated from a native
binary's debug sidecar. Int32%40) resolves an instruction address to its source
location the way NativeSymbol%40) resolves an address to a symbol,
letting the disassembler annotate the listing with `// file:line` where the sidecar has data.

```csharp
public sealed record NativeSourceMap : IEquatable<NativeSourceMap>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **NativeSourceMap**

## Implements

- [IEquatable\<NativeSourceMap\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### NativeSourceMap(IReadOnlyList\<NativeSourceLine\>)

An address-sorted map from virtual address to source file and line, aggregated from a native
binary's debug sidecar. Int32%40) resolves an instruction address to its source
location the way NativeSymbol%40) resolves an address to a symbol,
letting the disassembler annotate the listing with `// file:line` where the sidecar has data.

**Parameters:**

- `Lines` ([IReadOnlyList\<NativeSourceLine\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The source rows, sorted ascending by [Address](/api/dotsider.core.analysis.models.nativesourceline.address/).

```csharp
public NativeSourceMap(IReadOnlyList<NativeSourceLine> Lines)
```

## Properties

### Lines

The source rows, sorted ascending by [Address](/api/dotsider.core.analysis.models.nativesourceline.address/).

**Returns:** [IReadOnlyList\<NativeSourceLine\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<NativeSourceLine> Lines { get; init; }
```

## Methods

### Deconstruct(out IReadOnlyList\<NativeSourceLine\>)

**Parameters:**

- `Lines` ([IReadOnlyList\<NativeSourceLine\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public void Deconstruct(out IReadOnlyList<NativeSourceLine> Lines)
```

### Equals(NativeSourceMap?)

**Parameters:**

- `other` ([NativeSourceMap](/api/dotsider.core.analysis.models.nativesourcemap/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(NativeSourceMap? other)
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

### TryGetLine(ulong, out string, out int)

Resolves a virtual address to its source file and 1-based line, if mapped.

**Parameters:**

- `virtualAddress` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)): The instruction virtual address.
- `file` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The source file when found.
- `line` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The 1-based source line when found.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

True when the address falls within a mapped row; otherwise false.

```csharp
public bool TryGetLine(ulong virtualAddress, out string file, out int line)
```

## Members

### operator !=(NativeSourceMap?, NativeSourceMap?)

**Parameters:**

- `left` ([NativeSourceMap](/api/dotsider.core.analysis.models.nativesourcemap/))
- `right` ([NativeSourceMap](/api/dotsider.core.analysis.models.nativesourcemap/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(NativeSourceMap? left, NativeSourceMap? right)
```

### operator ==(NativeSourceMap?, NativeSourceMap?)

**Parameters:**

- `left` ([NativeSourceMap](/api/dotsider.core.analysis.models.nativesourcemap/))
- `right` ([NativeSourceMap](/api/dotsider.core.analysis.models.nativesourcemap/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(NativeSourceMap? left, NativeSourceMap? right)
```
