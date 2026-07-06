---
title: "SizeBudgetScope"
description: "What a size budget measures: the whole binary, one namespace subtree, or one assembly."
slug: api/dotsider.core.analysis.models.sizebudgetscope
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

What a size budget measures: the whole binary, one namespace subtree, or one assembly.

```csharp
public enum SizeBudgetScope
```

## Fields

### Assembly

One assembly, matched by simple name.

**Returns:** [SizeBudgetScope](/api/dotsider.core.analysis.models.sizebudgetscope/)

```csharp
Assembly = 2
```

### Namespace

A namespace and everything beneath it: a target of `System.Text.Json` covers
`System.Text.Json.Serialization` but not `System.Text.Json2`.

**Returns:** [SizeBudgetScope](/api/dotsider.core.analysis.models.sizebudgetscope/)

```csharp
Namespace = 1
```

### Total

The build's total size.

**Returns:** [SizeBudgetScope](/api/dotsider.core.analysis.models.sizebudgetscope/)

```csharp
Total = 0
```

