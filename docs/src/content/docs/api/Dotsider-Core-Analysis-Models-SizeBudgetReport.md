---
title: "SizeBudgetReport"
description: "The outcome of checking a build against a set of size budgets. The check fails — Passed is false — only when an error-severity budget breached; warning breaches surface through HasWarnings without failing the check."
slug: api/dotsider.core.analysis.models.sizebudgetreport
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The outcome of checking a build against a set of size budgets. The check fails —
[Passed](/api/dotsider.core.analysis.models.sizebudgetreport.passed/) is false — only when an error-severity budget breached; warning
breaches surface through [HasWarnings](/api/dotsider.core.analysis.models.sizebudgetreport.haswarnings/) without failing the check.

```csharp
public sealed record SizeBudgetReport : IEquatable<SizeBudgetReport>
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **SizeBudgetReport**

## Implements

- [IEquatable\<SizeBudgetReport\>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

## Constructors

### SizeBudgetReport(bool, bool, SizeBasis, long, long, long?, long?, IReadOnlyList\<SizeBudgetEvaluation\>)

The outcome of checking a build against a set of size budgets. The check fails —
[Passed](/api/dotsider.core.analysis.models.sizebudgetreport.passed/) is false — only when an error-severity budget breached; warning
breaches surface through [HasWarnings](/api/dotsider.core.analysis.models.sizebudgetreport.haswarnings/) without failing the check.

**Parameters:**

- `Passed` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): False when at least one error-severity budget breached.
- `HasWarnings` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)): True when at least one warning-severity budget breached.
- `TotalBasis` ([SizeBasis](/api/dotsider.core.analysis.models.sizebasis/)): What the total figures count: file size when the inputs were binaries, mstat totals otherwise.
- `LeftTotal` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): The baseline total on [TotalBasis](/api/dotsider.core.analysis.models.sizebudgetreport.totalbasis/), or 0 when the check ran without a baseline.
- `RightTotal` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): The current total on [TotalBasis](/api/dotsider.core.analysis.models.sizebudgetreport.totalbasis/).
- `LeftMstatTotal` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The baseline's mstat attributable total, surfaced alongside when [TotalBasis](/api/dotsider.core.analysis.models.sizebudgetreport.totalbasis/) is file size; null otherwise.
- `RightMstatTotal` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The current build's mstat attributable total, surfaced alongside when [TotalBasis](/api/dotsider.core.analysis.models.sizebudgetreport.totalbasis/) is file size; null otherwise.
- `Evaluations` ([IReadOnlyList\<SizeBudgetEvaluation\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): One outcome per budget, in input order.

```csharp
public SizeBudgetReport(bool Passed, bool HasWarnings, SizeBasis TotalBasis, long LeftTotal, long RightTotal, long? LeftMstatTotal, long? RightMstatTotal, IReadOnlyList<SizeBudgetEvaluation> Evaluations)
```

## Properties

### Evaluations

One outcome per budget, in input order.

**Returns:** [IReadOnlyList\<SizeBudgetEvaluation\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)

```csharp
public IReadOnlyList<SizeBudgetEvaluation> Evaluations { get; init; }
```

### HasWarnings

True when at least one warning-severity budget breached.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool HasWarnings { get; init; }
```

### LeftMstatTotal

The baseline's mstat attributable total, surfaced alongside when [TotalBasis](/api/dotsider.core.analysis.models.sizebudgetreport.totalbasis/) is file size; null otherwise.

**Returns:** [Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public long? LeftMstatTotal { get; init; }
```

### LeftTotal

The baseline total on [TotalBasis](/api/dotsider.core.analysis.models.sizebudgetreport.totalbasis/), or 0 when the check ran without a baseline.

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long LeftTotal { get; init; }
```

### Passed

False when at least one error-severity budget breached.

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Passed { get; init; }
```

### RightMstatTotal

The current build's mstat attributable total, surfaced alongside when [TotalBasis](/api/dotsider.core.analysis.models.sizebudgetreport.totalbasis/) is file size; null otherwise.

**Returns:** [Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)

```csharp
public long? RightMstatTotal { get; init; }
```

### RightTotal

The current total on [TotalBasis](/api/dotsider.core.analysis.models.sizebudgetreport.totalbasis/).

**Returns:** [Int64](https://learn.microsoft.com/dotnet/api/system.int64)

```csharp
public long RightTotal { get; init; }
```

### TotalBasis

What the total figures count: file size when the inputs were binaries, mstat totals otherwise.

**Returns:** [SizeBasis](/api/dotsider.core.analysis.models.sizebasis/)

```csharp
public SizeBasis TotalBasis { get; init; }
```

## Methods

### Deconstruct(out bool, out bool, out SizeBasis, out long, out long, out long?, out long?, out IReadOnlyList\<SizeBudgetEvaluation\>)

**Parameters:**

- `Passed` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `HasWarnings` ([Boolean](https://learn.microsoft.com/dotnet/api/system.boolean))
- `TotalBasis` ([SizeBasis](/api/dotsider.core.analysis.models.sizebasis/))
- `LeftTotal` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `RightTotal` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64))
- `LeftMstatTotal` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `RightMstatTotal` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1))
- `Evaluations` ([IReadOnlyList\<SizeBudgetEvaluation\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1))

```csharp
public void Deconstruct(out bool Passed, out bool HasWarnings, out SizeBasis TotalBasis, out long LeftTotal, out long RightTotal, out long? LeftMstatTotal, out long? RightMstatTotal, out IReadOnlyList<SizeBudgetEvaluation> Evaluations)
```

### Equals(object?)

**Parameters:**

- `obj` ([Object](https://learn.microsoft.com/dotnet/api/system.object))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public override bool Equals(object? obj)
```

### Equals(SizeBudgetReport?)

**Parameters:**

- `other` ([SizeBudgetReport](/api/dotsider.core.analysis.models.sizebudgetreport/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public bool Equals(SizeBudgetReport? other)
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

### operator !=(SizeBudgetReport?, SizeBudgetReport?)

**Parameters:**

- `left` ([SizeBudgetReport](/api/dotsider.core.analysis.models.sizebudgetreport/))
- `right` ([SizeBudgetReport](/api/dotsider.core.analysis.models.sizebudgetreport/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator !=(SizeBudgetReport? left, SizeBudgetReport? right)
```

### operator ==(SizeBudgetReport?, SizeBudgetReport?)

**Parameters:**

- `left` ([SizeBudgetReport](/api/dotsider.core.analysis.models.sizebudgetreport/))
- `right` ([SizeBudgetReport](/api/dotsider.core.analysis.models.sizebudgetreport/))

**Returns:** [Boolean](https://learn.microsoft.com/dotnet/api/system.boolean)

```csharp
public static bool operator ==(SizeBudgetReport? left, SizeBudgetReport? right)
```
