---
title: "SizeBudgetEvaluator"
description: "Evaluates size budgets against a size diff. Total budgets measure the caller's basis-resolved totals (file size for binaries, mstat total for bare reports); namespace and assembly budgets always measure the diff's mstat aggregates, with namespace targets covering their sub-namespaces. Each breach carries the scope's top positive regressions — the rows that explain the growth."
slug: api/dotsider.core.analysis.sizebudgetevaluator
sidebar:
  order: 0
---

**Namespace:** `Dotsider.Core.Analysis`

**Assembly:** Dotsider.Core.dll

Evaluates size budgets against a size diff. Total budgets measure the caller's
basis-resolved totals (file size for binaries, mstat total for bare reports); namespace and
assembly budgets always measure the diff's mstat aggregates, with namespace targets
covering their sub-namespaces. Each breach carries the scope's top positive regressions —
the rows that explain the growth.

```csharp
public static class SizeBudgetEvaluator
```

## Inheritance

[Object](https://learn.microsoft.com/dotnet/api/system.object) → **SizeBudgetEvaluator**

## Methods

### Evaluate(IReadOnlyList\<SizeBudget\>, MstatDiffResult, SizeBasis, long, long?, int)

Evaluates budgets against a diff.

**Parameters:**

- `budgets` ([IReadOnlyList\<SizeBudget\>](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)): The budgets to evaluate, reported in this order.
- `diff` ([MstatDiffResult](/api/dotsider.core.analysis.models.mstatdiffresult/)): The size diff between the baseline and the build under check. For a check without a baseline, a diff against [Empty](/api/dotsider.core.analysis.models.mstatdata.empty/).
- `totalBasis` ([SizeBasis](/api/dotsider.core.analysis.models.sizebasis/)): What the total figures count.
- `currentTotalBytes` ([Int64](https://learn.microsoft.com/dotnet/api/system.int64)): The build's total on totalBasis.
- `baselineTotalBytes` ([Nullable\<Int64\>](https://learn.microsoft.com/dotnet/api/system.nullable-1)): The baseline's total on totalBasis, or null when the check runs without one.
- `defaultTopN` ([Int32](https://learn.microsoft.com/dotnet/api/system.int32)): How many contributors a breach lists when its budget does not pin its own count.

**Returns:** [SizeBudgetReport](/api/dotsider.core.analysis.models.sizebudgetreport/)

The report, failing only on error-severity breaches.

```csharp
public static SizeBudgetReport Evaluate(IReadOnlyList<SizeBudget> budgets, MstatDiffResult diff, SizeBasis totalBasis, long currentTotalBytes, long? baselineTotalBytes, int defaultTopN = 10)
```

