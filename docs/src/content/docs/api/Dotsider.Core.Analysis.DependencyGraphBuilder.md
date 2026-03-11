---
title: "DependencyGraphBuilder"
description: "Builds a dependency graph from an assembly's references and type refs. Uses a hierarchical tree layout with the root assembly at top center."
slug: api/dotsider.core.analysis.dependencygraphbuilder
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

Builds a dependency graph from an assembly's references and type refs.
Uses a hierarchical tree layout with the root assembly at top center.

```csharp
public static class DependencyGraphBuilder
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **DependencyGraphBuilder**

## Methods

### Build(AssemblyAnalyzer)

Builds a graph of assembly references with positioned nodes and weighted edges.

**Parameters:**

- `analyzer` ([AssemblyAnalyzer](/api/dotsider.core.analysis.assemblyanalyzer/)): The assembly analyzer to read references from.

**Returns:** [ValueTuple\<GraphNode\>, GraphEdge\>\>](https://learn.microsoft.com/dotnet/api/system.valuetuple-2)

The graph nodes and edges for rendering.

```csharp
public static (IReadOnlyList<GraphNode> Nodes, IReadOnlyList<GraphEdge> Edges) Build(AssemblyAnalyzer analyzer)
```

