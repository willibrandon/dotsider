---
title: "DgmlLink"
description: "One edge of an ILC dependency graph: the source node depends on the target node, so the target is in the binary because the source needed it."
slug: api/dotsider.core.analysis.models.dgmllink
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One edge of an ILC dependency graph: the source node depends on the target node, so the
target is in the binary because the source needed it.

```csharp
public sealed record DgmlLink : IEquatable<DgmlLink>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **DgmlLink**

## Implements

- [IEquatable\<DgmlLink\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### DgmlLink(int, int, string?)

One edge of an ILC dependency graph: the source node depends on the target node, so the
target is in the binary because the source needed it.

**Parameters:**

- `SourceId` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The depender's node id.
- `TargetId` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The dependee's node id.
- `Reason` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The compiler's explanation for the dependency, or null when it gave none.

```csharp
public DgmlLink(int SourceId, int TargetId, string? Reason)
```

## Properties

### Reason

The compiler's explanation for the dependency, or null when it gave none.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Reason { get; init; }
```

### SourceId

The depender's node id.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int SourceId { get; init; }
```

### TargetId

The dependee's node id.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int TargetId { get; init; }
```

