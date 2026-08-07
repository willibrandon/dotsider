---
title: "SizeTotals"
description: "The basis-resolved totals of a size comparison: file sizes when every provided input is a binary, mstat attributable totals when a bare .mstat is anywhere in the pair — both sides always share one basis so the figures stay comparable."
slug: api/dotsider.core.analysis.models.sizetotals
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The basis-resolved totals of a size comparison: file sizes when every provided input is a
binary, mstat attributable totals when a bare `.mstat` is anywhere in the pair — both
sides always share one basis so the figures stay comparable.

```csharp
public sealed record SizeTotals : IEquatable<SizeTotals>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **SizeTotals**

## Implements

- [IEquatable\<SizeTotals\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### SizeTotals(SizeBasis, long, long?)

The basis-resolved totals of a size comparison: file sizes when every provided input is a
binary, mstat attributable totals when a bare `.mstat` is anywhere in the pair — both
sides always share one basis so the figures stay comparable.

**Parameters:**

- `Basis` ([SizeBasis](/api/dotsider.core.analysis.models.sizebasis/)): What the totals count.
- `RightTotal` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): The current build's total on [Basis](/api/dotsider.core.analysis.models.sizetotals.basis/).
- `LeftTotal` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The baseline's total on [Basis](/api/dotsider.core.analysis.models.sizetotals.basis/), or null when there is no baseline.

```csharp
public SizeTotals(SizeBasis Basis, long RightTotal, long? LeftTotal)
```

## Properties

### Basis

What the totals count.

**Returns:** [SizeBasis](/api/dotsider.core.analysis.models.sizebasis/)

```csharp
public SizeBasis Basis { get; init; }
```

### LeftTotal

The baseline's total on [Basis](/api/dotsider.core.analysis.models.sizetotals.basis/), or null when there is no baseline.

**Returns:** [Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public long? LeftTotal { get; init; }
```

### RightTotal

The current build's total on [Basis](/api/dotsider.core.analysis.models.sizetotals.basis/).

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long RightTotal { get; init; }
```

## Methods

### Deconstruct(out SizeBasis, out long, out long?)

**Parameters:**

- `Basis` ([SizeBasis](/api/dotsider.core.analysis.models.sizebasis/))
- `RightTotal` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `LeftTotal` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))

```csharp
public void Deconstruct(out SizeBasis Basis, out long RightTotal, out long? LeftTotal)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(SizeTotals?)

**Parameters:**

- `other` ([SizeTotals](/api/dotsider.core.analysis.models.sizetotals/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(SizeTotals? other)
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

### operator !=(SizeTotals?, SizeTotals?)

**Parameters:**

- `left` ([SizeTotals](/api/dotsider.core.analysis.models.sizetotals/))
- `right` ([SizeTotals](/api/dotsider.core.analysis.models.sizetotals/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(SizeTotals? left, SizeTotals? right)
```

### operator ==(SizeTotals?, SizeTotals?)

**Parameters:**

- `left` ([SizeTotals](/api/dotsider.core.analysis.models.sizetotals/))
- `right` ([SizeTotals](/api/dotsider.core.analysis.models.sizetotals/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(SizeTotals? left, SizeTotals? right)
```
