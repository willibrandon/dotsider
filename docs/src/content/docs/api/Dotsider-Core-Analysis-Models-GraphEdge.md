---
title: "GraphEdge"
description: "A directed edge from a referencing assembly to a referenced assembly in the transitive dependency graph. Edges are retained for cycles and diamonds: revisiting an already-seen target identity emits a new edge but does not re-expand the target's subtree."
slug: api/dotsider.core.analysis.models.graphedge
sidebar:
  order: 2
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

### GraphEdge(string, string, int, AssemblyRefInfo?)

A directed edge from a referencing assembly to a referenced assembly in the transitive
dependency graph. Edges are retained for cycles and diamonds: revisiting an already-seen
target identity emits a new edge but does not re-expand the target's subtree.

**Parameters:**

- `SourceId` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The [Id](/api/dotsider.core.analysis.models.graphnode.id/) of the referencing assembly.
- `TargetId` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The [Id](/api/dotsider.core.analysis.models.graphnode.id/) of the referenced assembly.
- `TypeRefCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The number of TypeRef entries in the referencing assembly whose resolution scope resolves
to the exact full identity of the target (not merely its simple name). Zero when the target
is referenced by the AssemblyRef table but no TypeRefs are scoped to it.
- `RequestedIdentity` ([AssemblyRefInfo](/api/dotsider.core.analysis.models.assemblyrefinfo/)): The identity exactly as it appeared in the referencing assembly's AssemblyRef metadata,
before any .NET Framework binding policy was applied. May differ from the target node's
identity when the target was keyed on the bound identity (e.g., two AssemblyRefs at
different versions both redirected to the same loaded version collapse onto a single
target node, but each edge preserves its own pre-redirect requested identity here).
null when there was no policy rewrite — the requested identity is the
same as the target node's identity in that case.

```csharp
public GraphEdge(string SourceId, string TargetId, int TypeRefCount, AssemblyRefInfo? RequestedIdentity = null)
```

## Properties

### RequestedIdentity

The identity exactly as it appeared in the referencing assembly's AssemblyRef metadata,
before any .NET Framework binding policy was applied. May differ from the target node's
identity when the target was keyed on the bound identity (e.g., two AssemblyRefs at
different versions both redirected to the same loaded version collapse onto a single
target node, but each edge preserves its own pre-redirect requested identity here).
null when there was no policy rewrite — the requested identity is the
same as the target node's identity in that case.

**Returns:** [AssemblyRefInfo](/api/dotsider.core.analysis.models.assemblyrefinfo/)

```csharp
public AssemblyRefInfo? RequestedIdentity { get; init; }
```

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

