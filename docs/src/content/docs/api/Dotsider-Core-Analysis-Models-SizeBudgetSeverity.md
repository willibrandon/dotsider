---
title: "SizeBudgetSeverity"
description: "How a failed size budget affects the outcome: an error fails the check (the CI gate exits non-zero), a warning is reported but never changes the exit code."
slug: api/dotsider.core.analysis.models.sizebudgetseverity
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

How a failed size budget affects the outcome: an error fails the check (the CI gate exits
non-zero), a warning is reported but never changes the exit code.

```csharp
public enum SizeBudgetSeverity
```

## Fields

### Error

A breach fails the check.

**Returns:** [SizeBudgetSeverity](/api/dotsider.core.analysis.models.sizebudgetseverity/)

```csharp
Error = 0
```

### Warning

A breach is reported without failing the check.

**Returns:** [SizeBudgetSeverity](/api/dotsider.core.analysis.models.sizebudgetseverity/)

```csharp
Warning = 1
```

