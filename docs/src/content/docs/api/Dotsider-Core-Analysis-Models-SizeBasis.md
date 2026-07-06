---
title: "SizeBasis"
description: "What a total-size figure measures. A binary on disk and the sum of its mstat entries are different numbers (headers, alignment, and unreported bytes sit between them), so every report states which basis it used."
slug: api/dotsider.core.analysis.models.sizebasis
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

What a total-size figure measures. A binary on disk and the sum of its mstat entries are
different numbers (headers, alignment, and unreported bytes sit between them), so every
report states which basis it used.

```csharp
public enum SizeBasis
```

## Fields

### FileSize

The binary's file size on disk.

**Returns:** [SizeBasis](/api/dotsider.core.analysis.models.sizebasis/)

```csharp
FileSize = 0
```

### MstatTotal

The sum of the mstat report's attributable entries.

**Returns:** [SizeBasis](/api/dotsider.core.analysis.models.sizebasis/)

```csharp
MstatTotal = 1
```

