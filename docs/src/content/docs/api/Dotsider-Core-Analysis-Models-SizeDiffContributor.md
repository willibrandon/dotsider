---
title: "SizeDiffContributor"
description: "One changed entry of a size diff in flat form — the shape a CI log or a budget violation prints. Contributors carry the same identity and attribution as their tree leaves."
slug: api/dotsider.core.analysis.models.sizediffcontributor
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One changed entry of a size diff in flat form — the shape a CI log or a budget violation
prints. Contributors carry the same identity and attribution as their tree leaves.

```csharp
public sealed record SizeDiffContributor : IEquatable<SizeDiffContributor>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **SizeDiffContributor**

## Implements

- [IEquatable\<SizeDiffContributor\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### SizeDiffContributor(string, string, SizeNodeKind, DiffKind, long, long, long, string, string, int, int, IReadOnlyList\<string\>, IReadOnlyList\<string\>)

One changed entry of a size diff in flat form — the shape a CI log or a budget violation
prints. Contributors carry the same identity and attribution as their tree leaves.

**Parameters:**

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): Display name. Method contributors carry their parameter list so overloads stay distinct.
- `FullPath` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The entry's deterministic path, matching its node in the delta tree.
- `Kind` ([SizeNodeKind](/api/dotsider.core.analysis.models.sizenodekind/)): The entry's node kind.
- `Diff` ([DiffKind](/api/dotsider.core.analysis.models.diffkind/)): Added, removed, or changed (grown when [Delta](/api/dotsider.core.analysis.models.sizediffcontributor.delta/) is positive, shrunk when negative).
- `LeftSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): The baseline bytes, or 0 when added.
- `RightSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): The comparison-side bytes, or 0 when removed.
- `Delta` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): [RightSize](/api/dotsider.core.analysis.models.sizediffcontributor.rightsize/) minus [LeftSize](/api/dotsider.core.analysis.models.sizediffcontributor.leftsize/).
- `AssemblyName` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The assembly the bytes are attributed to (owner-based for frozen objects,
[UnattributedName](/api/dotsider.core.analysis.mstatsizeindex.unattributedname/) when unknowable), or an
empty string for global sections.
- `Namespace` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The namespace the bytes are attributed to, or an empty string where none applies.
- `LeftEntryCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The number of raw baseline rows behind the entry; greater than one marks an aggregate.
- `RightEntryCount` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The number of raw comparison-side rows behind the entry.
- `LeftNodeNames` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Every baseline dependency-graph node name behind the entry.
- `RightNodeNames` ([IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Every comparison-side dependency-graph node name behind the entry — the join keys for "why did this appear".

```csharp
public SizeDiffContributor(string Name, string FullPath, SizeNodeKind Kind, DiffKind Diff, long LeftSize, long RightSize, long Delta, string AssemblyName, string Namespace, int LeftEntryCount, int RightEntryCount, IReadOnlyList<string> LeftNodeNames, IReadOnlyList<string> RightNodeNames)
```

## Properties

### AssemblyName

The assembly the bytes are attributed to (owner-based for frozen objects,
[UnattributedName](/api/dotsider.core.analysis.mstatsizeindex.unattributedname/) when unknowable), or an
empty string for global sections.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string AssemblyName { get; init; }
```

### Delta

[RightSize](/api/dotsider.core.analysis.models.sizediffcontributor.rightsize/) minus [LeftSize](/api/dotsider.core.analysis.models.sizediffcontributor.leftsize/).

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long Delta { get; init; }
```

### Diff

Added, removed, or changed (grown when [Delta](/api/dotsider.core.analysis.models.sizediffcontributor.delta/) is positive, shrunk when negative).

**Returns:** [DiffKind](/api/dotsider.core.analysis.models.diffkind/)

```csharp
public DiffKind Diff { get; init; }
```

### FullPath

The entry's deterministic path, matching its node in the delta tree.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string FullPath { get; init; }
```

### Kind

The entry's node kind.

**Returns:** [SizeNodeKind](/api/dotsider.core.analysis.models.sizenodekind/)

```csharp
public SizeNodeKind Kind { get; init; }
```

### LeftEntryCount

The number of raw baseline rows behind the entry; greater than one marks an aggregate.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int LeftEntryCount { get; init; }
```

### LeftNodeNames

Every baseline dependency-graph node name behind the entry.

**Returns:** [IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<string> LeftNodeNames { get; init; }
```

### LeftSize

The baseline bytes, or 0 when added.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long LeftSize { get; init; }
```

### Name

Display name. Method contributors carry their parameter list so overloads stay distinct.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### Namespace

The namespace the bytes are attributed to, or an empty string where none applies.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Namespace { get; init; }
```

### RightEntryCount

The number of raw comparison-side rows behind the entry.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int RightEntryCount { get; init; }
```

### RightNodeNames

Every comparison-side dependency-graph node name behind the entry — the join keys for "why did this appear".

**Returns:** [IReadOnlyList\<String\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<string> RightNodeNames { get; init; }
```

### RightSize

The comparison-side bytes, or 0 when removed.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long RightSize { get; init; }
```

