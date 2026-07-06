---
title: "SizeDiffSummary"
description: "The headline figures of a size diff between two Native AOT builds. Totals are mstat attributable bytes — the same figures analyze --size reports for each build alone."
slug: api/dotsider.core.analysis.models.sizediffsummary
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The headline figures of a size diff between two Native AOT builds. Totals are mstat
attributable bytes — the same figures `analyze --size` reports for each build alone.

```csharp
public sealed record SizeDiffSummary : IEquatable<SizeDiffSummary>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **SizeDiffSummary**

## Implements

- [IEquatable\<SizeDiffSummary\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### SizeDiffSummary(long, long, long, long, IReadOnlyList\<SizeDiffKindCounts\>, int, int)

The headline figures of a size diff between two Native AOT builds. Totals are mstat
attributable bytes — the same figures `analyze --size` reports for each build alone.

**Parameters:**

- `LeftTotal` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): The baseline build's total attributable bytes.
- `RightTotal` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): The comparison build's total attributable bytes.
- `Delta` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): [RightTotal](/api/dotsider.core.analysis.models.sizediffsummary.righttotal/) minus [LeftTotal](/api/dotsider.core.analysis.models.sizediffsummary.lefttotal/).
- `UnchangedTotal` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): The bytes carried by entries identical in both builds. The delta tree omits these; a
self-diff has [UnchangedTotal](/api/dotsider.core.analysis.models.sizediffsummary.unchangedtotal/) equal to the build total and an empty tree.
- `Counts` ([IReadOnlyList\<SizeDiffKindCounts\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Per-kind entry counts split by direction.
- `LeftDeduplicatedMethods` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The baseline build's deduplicated-method count (format 2.2+; informational — the entries carry no bytes).
- `RightDeduplicatedMethods` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): The comparison build's deduplicated-method count.

```csharp
public SizeDiffSummary(long LeftTotal, long RightTotal, long Delta, long UnchangedTotal, IReadOnlyList<SizeDiffKindCounts> Counts, int LeftDeduplicatedMethods, int RightDeduplicatedMethods)
```

## Properties

### Counts

Per-kind entry counts split by direction.

**Returns:** [IReadOnlyList\<SizeDiffKindCounts\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<SizeDiffKindCounts> Counts { get; init; }
```

### Delta

[RightTotal](/api/dotsider.core.analysis.models.sizediffsummary.righttotal/) minus [LeftTotal](/api/dotsider.core.analysis.models.sizediffsummary.lefttotal/).

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long Delta { get; init; }
```

### LeftDeduplicatedMethods

The baseline build's deduplicated-method count (format 2.2+; informational — the entries carry no bytes).

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int LeftDeduplicatedMethods { get; init; }
```

### LeftTotal

The baseline build's total attributable bytes.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long LeftTotal { get; init; }
```

### RightDeduplicatedMethods

The comparison build's deduplicated-method count.

**Returns:** [Int32](https://learn.microsoft.com/dotnet/api/system.int32)

```csharp
public int RightDeduplicatedMethods { get; init; }
```

### RightTotal

The comparison build's total attributable bytes.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long RightTotal { get; init; }
```

### UnchangedTotal

The bytes carried by entries identical in both builds. The delta tree omits these; a
self-diff has [UnchangedTotal](/api/dotsider.core.analysis.models.sizediffsummary.unchangedtotal/) equal to the build total and an empty tree.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long UnchangedTotal { get; init; }
```

