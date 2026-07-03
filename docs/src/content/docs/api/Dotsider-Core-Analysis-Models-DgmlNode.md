---
title: "DgmlNode"
description: "One node of an ILC dependency graph. The label is the compiler's node name — the same string an mstat size entry stores as its NodeName, which is how the two files join."
slug: api/dotsider.core.analysis.models.dgmlnode
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One node of an ILC dependency graph. The label is the compiler's node name — the same
string an mstat size entry stores as its `NodeName`, which is how the two files join.

```csharp
public sealed record DgmlNode : IEquatable<DgmlNode>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **DgmlNode**

## Implements

- [IEquatable\<DgmlNode\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### DgmlNode(int, string)

One node of an ILC dependency graph. The label is the compiler's node name — the same
string an mstat size entry stores as its `NodeName`, which is how the two files join.

**Parameters:**

- `Id` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The node id, unique within the graph.
- `Label` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The node name.

```csharp
public DgmlNode(int Id, string Label)
```

## Properties

### Id

The node id, unique within the graph.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Id { get; init; }
```

### Label

The node name.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Label { get; init; }
```

