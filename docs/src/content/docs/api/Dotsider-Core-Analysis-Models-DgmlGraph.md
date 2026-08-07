---
title: "DgmlGraph"
description: "An ILC dependency graph read from a DGML file, with the reverse index needed to answer \"why is this in my binary\": a breadth-first walk from any node toward its dependers ends at a root — a node nothing depends on — and the chain back down is the explanation."
slug: api/dotsider.core.analysis.models.dgmlgraph
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

An ILC dependency graph read from a DGML file, with the reverse index needed to answer
"why is this in my binary": a breadth-first walk from any node toward its dependers ends
at a root — a node nothing depends on — and the chain back down is the explanation.

```csharp
public sealed class DgmlGraph
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **DgmlGraph**

## Properties

### Links

The graph's edges; each source depends on its target.

**Returns:** [IReadOnlyList\<DgmlLink\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<DgmlLink> Links { get; }
```

### Nodes

The graph's nodes.

**Returns:** [IReadOnlyList\<DgmlNode\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<DgmlNode> Nodes { get; }
```

## Methods

### FindNodeByLabel(string)

Finds the node with the given label, or null when no node carries it. When labels
repeat, the first node wins.

**Parameters:**

- `label` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The node name to look up — for an mstat entry, its `NodeName`.

**Returns:** [DgmlNode](/api/dotsider.core.analysis.models.dgmlnode/)

```csharp
public DgmlNode? FindNodeByLabel(string label)
```

### PathToRoot(int)

Walks from the node to a root and returns the chain root-first, ending at the queried
node. Empty when the id is unknown.

**Parameters:**

- `nodeId` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The node id to explain.

**Returns:** [IReadOnlyList\<DgmlPathStep\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<DgmlPathStep> PathToRoot(int nodeId)
```

### PathToRoot(string)

Walks from the labeled node to a root and returns the chain root-first, ending at the
queried node. Empty when the label is unknown.

**Parameters:**

- `label` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The node name to explain.

**Returns:** [IReadOnlyList\<DgmlPathStep\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<DgmlPathStep> PathToRoot(string label)
```
