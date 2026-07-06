---
title: "MstatDiffResult"
description: "The complete result of comparing two ILC size reports: a hierarchical delta tree for treemap rendering, headline figures, the flat contributor list a CI log prints, and the per-assembly / per-namespace aggregates that size budgets evaluate against."
slug: api/dotsider.core.analysis.models.mstatdiffresult
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The complete result of comparing two ILC size reports: a hierarchical delta tree for
treemap rendering, headline figures, the flat contributor list a CI log prints, and the
per-assembly / per-namespace aggregates that size budgets evaluate against.

```csharp
public sealed record MstatDiffResult : IEquatable<MstatDiffResult>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **MstatDiffResult**

## Implements

- [IEquatable\<MstatDiffResult\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### MstatDiffResult(string, string, SizeDiffNode, SizeDiffSummary, IReadOnlyList\<SizeDiffContributor\>, IReadOnlyList\<SizeDiffAggregate\>, IReadOnlyList\<SizeDiffAggregate\>)

The complete result of comparing two ILC size reports: a hierarchical delta tree for
treemap rendering, headline figures, the flat contributor list a CI log prints, and the
per-assembly / per-namespace aggregates that size budgets evaluate against.

**Parameters:**

- `LeftFormatVersion` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The baseline report's format version (for example `"2.2"`; `"0.0"` for the empty baseline).
- `RightFormatVersion` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The comparison report's format version.
- `Root` ([SizeDiffNode](/api/dotsider.core.analysis.models.sizediffnode/)): The delta tree — changed subtrees only, children ordered by absolute delta.
- `Summary` ([SizeDiffSummary](/api/dotsider.core.analysis.models.sizediffsummary/)): Totals, unchanged mass, and per-kind direction counts.
- `Contributors` ([IReadOnlyList\<SizeDiffContributor\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Every changed entry, ordered by absolute delta descending. Callers trim to their top-N.
- `AssemblyDeltas` ([IReadOnlyList\<SizeDiffAggregate\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Attributable bytes per assembly on both sides, ordered by absolute delta descending.
- `NamespaceDeltas` ([IReadOnlyList\<SizeDiffAggregate\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Attributable bytes per namespace on both sides, folded across assemblies, ordered by absolute delta descending.

```csharp
public MstatDiffResult(string LeftFormatVersion, string RightFormatVersion, SizeDiffNode Root, SizeDiffSummary Summary, IReadOnlyList<SizeDiffContributor> Contributors, IReadOnlyList<SizeDiffAggregate> AssemblyDeltas, IReadOnlyList<SizeDiffAggregate> NamespaceDeltas)
```

## Properties

### AssemblyDeltas

Attributable bytes per assembly on both sides, ordered by absolute delta descending.

**Returns:** [IReadOnlyList\<SizeDiffAggregate\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<SizeDiffAggregate> AssemblyDeltas { get; init; }
```

### Contributors

Every changed entry, ordered by absolute delta descending. Callers trim to their top-N.

**Returns:** [IReadOnlyList\<SizeDiffContributor\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<SizeDiffContributor> Contributors { get; init; }
```

### LeftFormatVersion

The baseline report's format version (for example `"2.2"`; `"0.0"` for the empty baseline).

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string LeftFormatVersion { get; init; }
```

### NamespaceDeltas

Attributable bytes per namespace on both sides, folded across assemblies, ordered by absolute delta descending.

**Returns:** [IReadOnlyList\<SizeDiffAggregate\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<SizeDiffAggregate> NamespaceDeltas { get; init; }
```

### RightFormatVersion

The comparison report's format version.

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string RightFormatVersion { get; init; }
```

### Root

The delta tree — changed subtrees only, children ordered by absolute delta.

**Returns:** [SizeDiffNode](/api/dotsider.core.analysis.models.sizediffnode/)

```csharp
public SizeDiffNode Root { get; init; }
```

### Summary

Totals, unchanged mass, and per-kind direction counts.

**Returns:** [SizeDiffSummary](/api/dotsider.core.analysis.models.sizediffsummary/)

```csharp
public SizeDiffSummary Summary { get; init; }
```

