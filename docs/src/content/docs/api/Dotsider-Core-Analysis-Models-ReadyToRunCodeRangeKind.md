---
title: "ReadyToRunCodeRangeKind"
description: "What a ReadyToRunCodeRange represents within a precompiled method's body. An R2R method owns one hot entry, zero or more funclets, and an optional disjoint cold range."
slug: api/dotsider.core.analysis.models.readytoruncoderangekind
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

What a [ReadyToRunCodeRange](/api/dotsider.core.analysis.models.readytoruncoderange/) represents within a precompiled method's body. An R2R
method owns one hot entry, zero or more funclets, and an optional disjoint cold range.

```csharp
public enum ReadyToRunCodeRangeKind
```

## Fields

### Cold

The method's cold range, split out via the hot/cold map.

**Returns:** [ReadyToRunCodeRangeKind](/api/dotsider.core.analysis.models.readytoruncoderangekind/)

```csharp
Cold = 2
```

### Funclet

A funclet (exception handler / filter) that follows the hot entry.

**Returns:** [ReadyToRunCodeRangeKind](/api/dotsider.core.analysis.models.readytoruncoderangekind/)

```csharp
Funclet = 1
```

### HotEntry

The method's hot entry point — the range its entry runtime function starts.

**Returns:** [ReadyToRunCodeRangeKind](/api/dotsider.core.analysis.models.readytoruncoderangekind/)

```csharp
HotEntry = 0
```
