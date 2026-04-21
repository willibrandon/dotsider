---
title: "GraphNode"
description: "A node in the transitive assembly dependency graph. Topology only — layout coordinates and rendered labels are the responsibility of the view layer, which projects the visible subgraph into a separate render model so filters and viewport changes rebalance without perturbing this record."
slug: api/dotsider.core.analysis.models.graphnode
sidebar:
  order: 1
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A node in the transitive assembly dependency graph. Topology only — layout coordinates
and rendered labels are the responsibility of the view layer, which projects the visible
subgraph into a separate render model so filters and viewport changes rebalance without
perturbing this record.

```csharp
public sealed record GraphNode : IEquatable<GraphNode>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **GraphNode**

## Implements

- [IEquatable\<GraphNode\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### GraphNode(string, string, string?, string, string?, bool, int, bool)

A node in the transitive assembly dependency graph. Topology only — layout coordinates
and rendered labels are the responsibility of the view layer, which projects the visible
subgraph into a separate render model so filters and viewport changes rebalance without
perturbing this record.

**Parameters:**

- `Id` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Stable opaque identifier for this node, derived from the full assembly identity
([Name](/api/dotsider.core.analysis.models.graphnode.name/), [Version](/api/dotsider.core.analysis.models.graphnode.version/), [Culture](/api/dotsider.core.analysis.models.graphnode.culture/), [PublicKeyToken](/api/dotsider.core.analysis.models.graphnode.publickeytoken/))
via String). Two
assemblies that share a simple name but differ in any identity field produce distinct ids.
- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Assembly simple name.
- `Version` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Assembly version string, or null when unavailable.
- `Culture` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Assembly culture, or `"neutral"` for culture-neutral assemblies. Never empty.
- `PublicKeyToken` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Public key token hex string, or null.
- `IsRoot` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): Whether this is the analyzed assembly (the root of the graph).
- `Depth` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The minimum number of AssemblyRef hops from the root to this node as discovered by BFS.
Zero for the root; one for direct references; greater for transitive references.
- `Unresolved` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): Whether this node is a leaf that could not be resolved. Includes both the case where no
probe produced any candidate and the case where a probe produced a simple-name match whose
manifest identity did not match — the latter is further distinguished by the node's
navigation-context provenance.

```csharp
public GraphNode(string Id, string Name, string? Version, string Culture, string? PublicKeyToken, bool IsRoot, int Depth, bool Unresolved)
```

## Properties

### Culture

Assembly culture, or `"neutral"` for culture-neutral assemblies. Never empty.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Culture { get; init; }
```

### Depth

The minimum number of AssemblyRef hops from the root to this node as discovered by BFS.
Zero for the root; one for direct references; greater for transitive references.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int Depth { get; init; }
```

### Id

Stable opaque identifier for this node, derived from the full assembly identity
([Name](/api/dotsider.core.analysis.models.graphnode.name/), [Version](/api/dotsider.core.analysis.models.graphnode.version/), [Culture](/api/dotsider.core.analysis.models.graphnode.culture/), [PublicKeyToken](/api/dotsider.core.analysis.models.graphnode.publickeytoken/))
via String). Two
assemblies that share a simple name but differ in any identity field produce distinct ids.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Id { get; init; }
```

### IsRoot

Whether this is the analyzed assembly (the root of the graph).

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool IsRoot { get; init; }
```

### Name

Assembly simple name.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### PublicKeyToken

Public key token hex string, or null.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? PublicKeyToken { get; init; }
```

### Unresolved

Whether this node is a leaf that could not be resolved. Includes both the case where no
probe produced any candidate and the case where a probe produced a simple-name match whose
manifest identity did not match — the latter is further distinguished by the node's
navigation-context provenance.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Unresolved { get; init; }
```

### Version

Assembly version string, or null when unavailable.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string? Version { get; init; }
```

