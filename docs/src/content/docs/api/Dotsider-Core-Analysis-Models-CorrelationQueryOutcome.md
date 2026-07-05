---
title: "CorrelationQueryOutcome"
description: "The outcome of a CorrelationQuery: how a method-or-address query resolved against an attached companion set's correlation index."
slug: api/dotsider.core.analysis.models.correlationqueryoutcome
sidebar:
  order: 2
---

**Namespace:** `Dotsider.Core.Analysis.Models`

**Assembly:** Dotsider.Core.dll

The outcome of a [CorrelationQuery](/api/dotsider.core.analysis.correlationquery/): how a method-or-address query resolved
against an attached companion set's correlation index.

```csharp
public enum CorrelationQueryOutcome
```

## Fields

### Ambiguous

The query matched several methods (overloads); the candidates are listed and the caller must disambiguate.

**Returns:** [CorrelationQueryOutcome](/api/dotsider.core.analysis.models.correlationqueryoutcome/)

```csharp
Ambiguous = 1
```

### NotFound

The query matched no method or address in the companion set.

**Returns:** [CorrelationQueryOutcome](/api/dotsider.core.analysis.models.correlationqueryoutcome/)

```csharp
NotFound = 2
```

### Resolved

The query resolved to exactly one method; the report is populated.

**Returns:** [CorrelationQueryOutcome](/api/dotsider.core.analysis.models.correlationqueryoutcome/)

```csharp
Resolved = 0
```

### Unavailable

Correlation could not run — no attachable companion, or the index could not be built.

**Returns:** [CorrelationQueryOutcome](/api/dotsider.core.analysis.models.correlationqueryoutcome/)

```csharp
Unavailable = 3
```

