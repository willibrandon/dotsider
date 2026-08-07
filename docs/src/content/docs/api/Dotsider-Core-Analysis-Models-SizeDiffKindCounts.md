---
title: "SizeDiffKindCounts"
description: "Entry counts for one node kind in a size diff, split by direction. Grown and shrunk are the two signs of a changed entry; unchanged entries are counted here but never appear in the delta tree."
slug: api/dotsider.core.analysis.models.sizediffkindcounts
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

Entry counts for one node kind in a size diff, split by direction. Grown and shrunk are
the two signs of a changed entry; unchanged entries are counted here but never appear in
the delta tree.

```csharp
public sealed record SizeDiffKindCounts : IEquatable<SizeDiffKindCounts>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **SizeDiffKindCounts**

## Implements

- [IEquatable\<SizeDiffKindCounts\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### SizeDiffKindCounts(SizeNodeKind, int, int, int, int, int)

Entry counts for one node kind in a size diff, split by direction. Grown and shrunk are
the two signs of a changed entry; unchanged entries are counted here but never appear in
the delta tree.

**Parameters:**

- `Kind` ([SizeNodeKind](/api/dotsider.core.analysis.models.sizenodekind/)): The node kind the counts describe.
- `Added` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): Entries present only in the comparison build.
- `Removed` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): Entries present only in the baseline build.
- `Grown` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): Entries present in both builds whose size increased.
- `Shrunk` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): Entries present in both builds whose size decreased.
- `Unchanged` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): Entries present in both builds at the same size.

```csharp
public SizeDiffKindCounts(SizeNodeKind Kind, int Added, int Removed, int Grown, int Shrunk, int Unchanged)
```

## Properties

### Added

Entries present only in the comparison build.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Added { get; init; }
```

### Grown

Entries present in both builds whose size increased.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Grown { get; init; }
```

### Kind

The node kind the counts describe.

**Returns:** [SizeNodeKind](/api/dotsider.core.analysis.models.sizenodekind/)

```csharp
public SizeNodeKind Kind { get; init; }
```

### Removed

Entries present only in the baseline build.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Removed { get; init; }
```

### Shrunk

Entries present in both builds whose size decreased.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Shrunk { get; init; }
```

### Unchanged

Entries present in both builds at the same size.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Unchanged { get; init; }
```

## Methods

### Deconstruct(out SizeNodeKind, out int, out int, out int, out int, out int)

**Parameters:**

- `Kind` ([SizeNodeKind](/api/dotsider.core.analysis.models.sizenodekind/))
- `Added` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Removed` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Grown` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Shrunk` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `Unchanged` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))

```csharp
public void Deconstruct(out SizeNodeKind Kind, out int Added, out int Removed, out int Grown, out int Shrunk, out int Unchanged)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(SizeDiffKindCounts?)

**Parameters:**

- `other` ([SizeDiffKindCounts](/api/dotsider.core.analysis.models.sizediffkindcounts/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(SizeDiffKindCounts? other)
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

### operator !=(SizeDiffKindCounts?, SizeDiffKindCounts?)

**Parameters:**

- `left` ([SizeDiffKindCounts](/api/dotsider.core.analysis.models.sizediffkindcounts/))
- `right` ([SizeDiffKindCounts](/api/dotsider.core.analysis.models.sizediffkindcounts/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(SizeDiffKindCounts? left, SizeDiffKindCounts? right)
```

### operator ==(SizeDiffKindCounts?, SizeDiffKindCounts?)

**Parameters:**

- `left` ([SizeDiffKindCounts](/api/dotsider.core.analysis.models.sizediffkindcounts/))
- `right` ([SizeDiffKindCounts](/api/dotsider.core.analysis.models.sizediffkindcounts/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(SizeDiffKindCounts? left, SizeDiffKindCounts? right)
```
