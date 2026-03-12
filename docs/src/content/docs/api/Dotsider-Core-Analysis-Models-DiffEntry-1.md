---
title: "DiffEntry<T>"
description: "A single diff entry wrapping an item from either side."
slug: api/dotsider.core.analysis.models.diffentry-1
sidebar:
  order: 1
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

