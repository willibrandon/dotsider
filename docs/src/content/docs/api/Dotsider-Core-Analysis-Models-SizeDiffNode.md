---
title: "SizeDiffNode"
description: "A node in the hierarchical size-difference tree between two Native AOT builds. The tree contains changed subtrees only — added, removed, grown, and shrunk entries; unchanged mass is summarized in SizeDiffSummary instead of carried as zero-delta nodes."
slug: api/dotsider.core.analysis.models.sizediffnode
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

A node in the hierarchical size-difference tree between two Native AOT builds. The tree
contains changed subtrees only — added, removed, grown, and shrunk entries; unchanged mass
is summarized in [SizeDiffSummary](/api/dotsider.core.analysis.models.sizediffsummary/) instead of carried as zero-delta nodes.

```csharp
public sealed record SizeDiffNode : IEquatable<SizeDiffNode>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **SizeDiffNode**

## Implements

- [IEquatable\<SizeDiffNode\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### SizeDiffNode(string, string, SizeNodeKind, DiffKind, long, long, long, IReadOnlyList\<SizeDiffNode\>, int, int, IReadOnlyList\<string\>, IReadOnlyList\<string\>)

A node in the hierarchical size-difference tree between two Native AOT builds. The tree
contains changed subtrees only — added, removed, grown, and shrunk entries; unchanged mass
is summarized in [SizeDiffSummary](/api/dotsider.core.analysis.models.sizediffsummary/) instead of carried as zero-delta nodes.

**Parameters:**

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Display name for this node. Method leaves carry their parameter list so overloads stay distinct.
- `FullPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): A deterministic path from the root, unique within the tree.
- `Kind` ([SizeNodeKind](/api/dotsider.core.analysis.models.sizenodekind/)): The granularity level of this node, in [SizeNode](/api/dotsider.core.analysis.models.sizenode/) terms.
- `Diff` ([DiffKind](/api/dotsider.core.analysis.models.diffkind/)): The direction of the difference: [Added](/api/dotsider.core.analysis.models.diffkind.added/) or [Removed](/api/dotsider.core.analysis.models.diffkind.removed/)
when the whole subtree exists on one side only, otherwise [Changed](/api/dotsider.core.analysis.models.diffkind.changed/) —
grown when [Delta](/api/dotsider.core.analysis.models.sizediffnode.delta/) is positive, shrunk when negative.
- `LeftSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): The bytes attributed on the baseline side (changed entries only for interior nodes).
- `RightSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): The bytes attributed on the comparison side (changed entries only for interior nodes).
- `Delta` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): [RightSize](/api/dotsider.core.analysis.models.sizediffnode.rightsize/) minus [LeftSize](/api/dotsider.core.analysis.models.sizediffnode.leftsize/).
- `Children` ([IReadOnlyList\<SizeDiffNode\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Child nodes ordered by absolute delta, largest first.
- `LeftEntryCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The number of raw baseline report rows behind this node. Greater than one on a leaf means
the leaf is an aggregate (display collisions, frozen objects grouped by owner) and is
rendered as such.
- `RightEntryCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The number of raw comparison-side report rows behind this node.
- `LeftNodeNames` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Every dependency-graph node name behind the baseline rows. An aggregate maps to many DGML
nodes; keeping the full list keeps "why is this in my binary" answers honest.
- `RightNodeNames` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Every dependency-graph node name behind the comparison-side rows.

```csharp
public SizeDiffNode(string Name, string FullPath, SizeNodeKind Kind, DiffKind Diff, long LeftSize, long RightSize, long Delta, IReadOnlyList<SizeDiffNode> Children, int LeftEntryCount, int RightEntryCount, IReadOnlyList<string> LeftNodeNames, IReadOnlyList<string> RightNodeNames)
```

## Properties

### Children

Child nodes ordered by absolute delta, largest first.

**Returns:** [IReadOnlyList\<SizeDiffNode\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<SizeDiffNode> Children { get; init; }
```

### Delta

[RightSize](/api/dotsider.core.analysis.models.sizediffnode.rightsize/) minus [LeftSize](/api/dotsider.core.analysis.models.sizediffnode.leftsize/).

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long Delta { get; init; }
```

### Diff

The direction of the difference: [Added](/api/dotsider.core.analysis.models.diffkind.added/) or [Removed](/api/dotsider.core.analysis.models.diffkind.removed/)
when the whole subtree exists on one side only, otherwise [Changed](/api/dotsider.core.analysis.models.diffkind.changed/) —
grown when [Delta](/api/dotsider.core.analysis.models.sizediffnode.delta/) is positive, shrunk when negative.

**Returns:** [DiffKind](/api/dotsider.core.analysis.models.diffkind/)

```csharp
public DiffKind Diff { get; init; }
```

### FullPath

A deterministic path from the root, unique within the tree.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string FullPath { get; init; }
```

### Kind

The granularity level of this node, in [SizeNode](/api/dotsider.core.analysis.models.sizenode/) terms.

**Returns:** [SizeNodeKind](/api/dotsider.core.analysis.models.sizenodekind/)

```csharp
public SizeNodeKind Kind { get; init; }
```

### LeftEntryCount

The number of raw baseline report rows behind this node. Greater than one on a leaf means
the leaf is an aggregate (display collisions, frozen objects grouped by owner) and is
rendered as such.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int LeftEntryCount { get; init; }
```

### LeftNodeNames

Every dependency-graph node name behind the baseline rows. An aggregate maps to many DGML
nodes; keeping the full list keeps "why is this in my binary" answers honest.

**Returns:** [IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<string> LeftNodeNames { get; init; }
```

### LeftSize

The bytes attributed on the baseline side (changed entries only for interior nodes).

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long LeftSize { get; init; }
```

### Name

Display name for this node. Method leaves carry their parameter list so overloads stay distinct.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### RightEntryCount

The number of raw comparison-side report rows behind this node.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int RightEntryCount { get; init; }
```

### RightNodeNames

Every dependency-graph node name behind the comparison-side rows.

**Returns:** [IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<string> RightNodeNames { get; init; }
```

### RightSize

The bytes attributed on the comparison side (changed entries only for interior nodes).

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long RightSize { get; init; }
```

## Methods

### Deconstruct(out string, out string, out SizeNodeKind, out DiffKind, out long, out long, out long, out IReadOnlyList\<SizeDiffNode\>, out int, out int, out IReadOnlyList\<string\>, out IReadOnlyList\<string\>)

**Parameters:**

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `FullPath` ([String](https://learn.microsoft.com/dotnet/api/system.string))
- `Kind` ([SizeNodeKind](/api/dotsider.core.analysis.models.sizenodekind/))
- `Diff` ([DiffKind](/api/dotsider.core.analysis.models.diffkind/))
- `LeftSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `RightSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `Delta` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `Children` ([IReadOnlyList\<SizeDiffNode\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `LeftEntryCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `RightEntryCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32))
- `LeftNodeNames` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `RightNodeNames` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public void Deconstruct(out string Name, out string FullPath, out SizeNodeKind Kind, out DiffKind Diff, out long LeftSize, out long RightSize, out long Delta, out IReadOnlyList<SizeDiffNode> Children, out int LeftEntryCount, out int RightEntryCount, out IReadOnlyList<string> LeftNodeNames, out IReadOnlyList<string> RightNodeNames)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(SizeDiffNode?)

**Parameters:**

- `other` ([SizeDiffNode](/api/dotsider.core.analysis.models.sizediffnode/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(SizeDiffNode? other)
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

### operator !=(SizeDiffNode?, SizeDiffNode?)

**Parameters:**

- `left` ([SizeDiffNode](/api/dotsider.core.analysis.models.sizediffnode/))
- `right` ([SizeDiffNode](/api/dotsider.core.analysis.models.sizediffnode/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(SizeDiffNode? left, SizeDiffNode? right)
```

### operator ==(SizeDiffNode?, SizeDiffNode?)

**Parameters:**

- `left` ([SizeDiffNode](/api/dotsider.core.analysis.models.sizediffnode/))
- `right` ([SizeDiffNode](/api/dotsider.core.analysis.models.sizediffnode/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(SizeDiffNode? left, SizeDiffNode? right)
```
