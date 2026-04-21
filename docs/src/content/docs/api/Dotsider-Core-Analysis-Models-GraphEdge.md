---
title: "GraphEdge"
description: "A directed edge from a referencing assembly to a referenced assembly in the transitive dependency graph. Edges are retained for cycles and diamonds: revisiting an already-seen target identity emits a new edge but does not re-expand the target's subtree."
slug: api/dotsider.core.analysis.models.graphedge
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A directed edge from a referencing assembly to a referenced assembly in the transitive
dependency graph. Edges are retained for cycles and diamonds: revisiting an already-seen
target identity emits a new edge but does not re-expand the target's subtree.

```csharp
public sealed record GraphEdge : IEquatable<GraphEdge>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **GraphEdge**

## Implements

- [IEquatable\<GraphEdge\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### GraphEdge(string, string, int)

A directed edge from a referencing assembly to a referenced assembly in the transitive
dependency graph. Edges are retained for cycles and diamonds: revisiting an already-seen
target identity emits a new edge but does not re-expand the target's subtree.

**Parameters:**

- `SourceId` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The [Id](/api/dotsider.core.analysis.models.graphnode.id/) of the referencing assembly.
- `TargetId` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The [Id](/api/dotsider.core.analysis.models.graphnode.id/) of the referenced assembly.
- `TypeRefCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The number of TypeRef entries in the referencing assembly whose resolution scope resolves
to the exact full identity of the target (not merely its simple name). Zero when the target
is referenced by the AssemblyRef table but no TypeRefs are scoped to it.

```csharp
public GraphEdge(string SourceId, string TargetId, int TypeRefCount)
```

## Properties

### SourceId

The [Id](/api/dotsider.core.analysis.models.graphnode.id/) of the referencing assembly.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string SourceId { get; init; }
```

### TargetId

The [Id](/api/dotsider.core.analysis.models.graphnode.id/) of the referenced assembly.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string TargetId { get; init; }
```

### TypeRefCount

The number of TypeRef entries in the referencing assembly whose resolution scope resolves
to the exact full identity of the target (not merely its simple name). Zero when the target
is referenced by the AssemblyRef table but no TypeRefs are scoped to it.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int TypeRefCount { get; init; }
```

