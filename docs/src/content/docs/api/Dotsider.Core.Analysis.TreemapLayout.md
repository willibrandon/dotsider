---
title: "TreemapLayout"
description: "Squarified treemap layout algorithm. Produces rectangles with aspect ratios close to 1:1 for better readability."
slug: api/dotsider.core.analysis.treemaplayout
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

Squarified treemap layout algorithm.
Produces rectangles with aspect ratios close to 1:1 for better readability.

```csharp
public static class TreemapLayout
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **TreemapLayout**

## Methods

### Layout(IReadOnlyList\<SizeNode\>, double, double, double, double)

Computes a squarified treemap layout for the given nodes within the specified bounds.

**Parameters:**

- `nodes` ([IReadOnlyList\<SizeNode\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The size nodes to lay out.
- `x` ([Double](https://learn.microsoft.com/dotnet/api/system.double)): The left edge of the layout area.
- `y` ([Double](https://learn.microsoft.com/dotnet/api/system.double)): The top edge of the layout area.
- `width` ([Double](https://learn.microsoft.com/dotnet/api/system.double)): The width of the layout area.
- `height` ([Double](https://learn.microsoft.com/dotnet/api/system.double)): The height of the layout area.

**Returns:** [IReadOnlyList\<TreemapRect\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

A list of positioned rectangles for each node.

```csharp
public static IReadOnlyList<TreemapRect> Layout(IReadOnlyList<SizeNode> nodes, double x, double y, double width, double height)
```

