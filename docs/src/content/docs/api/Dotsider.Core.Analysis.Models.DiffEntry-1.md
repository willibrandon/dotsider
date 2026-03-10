---
title: "DiffEntry<T>"
description: "A single diff entry wrapping an item from either side."
slug: api/dotsider.core.analysis.models.diffentry-1
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

- `Kind` ([DiffKind](/api/dotsider.core.analysis.models.diffkind/)): 
- `Left` (\<T\>): 
- `Right` (\<T\>): 
- `ChangeDescription` ([String](https://learn.microsoft.com/dotnet/api/system.string)): 

```csharp
public DiffEntry(DiffKind Kind, T? Left, T? Right, string? ChangeDescription)
```

## Properties

### ChangeDescription

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? ChangeDescription { get; init; }
```

### Kind

**Returns:** [DiffKind](/api/dotsider.core.analysis.models.diffkind/)

```csharp
public DiffKind Kind { get; init; }
```

### Left

**Returns:** \<T\>

```csharp
public T? Left { get; init; }
```

### Right

**Returns:** \<T\>

```csharp
public T? Right { get; init; }
```

