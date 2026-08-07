---
title: "DiffEntry<T>"
description: "A single diff entry wrapping an item from either side."
slug: api/dotsider.core.analysis.models.diffentry-1
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A single diff entry wrapping an item from either side.

```csharp
public sealed record DiffEntry<T> : IEquatable<DiffEntry<T>>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **DiffEntry\<T\>**

## Implements

- [IEquatable\<DiffEntry`1\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### DiffEntry(DiffKind, T?, T?, string?)

A single diff entry wrapping an item from either side.

**Parameters:**

- `Kind` ([DiffKind](/api/dotsider.core.analysis.models.diffkind/)): Whether the item was added, removed, or changed.
- `Left` (\<T\>): The item from the left (baseline) assembly, or null if added.
- `Right` (\<T\>): The item from the right (updated) assembly, or null if removed.
- `ChangeDescription` ([String](https://learn.microsoft.com/dotnet/api/system.string)): A human-readable description of what changed, or null.

```csharp
public DiffEntry(DiffKind Kind, T? Left, T? Right, string? ChangeDescription)
```

## Properties

### ChangeDescription

A human-readable description of what changed, or null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? ChangeDescription { get; init; }
```

### Kind

Whether the item was added, removed, or changed.

**Returns:** [DiffKind](/api/dotsider.core.analysis.models.diffkind/)

```csharp
public DiffKind Kind { get; init; }
```

### Left

The item from the left (baseline) assembly, or null if added.

**Returns:** \<T\>

```csharp
public T? Left { get; init; }
```

### Right

The item from the right (updated) assembly, or null if removed.

**Returns:** \<T\>

```csharp
public T? Right { get; init; }
```

## Methods

### Deconstruct(out DiffKind, out T?, out T?, out string?)

**Parameters:**

- `Kind` ([DiffKind](/api/dotsider.core.analysis.models.diffkind/))
- `Left` (\<T\>)
- `Right` (\<T\>)
- `ChangeDescription` ([String](https://learn.microsoft.com/dotnet/api/system.string))

```csharp
public void Deconstruct(out DiffKind Kind, out T? Left, out T? Right, out string? ChangeDescription)
```

### Equals(DiffEntry\<T\>?)

**Parameters:**

- `other` ([DiffEntry`1](/api/dotsider.core.analysis.models.diffentry-1/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(DiffEntry<T>? other)
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

### operator !=(DiffEntry\<T\>?, DiffEntry\<T\>?)

**Parameters:**

- `left` ([DiffEntry`1](/api/dotsider.core.analysis.models.diffentry-1/))
- `right` ([DiffEntry`1](/api/dotsider.core.analysis.models.diffentry-1/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(DiffEntry<T>? left, DiffEntry<T>? right)
```

### operator ==(DiffEntry\<T\>?, DiffEntry\<T\>?)

**Parameters:**

- `left` ([DiffEntry`1](/api/dotsider.core.analysis.models.diffentry-1/))
- `right` ([DiffEntry`1](/api/dotsider.core.analysis.models.diffentry-1/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(DiffEntry<T>? left, DiffEntry<T>? right)
```
