---
title: "ReadyToRunQueryOutcome"
description: "The outcome of a ReadyToRunCorrelationQuery: how a method-or-address query resolved against a ReadyToRun image."
slug: api/dotsider.core.analysis.models.readytorunqueryoutcome
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The outcome of a [ReadyToRunCorrelationQuery](/api/dotsider.core.analysis.readytoruncorrelationquery/): how a method-or-address query
resolved against a ReadyToRun image.

```csharp
public enum ReadyToRunQueryOutcome
```

## Fields

### Ambiguous

The query matched several methods (overloads, or a token and an address); the candidates are listed.

**Returns:** [ReadyToRunQueryOutcome](/api/dotsider.core.analysis.models.readytorunqueryoutcome/)

```csharp
Ambiguous = 1
```

### NotFound

The query matched no method or address in the image.

**Returns:** [ReadyToRunQueryOutcome](/api/dotsider.core.analysis.models.readytorunqueryoutcome/)

```csharp
NotFound = 2
```

### Resolved

The query resolved to exactly one method; the report is populated.

**Returns:** [ReadyToRunQueryOutcome](/api/dotsider.core.analysis.models.readytorunqueryoutcome/)

```csharp
Resolved = 0
```

### Unavailable

Correlation could not run — the image is not a usable ReadyToRun image.

**Returns:** [ReadyToRunQueryOutcome](/api/dotsider.core.analysis.models.readytorunqueryoutcome/)

```csharp
Unavailable = 3
```

