---
title: "SizeBudgetViolation"
description: "One breached limit of a size budget. Every figure is expressed in bytes — for the percent metric the limit is resolved against the baseline so the overage stays a byte count — with the percentages carried alongside where they apply."
slug: api/dotsider.core.analysis.models.sizebudgetviolation
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

One breached limit of a size budget. Every figure is expressed in bytes — for the percent
metric the limit is resolved against the baseline so the overage stays a byte count — with
the percentages carried alongside where they apply.

```csharp
public sealed record SizeBudgetViolation : IEquatable<SizeBudgetViolation>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **SizeBudgetViolation**

## Implements

- [IEquatable\<SizeBudgetViolation\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### SizeBudgetViolation(SizeBudgetMetric, long, long, long, double?, double?)

One breached limit of a size budget. Every figure is expressed in bytes — for the percent
metric the limit is resolved against the baseline so the overage stays a byte count — with
the percentages carried alongside where they apply.

**Parameters:**

- `Metric` ([SizeBudgetMetric](/api/dotsider.core.analysis.models.sizebudgetmetric/)): The limit that was breached.
- `ActualBytes` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): The measured value: the current size for [MaxBytes](/api/dotsider.core.analysis.models.sizebudgetmetric.maxbytes/), the growth in bytes for the growth metrics.
- `LimitBytes` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): The limit in bytes; for [MaxGrowthPercent](/api/dotsider.core.analysis.models.sizebudgetmetric.maxgrowthpercent/) this is the baseline times the allowed percentage.
- `OverageBytes` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): [ActualBytes](/api/dotsider.core.analysis.models.sizebudgetviolation.actualbytes/) minus [LimitBytes](/api/dotsider.core.analysis.models.sizebudgetviolation.limitbytes/).
- `ActualPercent` ([Nullable\<Double\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The measured growth percentage, or null when the baseline was zero (a new scope — any growth breaches).
- `LimitPercent` ([Nullable\<Double\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The allowed growth percentage, or null for the byte metrics.

```csharp
public SizeBudgetViolation(SizeBudgetMetric Metric, long ActualBytes, long LimitBytes, long OverageBytes, double? ActualPercent, double? LimitPercent)
```

## Properties

### ActualBytes

The measured value: the current size for [MaxBytes](/api/dotsider.core.analysis.models.sizebudgetmetric.maxbytes/), the growth in bytes for the growth metrics.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long ActualBytes { get; init; }
```

### ActualPercent

The measured growth percentage, or null when the baseline was zero (a new scope — any growth breaches).

**Returns:** [Nullable\<Double\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public double? ActualPercent { get; init; }
```

### LimitBytes

The limit in bytes; for [MaxGrowthPercent](/api/dotsider.core.analysis.models.sizebudgetmetric.maxgrowthpercent/) this is the baseline times the allowed percentage.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long LimitBytes { get; init; }
```

### LimitPercent

The allowed growth percentage, or null for the byte metrics.

**Returns:** [Nullable\<Double\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public double? LimitPercent { get; init; }
```

### Metric

The limit that was breached.

**Returns:** [SizeBudgetMetric](/api/dotsider.core.analysis.models.sizebudgetmetric/)

```csharp
public SizeBudgetMetric Metric { get; init; }
```

### OverageBytes

[ActualBytes](/api/dotsider.core.analysis.models.sizebudgetviolation.actualbytes/) minus [LimitBytes](/api/dotsider.core.analysis.models.sizebudgetviolation.limitbytes/).

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long OverageBytes { get; init; }
```

## Methods

### Deconstruct(out SizeBudgetMetric, out long, out long, out long, out double?, out double?)

**Parameters:**

- `Metric` ([SizeBudgetMetric](/api/dotsider.core.analysis.models.sizebudgetmetric/))
- `ActualBytes` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `LimitBytes` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `OverageBytes` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `ActualPercent` ([Nullable\<Double\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `LimitPercent` ([Nullable\<Double\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))

```csharp
public void Deconstruct(out SizeBudgetMetric Metric, out long ActualBytes, out long LimitBytes, out long OverageBytes, out double? ActualPercent, out double? LimitPercent)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(SizeBudgetViolation?)

**Parameters:**

- `other` ([SizeBudgetViolation](/api/dotsider.core.analysis.models.sizebudgetviolation/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(SizeBudgetViolation? other)
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

### operator !=(SizeBudgetViolation?, SizeBudgetViolation?)

**Parameters:**

- `left` ([SizeBudgetViolation](/api/dotsider.core.analysis.models.sizebudgetviolation/))
- `right` ([SizeBudgetViolation](/api/dotsider.core.analysis.models.sizebudgetviolation/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(SizeBudgetViolation? left, SizeBudgetViolation? right)
```

### operator ==(SizeBudgetViolation?, SizeBudgetViolation?)

**Parameters:**

- `left` ([SizeBudgetViolation](/api/dotsider.core.analysis.models.sizebudgetviolation/))
- `right` ([SizeBudgetViolation](/api/dotsider.core.analysis.models.sizebudgetviolation/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(SizeBudgetViolation? left, SizeBudgetViolation? right)
```
