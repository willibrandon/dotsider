---
title: "DependencyGraphResult"
description: "The result of building a transitive assembly dependency graph. Contains the public topology consumed by serializers (Nodes, Edges) and the internal navigation metadata consumed by the TUI (NavigationById)."
slug: api/dotsider.core.analysis.models.dependencygraphresult
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The result of building a transitive assembly dependency graph. Contains the public topology
consumed by serializers ([Nodes](/api/dotsider.core.analysis.models.dependencygraphresult.nodes/), [Edges](/api/dotsider.core.analysis.models.dependencygraphresult.edges/)) and the internal navigation
metadata consumed by the TUI ([NavigationById](/api/dotsider.core.analysis.models.dependencygraphresult.navigationbyid/)).

```csharp
public sealed record DependencyGraphResult : IEquatable<DependencyGraphResult>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **DependencyGraphResult**

## Implements

- [IEquatable\<DependencyGraphResult\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### DependencyGraphResult(IReadOnlyList\<GraphNode\>, IReadOnlyList\<GraphEdge\>, IReadOnlyDictionary\<string, GraphNavigationContext\>)

The result of building a transitive assembly dependency graph. Contains the public topology
consumed by serializers ([Nodes](/api/dotsider.core.analysis.models.dependencygraphresult.nodes/), [Edges](/api/dotsider.core.analysis.models.dependencygraphresult.edges/)) and the internal navigation
metadata consumed by the TUI ([NavigationById](/api/dotsider.core.analysis.models.dependencygraphresult.navigationbyid/)).

**Parameters:**

- `Nodes` ([IReadOnlyList\<GraphNode\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): All nodes in the graph including the root and any unresolved or identity-mismatched leaves,
each carrying its computed layout coordinates and depth.
- `Edges` ([IReadOnlyList\<GraphEdge\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Directed edges from each referencing assembly to every assembly it references. Edges for
cycles and diamonds are preserved; a child identity revisited through a second parent emits
a new edge but does not re-expand the subtree.
- `NavigationById` ([IReadOnlyDictionary\<String, GraphNavigationContext\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)): Per-node navigation metadata keyed by [Id](/api/dotsider.core.analysis.models.graphnode.id/). Intended for in-process TUI
use only — consumers that serialize graph topology (CLI JSON, diagnostics UDS, MCP tools) must
ignore this dictionary to avoid leaking machine-local paths through their public contracts.

```csharp
public DependencyGraphResult(IReadOnlyList<GraphNode> Nodes, IReadOnlyList<GraphEdge> Edges, IReadOnlyDictionary<string, GraphNavigationContext> NavigationById)
```

## Properties

### Edges

Directed edges from each referencing assembly to every assembly it references. Edges for
cycles and diamonds are preserved; a child identity revisited through a second parent emits
a new edge but does not re-expand the subtree.

**Returns:** [IReadOnlyList\<GraphEdge\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<GraphEdge> Edges { get; init; }
```

### NavigationById

Per-node navigation metadata keyed by [Id](/api/dotsider.core.analysis.models.graphnode.id/). Intended for in-process TUI
use only — consumers that serialize graph topology (CLI JSON, diagnostics UDS, MCP tools) must
ignore this dictionary to avoid leaking machine-local paths through their public contracts.

**Returns:** [IReadOnlyDictionary\<String, GraphNavigationContext\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)

```csharp
public IReadOnlyDictionary<string, GraphNavigationContext> NavigationById { get; init; }
```

### Nodes

All nodes in the graph including the root and any unresolved or identity-mismatched leaves,
each carrying its computed layout coordinates and depth.

**Returns:** [IReadOnlyList\<GraphNode\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<GraphNode> Nodes { get; init; }
```

## Methods

### Deconstruct(out IReadOnlyList\<GraphNode\>, out IReadOnlyList\<GraphEdge\>, out IReadOnlyDictionary\<string, GraphNavigationContext\>)

**Parameters:**

- `Nodes` ([IReadOnlyList\<GraphNode\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `Edges` ([IReadOnlyList\<GraphEdge\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `NavigationById` ([IReadOnlyDictionary\<String, GraphNavigationContext\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2))

```csharp
public void Deconstruct(out IReadOnlyList<GraphNode> Nodes, out IReadOnlyList<GraphEdge> Edges, out IReadOnlyDictionary<string, GraphNavigationContext> NavigationById)
```

### Equals(DependencyGraphResult?)

**Parameters:**

- `other` ([DependencyGraphResult](/api/dotsider.core.analysis.models.dependencygraphresult/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(DependencyGraphResult? other)
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

### operator !=(DependencyGraphResult?, DependencyGraphResult?)

**Parameters:**

- `left` ([DependencyGraphResult](/api/dotsider.core.analysis.models.dependencygraphresult/))
- `right` ([DependencyGraphResult](/api/dotsider.core.analysis.models.dependencygraphresult/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(DependencyGraphResult? left, DependencyGraphResult? right)
```

### operator ==(DependencyGraphResult?, DependencyGraphResult?)

**Parameters:**

- `left` ([DependencyGraphResult](/api/dotsider.core.analysis.models.dependencygraphresult/))
- `right` ([DependencyGraphResult](/api/dotsider.core.analysis.models.dependencygraphresult/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(DependencyGraphResult? left, DependencyGraphResult? right)
```
