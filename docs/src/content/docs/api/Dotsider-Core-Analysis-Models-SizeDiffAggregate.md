---
title: "SizeDiffAggregate"
description: "The byte totals of one assembly or namespace on both sides of a size diff. Aggregates cover all attributable bytes for their scope — methods, MethodTables, RVA fields, frozen objects via their owner, and (for assemblies) resources — so a scoped size budget measures what the scope actually contributes."
slug: api/dotsider.core.analysis.models.sizediffaggregate
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The byte totals of one assembly or namespace on both sides of a size diff. Aggregates
cover all attributable bytes for their scope — methods, MethodTables, RVA fields, frozen
objects via their owner, and (for assemblies) resources — so a scoped size budget measures
what the scope actually contributes.

```csharp
public sealed record SizeDiffAggregate : IEquatable<SizeDiffAggregate>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **SizeDiffAggregate**

## Implements

- [IEquatable\<SizeDiffAggregate\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### SizeDiffAggregate(string, long, long, long)

The byte totals of one assembly or namespace on both sides of a size diff. Aggregates
cover all attributable bytes for their scope — methods, MethodTables, RVA fields, frozen
objects via their owner, and (for assemblies) resources — so a scoped size budget measures
what the scope actually contributes.

**Parameters:**

- `Name` ([String](https://learn.microsoft.com/dotnet/api/system.string)): The assembly simple name or namespace, or [UnattributedName](/api/dotsider.core.analysis.mstatsizeindex.unattributedname/).
- `LeftSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): The baseline bytes, or 0 when the scope is new.
- `RightSize` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): The comparison-side bytes, or 0 when the scope disappeared.
- `Delta` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): [RightSize](/api/dotsider.core.analysis.models.sizediffaggregate.rightsize/) minus [LeftSize](/api/dotsider.core.analysis.models.sizediffaggregate.leftsize/).

```csharp
public SizeDiffAggregate(string Name, long LeftSize, long RightSize, long Delta)
```

## Properties

### Delta

[RightSize](/api/dotsider.core.analysis.models.sizediffaggregate.rightsize/) minus [LeftSize](/api/dotsider.core.analysis.models.sizediffaggregate.leftsize/).

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long Delta { get; init; }
```

### LeftSize

The baseline bytes, or 0 when the scope is new.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long LeftSize { get; init; }
```

### Name

The assembly simple name or namespace, or [UnattributedName](/api/dotsider.core.analysis.mstatsizeindex.unattributedname/).

**Returns:** [String](https://learn.microsoft.com/dotnet/api/system.string)

```csharp
public string Name { get; init; }
```

### RightSize

The comparison-side bytes, or 0 when the scope disappeared.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long RightSize { get; init; }
```

