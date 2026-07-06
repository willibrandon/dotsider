---
title: "SizeBudgetMetric"
description: "The limit a size budget enforces."
slug: api/dotsider.core.analysis.models.sizebudgetmetric
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The limit a size budget enforces.

```csharp
public enum SizeBudgetMetric
```

## Fields

### MaxBytes

An absolute cap on the current value, in bytes.

**Returns:** [SizeBudgetMetric](/api/dotsider.core.analysis.models.sizebudgetmetric/)

```csharp
MaxBytes = 0
```

### MaxGrowthBytes

A cap on growth versus the baseline, in bytes.

**Returns:** [SizeBudgetMetric](/api/dotsider.core.analysis.models.sizebudgetmetric/)

```csharp
MaxGrowthBytes = 1
```

### MaxGrowthPercent

A cap on growth versus the baseline, as a percentage of the baseline.

**Returns:** [SizeBudgetMetric](/api/dotsider.core.analysis.models.sizebudgetmetric/)

```csharp
MaxGrowthPercent = 2
```

