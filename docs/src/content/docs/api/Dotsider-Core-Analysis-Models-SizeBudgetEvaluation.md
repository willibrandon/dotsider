---
title: "SizeBudgetEvaluation"
description: "The outcome of evaluating one size budget: the measured values, any breached limits, and the top positive regressions inside the budget's scope — the rows that explain a growth breach, never diluted by improvements elsewhere."
slug: api/dotsider.core.analysis.models.sizebudgetevaluation
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The outcome of evaluating one size budget: the measured values, any breached limits, and
the top positive regressions inside the budget's scope — the rows that explain a growth
breach, never diluted by improvements elsewhere.

```csharp
public sealed record SizeBudgetEvaluation : IEquatable<SizeBudgetEvaluation>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **SizeBudgetEvaluation**

## Implements

- [IEquatable\<SizeBudgetEvaluation\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### SizeBudgetEvaluation(SizeBudget, bool, SizeBasis, long, long?, IReadOnlyList\<SizeBudgetViolation\>, IReadOnlyList\<SizeDiffContributor\>)

The outcome of evaluating one size budget: the measured values, any breached limits, and
the top positive regressions inside the budget's scope — the rows that explain a growth
breach, never diluted by improvements elsewhere.

**Parameters:**

- `Budget` ([SizeBudget](/api/dotsider.core.analysis.models.sizebudget/)): The budget that was evaluated.
- `Passed` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): True when no limit was breached.
- `Basis` ([SizeBasis](/api/dotsider.core.analysis.models.sizebasis/)): What the measured values count: total budgets use the check's total basis (file size for
binaries, mstat total for bare reports); namespace and assembly budgets always measure
mstat aggregates.
- `ActualBytes` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): The scope's current size in bytes.
- `BaselineBytes` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The scope's baseline size in bytes, or null when the check ran without a baseline.
- `Violations` ([IReadOnlyList\<SizeBudgetViolation\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): Each breached limit, or empty when the budget passed.
- `TopContributors` ([IReadOnlyList\<SizeDiffContributor\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The scope's largest positive regressions (delta &gt; 0), ordered by delta descending, up to
the budget's or the caller's top-N.

```csharp
public SizeBudgetEvaluation(SizeBudget Budget, bool Passed, SizeBasis Basis, long ActualBytes, long? BaselineBytes, IReadOnlyList<SizeBudgetViolation> Violations, IReadOnlyList<SizeDiffContributor> TopContributors)
```

## Properties

### ActualBytes

The scope's current size in bytes.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long ActualBytes { get; init; }
```

### BaselineBytes

The scope's baseline size in bytes, or null when the check ran without a baseline.

**Returns:** [Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public long? BaselineBytes { get; init; }
```

### Basis

What the measured values count: total budgets use the check's total basis (file size for
binaries, mstat total for bare reports); namespace and assembly budgets always measure
mstat aggregates.

**Returns:** [SizeBasis](/api/dotsider.core.analysis.models.sizebasis/)

```csharp
public SizeBasis Basis { get; init; }
```

### Budget

The budget that was evaluated.

**Returns:** [SizeBudget](/api/dotsider.core.analysis.models.sizebudget/)

```csharp
public SizeBudget Budget { get; init; }
```

### DeferredMetrics

Growth limits that could not be evaluated because no baseline exists. Absolute limits
in the same budget are still evaluated normally.

**Returns:** [IReadOnlyList\<SizeBudgetMetric\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<SizeBudgetMetric> DeferredMetrics { get; init; }
```

### Passed

True when no limit was breached.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Passed { get; init; }
```

### TopContributors

The scope's largest positive regressions (delta &gt; 0), ordered by delta descending, up to
the budget's or the caller's top-N.

**Returns:** [IReadOnlyList\<SizeDiffContributor\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<SizeDiffContributor> TopContributors { get; init; }
```

### Violations

Each breached limit, or empty when the budget passed.

**Returns:** [IReadOnlyList\<SizeBudgetViolation\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<SizeBudgetViolation> Violations { get; init; }
```

## Methods

### Deconstruct(out SizeBudget, out bool, out SizeBasis, out long, out long?, out IReadOnlyList\<SizeBudgetViolation\>, out IReadOnlyList\<SizeDiffContributor\>)

**Parameters:**

- `Budget` ([SizeBudget](/api/dotsider.core.analysis.models.sizebudget/))
- `Passed` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `Basis` ([SizeBasis](/api/dotsider.core.analysis.models.sizebasis/))
- `ActualBytes` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `BaselineBytes` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `Violations` ([IReadOnlyList\<SizeBudgetViolation\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))
- `TopContributors` ([IReadOnlyList\<SizeDiffContributor\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public void Deconstruct(out SizeBudget Budget, out bool Passed, out SizeBasis Basis, out long ActualBytes, out long? BaselineBytes, out IReadOnlyList<SizeBudgetViolation> Violations, out IReadOnlyList<SizeDiffContributor> TopContributors)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(SizeBudgetEvaluation?)

**Parameters:**

- `other` ([SizeBudgetEvaluation](/api/dotsider.core.analysis.models.sizebudgetevaluation/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(SizeBudgetEvaluation? other)
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

### operator !=(SizeBudgetEvaluation?, SizeBudgetEvaluation?)

**Parameters:**

- `left` ([SizeBudgetEvaluation](/api/dotsider.core.analysis.models.sizebudgetevaluation/))
- `right` ([SizeBudgetEvaluation](/api/dotsider.core.analysis.models.sizebudgetevaluation/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(SizeBudgetEvaluation? left, SizeBudgetEvaluation? right)
```

### operator ==(SizeBudgetEvaluation?, SizeBudgetEvaluation?)

**Parameters:**

- `left` ([SizeBudgetEvaluation](/api/dotsider.core.analysis.models.sizebudgetevaluation/))
- `right` ([SizeBudgetEvaluation](/api/dotsider.core.analysis.models.sizebudgetevaluation/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(SizeBudgetEvaluation? left, SizeBudgetEvaluation? right)
```
