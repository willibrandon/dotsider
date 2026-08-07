---
title: "ReadyToRunCodeRange"
description: "One contiguous block of a precompiled ReadyToRun method's native code, derived from a single runtime function. A method body is not one slice: it is the ordered list of these ranges (hot entry, funclets, cold), each of which is disassembled, sized, and navigated on its own."
slug: api/dotsider.core.analysis.models.readytoruncoderange
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One contiguous block of a precompiled ReadyToRun method's native code, derived from a single
runtime function. A method body is not one slice: it is the ordered list of these ranges
(hot entry, funclets, cold), each of which is disassembled, sized, and navigated on its own.

```csharp
public sealed record ReadyToRunCodeRange : IEquatable<ReadyToRunCodeRange>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **ReadyToRunCodeRange**

## Implements

- [IEquatable\<ReadyToRunCodeRange\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### ReadyToRunCodeRange(ReadyToRunCodeRangeKind, int, long, ulong, int?)

One contiguous block of a precompiled ReadyToRun method's native code, derived from a single
runtime function. A method body is not one slice: it is the ordered list of these ranges
(hot entry, funclets, cold), each of which is disassembled, sized, and navigated on its own.

**Parameters:**

- `Kind` ([ReadyToRunCodeRangeKind](/api/dotsider.core.analysis.models.readytoruncoderangekind/)): Whether this range is the hot entry, a funclet, or the cold range.
- `StartRva` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The range's relative virtual address (machine-specific fixups already applied).
- `Size` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): The range size in bytes.
- `VirtualAddress` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)): The range's absolute virtual address (image base + StartRva) in its code image.
- `FileOffset` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The file offset of the range within its code image, or null when not file-backed.

```csharp
public ReadyToRunCodeRange(ReadyToRunCodeRangeKind Kind, int StartRva, long Size, ulong VirtualAddress, int? FileOffset)
```

## Properties

### FileOffset

The file offset of the range within its code image, or null when not file-backed.

**Returns:** [Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public int? FileOffset { get; init; }
```

### Kind

Whether this range is the hot entry, a funclet, or the cold range.

**Returns:** [ReadyToRunCodeRangeKind](/api/dotsider.core.analysis.models.readytoruncoderangekind/)

```csharp
public ReadyToRunCodeRangeKind Kind { get; init; }
```

### Size

The range size in bytes.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long Size { get; init; }
```

### StartRva

The range's relative virtual address (machine-specific fixups already applied).

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int StartRva { get; init; }
```

### VirtualAddress

The range's absolute virtual address (image base + StartRva) in its code image.

**Returns:** [UInt64](https://learn.microsoft.com/dotnet/api/system.uint64)

```csharp
public ulong VirtualAddress { get; init; }
```

## Methods

### Deconstruct(out ReadyToRunCodeRangeKind, out int, out long, out ulong, out int?)

**Parameters:**

- `Kind` ([ReadyToRunCodeRangeKind](/api/dotsider.core.analysis.models.readytoruncoderangekind/))
- `StartRva` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Size` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `VirtualAddress` ([UInt64](https://learn.microsoft.com/dotnet/api/system.uint64))
- `FileOffset` ([Nullable\<Int32\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))

```csharp
public void Deconstruct(out ReadyToRunCodeRangeKind Kind, out int StartRva, out long Size, out ulong VirtualAddress, out int? FileOffset)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(ReadyToRunCodeRange?)

**Parameters:**

- `other` ([ReadyToRunCodeRange](/api/dotsider.core.analysis.models.readytoruncoderange/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(ReadyToRunCodeRange? other)
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

### operator !=(ReadyToRunCodeRange?, ReadyToRunCodeRange?)

**Parameters:**

- `left` ([ReadyToRunCodeRange](/api/dotsider.core.analysis.models.readytoruncoderange/))
- `right` ([ReadyToRunCodeRange](/api/dotsider.core.analysis.models.readytoruncoderange/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(ReadyToRunCodeRange? left, ReadyToRunCodeRange? right)
```

### operator ==(ReadyToRunCodeRange?, ReadyToRunCodeRange?)

**Parameters:**

- `left` ([ReadyToRunCodeRange](/api/dotsider.core.analysis.models.readytoruncoderange/))
- `right` ([ReadyToRunCodeRange](/api/dotsider.core.analysis.models.readytoruncoderange/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(ReadyToRunCodeRange? left, ReadyToRunCodeRange? right)
```
